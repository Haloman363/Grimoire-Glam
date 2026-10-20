#!/usr/bin/env python3
"""
French-tip nail guide generator (CadQuery)
==========================================

Generates 3D-printable French-manicure stencils and an optional multi-nail
array plate.

  Component A  Flexible clip-on stencil (print in TPU 95A): a thin arched
               shell that follows the nail's C-curve, with a smile-line
               window, a 45-degree bevelled knife edge, side clips,
               underslung depth stops and a rear pull handle.
  Component B  Array jig plate (10 or 20 positions) for flatbed UV printing.
               Each position has a pocket for the nail and snap slots for
               the stencil's key tongues.

Requirements:  pip install cadquery        (Python 3.9 - 3.12)

Quick start
-----------
  python french_tip_jig.py --size 5 --style classic
  python french_tip_jig.py --size all --style deep --margin 1.2
  python french_tip_jig.py --array 10 --style vshape
  python french_tip_jig.py --array 20 --style classic --format step
  python french_tip_jig.py --selftest

COORDINATE SYSTEM (every function uses this frame)
--------------------------------------------------
  +X  across the nail (left/right sidewalls)
  +Y  along the nail: Y = 0 is the cuticle edge, Y = L is the free edge
  +Z  up, out of the nail's dorsal surface
  The nail's C-curve is a cylinder of radius R whose axis is the line
  (x = 0, z = -R) parallel to Y, so the dorsal apex sits at (0, y, 0).
  Angle theta is measured around that axis from +Z towards +X.

INTERPRETATION NOTES (where the brief was ambiguous)
----------------------------------------------------
  * "Longitudinal C-curve radius" is treated as the transverse curvature,
    i.e. the curve you see looking down the finger.
  * Width and tip length are mapped linearly onto the size number
    (size 9 -> 9 mm / R 6.0 / L 15;  size 0 -> 18 mm / R 8.5 / L 30).
    Override any of them with --c-curve / --tip-length.
  * A chord wider than 2R cannot sit on a circle of radius R (sizes 0-2 with
    R <= 8.5 mm).  The radius is raised just enough that the nail edge sits
    at --max-wrap degrees, and a warning is printed.
  * Handle pitch: negative = tilts down towards the finger (-15 deg).
    Pass --handle-pitch 15 if you want it to lift away instead.
  * "Bleed margin" is how far the stencil rails wrap past the nail-plate
    edge over the sidewall, shielding the skin fold from gel and ink.
  * Only the smile edge carries the full 45-degree bevel; the short lateral
    window walls are cut square.
  * Tested with CadQuery 2.x: --selftest builds all 40 size/style combinations
    as single valid solids, and --array 10 builds (plate is ~251 mm wide, so
    check your printer bed).  The 20-position plate and STEP export were not run.
  * Nail bed and finger geometry are not modelled; fit is by nominal radius
    plus --clearance, so print a test fit before running a full set.
"""
from __future__ import annotations

import argparse
import math
import os
import sys
from dataclasses import dataclass, replace

import cadquery as cq

# =============================================================================
# 1. CONFIGURATION  (edit here, or override from the command line)
# =============================================================================


@dataclass(frozen=True)
class Params:
    # ---- what to build ------------------------------------------------------
    size: int = 5                    # 0 (widest) .. 9 (narrowest)
    style: str = "classic"           # classic | deep | vshape | straight

    # ---- per-size overrides (None = derive from size) -----------------------
    c_curve_radius: float | None = None   # mm
    tip_length: float | None = None       # mm, stencil length along the nail

    # ---- tolerances & offsets ----------------------------------------------
    wall_body: float = 1.0           # mm, shell thickness at the rear
    wall_edge: float = 0.4           # mm, thickness near the smile edge
    lip: float = 0.2                 # mm, flat land left at the knife edge
    bevel_deg: float = 45.0          # taper angle off the surface normal
    margin: float = 1.0              # mm, 0.5 .. 1.5 side wrap past the nail
    clearance: float = 0.1           # mm, gap between shell and nail surface
    nail_thickness: float = 0.6      # mm, used to place underslung features
    max_wrap_deg: float = 70.0       # nail half-angle limit (see notes above)

    # ---- smile line ---------------------------------------------------------
    tip_zone_frac: float = 0.35      # smile-line apex distance from the free
                                     # edge, as a fraction of tip length
    straight_fillet: float = 0.5     # mm, corner radius for the Straight style

    # ---- handle (Component A) ----------------------------------------------
    handle_length: float = 20.0
    handle_width: float = 6.0
    handle_thickness: float = 1.5
    handle_pitch_deg: float = -15.0

    # ---- clips and stops ----------------------------------------------------
    clip_drop: float = 2.5           # mm of arc each side clip runs down the sidewall
    clip_width: float = 2.5          # mm along Y
    clip_hook: float = 0.7           # mm inward hook at the clip tip
    stop_thickness: float = 1.0      # mm, depth stop beyond the free edge
    stop_width: float = 2.5          # mm of arc for each depth stop
    ledge_length: float = 3.0        # mm, underslung ledge under the free edge

    # ---- array plate (Component B) -----------------------------------------
    plate_thickness: float = 8.0
    pocket_depth: float = 5.0
    slot_gap: float = -0.10          # negative = interference (snap fit)
    tongue_length: float = 3.0       # mm, key tongue sticking out sideways
    tongue_width: float = 2.2        # mm along Y
    tongue_thickness: float = 1.2
    plate_gap: float = 3.0           # mm between neighbouring positions
    plate_pad: float = 5.0           # mm border around the outermost positions

    # ---- mesh quality -------------------------------------------------------
    samples: int = 49                # sections along the smile line (made odd)
    stl_tolerance: float = 0.01      # mm linear deflection
    stl_angular_tol: float = 0.1     # rad


STYLES = ("classic", "deep", "vshape", "straight")
SIZE_MIN, SIZE_MAX = 0, 9
WIDTH_RANGE = (9.0, 18.0)      # size 9 .. size 0
RADIUS_RANGE = (6.0, 8.5)
LENGTH_RANGE = (15.0, 30.0)


# =============================================================================
# 2. SIZE TABLE
# =============================================================================


def lerp(a: float, b: float, t: float) -> float:
    return a + (b - a) * t


@dataclass(frozen=True)
class NailDims:
    size: int
    width: float        # chord across the nail plate
    radius: float       # dorsal C-curve radius actually used
    length: float       # stencil length along Y
    phi_nail: float     # half-angle of the nail plate (rad)
    phi_shell: float    # half-angle of the stencil rails (rad)
    r_in: float         # inner shell radius (nail radius + clearance)
    r_out: float        # outer shell radius at the rear (r_in + wall_body)


def nail_dims(p: Params) -> NailDims:
    """Map a size number to nail geometry.

    t = 0 at size 9 (smallest) and t = 1 at size 0 (largest), so width, radius
    and length all grow together.  The half-angle follows from the chord:

        sin(phi) = (width / 2) / R

    which is only defined when R >= width / 2.  If it would exceed
    max_wrap_deg, R is raised to (width / 2) / sin(max_wrap).
    """
    if not SIZE_MIN <= p.size <= SIZE_MAX:
        raise ValueError(f"size must be {SIZE_MIN}..{SIZE_MAX}, got {p.size}")
    t = (SIZE_MAX - p.size) / (SIZE_MAX - SIZE_MIN)
    width = lerp(WIDTH_RANGE[0], WIDTH_RANGE[1], t)       # 9 mm .. 18 mm
    radius = p.c_curve_radius or lerp(RADIUS_RANGE[0], RADIUS_RANGE[1], t)
    length = p.tip_length or lerp(LENGTH_RANGE[0], LENGTH_RANGE[1], t)

    half = width / 2.0
    r_needed = half / math.sin(math.radians(p.max_wrap_deg))
    if radius < r_needed:
        print(f"  [warn] size {p.size}: width {width:.1f} mm cannot sit on "
              f"R {radius:.2f} mm; using R {r_needed:.2f} mm", file=sys.stderr)
        radius = r_needed

    r_in = radius + p.clearance
    phi_nail = math.asin(half / radius)
    # Rails wrap `margin` mm of arc further round the sidewall.
    phi_shell = phi_nail + p.margin / r_in
    return NailDims(p.size, width, radius, length, phi_nail, phi_shell,
                    r_in, r_in + p.wall_body)


# =============================================================================
# 3. SMILE-LINE MATH
# =============================================================================
#
# The smile line is a function y = f(u) of the normalised arc position
# u in [-1, 1] across the nail (u = theta / phi_nail, so the sidewalls are
# at u = +-1 and the line follows the curved nail rather than its flat
# projection).  Every style is
#
#       f(u) = y_apex + depth * g(u)           (g(0) = 0, g >= 0)
#
# where y_apex is the point closest to the cuticle and the line rises towards
# the free edge at the sides, giving the "smile".
#
#   classic   g = u^2                  parabola, gentle sides
#   deep      g = 1 - sqrt(1 - u^2)    quarter ellipse: the slope is vertical at
#                                      the sidewalls, so the line sweeps all
#                                      the way up to them, and depth is doubled
#   vshape    g = |u|                  straight rakes meeting at a sharp apex
#   straight  g = 0                    flat; depth is 0 and the window corners
#                                      are rounded by `_corner_fillet`


def _corner_fillet(s: float, s_edge: float, rho: float) -> float:
    """Raise the flat floor near the lateral walls to round the corner.

    s: arc distance from the centreline; s_edge: arc distance to the wall.
    Inside the last rho mm the floor follows a quarter circle of radius rho
    that meets the wall vertically:  dy = rho - sqrt(rho^2 - (s - s_edge + rho)^2)
    """
    d = abs(s) - (s_edge - rho)
    if d <= 0.0:
        return 0.0
    return rho - math.sqrt(max(rho * rho - d * d, 0.0))


def smile_depth(style: str, width: float) -> float:
    """Total rise (mm) of the smile line from apex to sidewall."""
    return {"classic": 0.20 * width,
            "deep": 0.40 * width,
            "vshape": 0.30 * width,
            "straight": 0.0}[style]


def smile_rise(style: str, u: float, depth: float, s_edge: float,
               fillet: float) -> float:
    """Height of the smile line above its apex at normalised position u."""
    a = abs(u)
    if style == "classic":
        return depth * a * a
    if style == "deep":
        return depth * (1.0 - math.sqrt(max(1.0 - a * a, 0.0)))
    if style == "vshape":
        return depth * a
    if style == "straight":
        return _corner_fillet(u * s_edge, s_edge, fillet)
    raise ValueError(f"unknown style {style!r}; choose from {STYLES}")


def smile_samples(n: int) -> list[float]:
    """n (odd) positions in [-1, 1], u = sin(tau).

    The sine spacing bunches points near |u| = 1, where the deep style and the
    straight-style fillets change fastest, and always includes u = 0 exactly
    so the V apex is a real vertex.
    """
    n |= 1
    return [math.sin(math.pi / 2.0 * (2.0 * k / (n - 1) - 1.0)) for k in range(n)]


# =============================================================================
# 4. GEOMETRY HELPERS
# =============================================================================


def _axis_z(d: NailDims) -> float:
    """Z of the C-curve axis: the dorsal apex is at z = 0."""
    return -d.radius


def polar_xz(d: NailDims, r: float, theta: float) -> tuple[float, float]:
    """(x, z) of the point at radius r, angle theta around the C-curve axis."""
    return r * math.sin(theta), _axis_z(d) + r * math.cos(theta)


def _cylinder(d: NailDims, r: float, y0: float, y1: float) -> cq.Workplane:
    # "XZ" workplane: local x = X, local y = Z, extrusion runs towards -Y.
    return (cq.Workplane("XZ", origin=(0, y1, _axis_z(d)))
            .circle(r).extrude(y1 - y0))


def _wedge(d: NailDims, th0: float, th1: float, y0: float, y1: float) -> cq.Workplane:
    """Prism bounded by two radial planes at th0, th1 (a pie slice)."""
    rho = 4.0 * d.r_out
    steps = max(1, math.ceil(abs(th1 - th0) / math.radians(30.0)))
    pts = [(0.0, 0.0)] + [
        (rho * math.sin(th0 + (th1 - th0) * k / steps),
         rho * math.cos(th0 + (th1 - th0) * k / steps))
        for k in range(steps + 1)
    ]
    return (cq.Workplane("XZ", origin=(0, y1, _axis_z(d)))
            .polyline(pts).close().extrude(y1 - y0))


def ring_sector(d: NailDims, r0: float, r1: float, th0: float, th1: float,
                y0: float, y1: float) -> cq.Workplane:
    """Piece of a cylindrical shell: radius r0..r1, angle th0..th1, Y y0..y1."""
    ring = _cylinder(d, r1, y0, y1).cut(_cylinder(d, r0, y0 - 1.0, y1 + 1.0))
    return ring.intersect(_wedge(d, th0, th1, y0 - 1.0, y1 + 1.0))


def _mirror_x(w: cq.Workplane) -> cq.Workplane:
    return w.mirror("YZ")


def _sym(w: cq.Workplane) -> cq.Workplane:
    """Union of a +X feature with its mirror image."""
    return w.union(_mirror_x(w))


# =============================================================================
# 5. COMPONENT A - STENCIL
# =============================================================================


@dataclass(frozen=True)
class SmileLayout:
    y_apex: float      # smile line at the centre
    depth: float       # rise at the sidewalls
    y_hi: float        # highest point of the line (toward the free edge)


def smile_layout(p: Params, d: NailDims) -> SmileLayout:
    y_apex = d.length * (1.0 - p.tip_zone_frac)
    depth = smile_depth(p.style, d.width)
    # Keep at least 1 mm of window ahead of the highest point.
    depth = min(depth, max(d.length - 1.0 - y_apex, 0.0))
    return SmileLayout(y_apex, depth, y_apex + depth)


def _shell_body(p: Params, d: NailDims, lay: SmileLayout) -> cq.Workplane:
    """Arched shell whose thickness tapers from wall_body to wall_edge.

    The outer surface is a surface of revolution about the C-curve axis, so a
    single 2D profile (Y, r) revolved 360 degrees gives the tapered wall; it
    is then trimmed to the rail half-angle.  Wall profile:

        t(y) = wall_body                        y <= y_ts
        t(y) = lerp(wall_body, wall_edge, ...)  y_ts < y < y_hi   (linear)
        t(y) = wall_edge                        y >= y_hi
    """
    y_ts = 0.4 * lay.y_apex
    z_in = d.r_in + _axis_z(d)
    z_body = d.r_out + _axis_z(d)
    z_edge = d.r_in + p.wall_edge + _axis_z(d)
    L = d.length
    profile = [(0, z_in), (L, z_in), (L, z_edge), (lay.y_hi, z_edge),
               (y_ts, z_body), (0, z_body)]
    # "YZ" workplane: local x = Y, local y = Z.
    body = (cq.Workplane("YZ").polyline(profile).close()
            .revolve(360, (0, _axis_z(d), 0), (1, _axis_z(d), 0)))
    return body.intersect(_wedge(d, -d.phi_shell, d.phi_shell, 0.0, L))


def _window_cutter(p: Params, d: NailDims, lay: SmileLayout) -> cq.Solid:
    """Solid removed to open the smile-line window, with the bevel built in.

    One planar section per smile sample, lying in the radial half-plane at
    angle theta_k.  In that plane the section is (r, y):

        (r_in - e , f)          start below the nail surface
        (r_in+lip , f)          vertical land of height `lip`  (the sharp edge)
        (r_top    , f - dr*tan(bevel))   45-degree slope back from the opening
        (r_top    , y_far)
        (r_in - e , y_far)

    where f = f(u_k) is the smile height.  Because the plane contains the
    C-curve axis, the slope is measured against the true surface normal at
    every angle.  A ruled loft through all sections gives the finished cutter.
    """
    e = 0.3
    r_top = d.r_out + 0.5
    dr = r_top - d.r_in - p.lip
    setback = dr * math.tan(math.radians(p.bevel_deg))
    y_far = d.length + 1.0
    s_edge = d.r_in * d.phi_nail

    wires = []
    for u in smile_samples(p.samples):
        theta = u * d.phi_nail
        f = lay.y_apex + smile_rise(p.style, u, lay.depth, s_edge, p.straight_fillet)
        section = [(d.r_in - e, f), (d.r_in + p.lip, f), (r_top, f - setback),
                   (r_top, y_far), (d.r_in - e, y_far)]
        pts = []
        for r, y in section:
            x, z = polar_xz(d, r, theta)
            pts.append(cq.Vector(x, y, z))
        pts.append(pts[0])
        wires.append(cq.Wire.makePolygon(pts))
    return cq.Solid.makeLoft(wires, True)


def _handle(p: Params, d: NailDims) -> cq.Workplane:
    """Rear pull tab: 20 x 6 x 1.5 mm, hinged at the cuticle edge.

    The plate is built flat, running from Y = -length to Y = +1 (the extra mm
    overlaps the shell), then rotated about an X-parallel axis at the hinge.
    Rotating by beta about +X sends the rear direction (0,-1,0) to
    (0, -cos beta, -sin beta), so beta = -pitch gives the requested pitch.
    """
    z0 = d.r_in + _axis_z(d)
    plate = (cq.Workplane("XY")
             .box(p.handle_width, p.handle_length + 1.0, p.handle_thickness,
                  centered=(True, False, False))
             .translate((0, -p.handle_length, z0)))
    pivot_z = z0 + p.handle_thickness / 2.0
    return plate.rotate((0, 0, pivot_z), (1, 0, pivot_z), -p.handle_pitch_deg)


def _side_clips(p: Params, d: NailDims) -> cq.Workplane:
    """Lateral tabs that run down the sidewall and hook inward.

    Two per side.  Each is a strip of shell, `clip_drop` mm of arc long,
    continuing the rail past phi_shell, ending in an inward hook that grips
    below the nail fold and stops the stencil rotating or sliding sideways.
    """
    t = 0.7
    th0 = d.phi_shell - 0.5 / d.r_in            # overlap into the rail
    th1 = d.phi_shell + p.clip_drop / d.r_in
    th_hook = th1 - 0.8 / d.r_in
    clips = None
    for yc in (0.30 * d.length, 0.70 * d.length):
        y0, y1 = yc - p.clip_width / 2.0, yc + p.clip_width / 2.0
        arm = ring_sector(d, d.r_in, d.r_in + t, th0, th1, y0, y1)
        hook = ring_sector(d, d.r_in - p.clip_hook, d.r_in + t, th_hook, th1, y0, y1)
        piece = arm.union(hook)
        clips = piece if clips is None else clips.union(piece)
    return _sym(clips)


def _depth_stops(p: Params, d: NailDims) -> cq.Workplane:
    """Underslung registration tabs that fix how far the nail slides in.

    At each rail end a block stands just beyond the free edge (Y = L) and a
    thin ledge runs back underneath it, so the tip is trapped between the
    shell above and the ledge below, and stops against the block face.
    """
    L = d.length
    th_lo = d.phi_nail - p.stop_width / d.r_in
    r_under_top = d.r_in - p.nail_thickness - 0.1        # just below the nail
    r_under_bot = r_under_top - 0.8
    block = ring_sector(d, r_under_bot, d.r_out, th_lo, d.phi_shell,
                        L - 0.1, L + p.stop_thickness)
    ledge = ring_sector(d, r_under_bot, r_under_top, th_lo, d.phi_shell,
                        L - p.ledge_length, L + p.stop_thickness)
    return _sym(block.union(ledge))


def _key_tongues(p: Params, d: NailDims) -> cq.Workplane:
    """Flat tongues under the rail edges that press into the plate slots."""
    x_in, z_in = polar_xz(d, d.r_in, d.phi_shell)
    boxes = None
    for yc in tongue_y_positions(d):
        b = (cq.Workplane("XY")
             .box(p.tongue_length + 0.2, p.tongue_width, p.tongue_thickness + 0.5,
                  centered=(False, True, False))
             .translate((x_in - 0.2, yc, z_in - p.tongue_thickness)))
        boxes = b if boxes is None else boxes.union(b)
    return _sym(boxes)


def tongue_y_positions(d: NailDims) -> tuple[float, float]:
    return (0.25 * d.length, 0.75 * d.length)


def build_stencil(p: Params, with_tongues: bool = False) -> cq.Workplane:
    """Assemble Component A.  Returns a single solid."""
    d = nail_dims(p)
    lay = smile_layout(p, d)

    shell = _shell_body(p, d, lay)
    shell = shell.cut(cq.Workplane("XY").add(_window_cutter(p, d, lay)))
    # Register features go on after the window cut so nothing is trimmed.
    shell = (shell.union(_side_clips(p, d))
                  .union(_depth_stops(p, d))
                  .union(_handle(p, d)))
    if with_tongues:
        shell = shell.union(_key_tongues(p, d))
    return shell.clean()


# =============================================================================
# 6. COMPONENT B - ARRAY PLATE
# =============================================================================


def _footprint_half_width(p: Params, d: NailDims) -> float:
    x_in, _ = polar_xz(d, d.r_in, d.phi_shell)
    return x_in + p.tongue_length + 0.5


def _slot_cutters(p: Params, d: NailDims) -> cq.Workplane:
    """Negative of one position, in the stencil's own frame.

    * a pocket under the arch for the nail / dummy tip and the ledges
    * two tongue slots per side, sized with `slot_gap` (negative = press fit)
    """
    x_in, z_in = polar_xz(d, d.r_in, d.phi_shell)
    x_pocket = x_in - 0.3
    L = d.length + p.stop_thickness + 1.0
    pocket = (cq.Workplane("XY")
              .box(2 * x_pocket, L + 0.5, p.pocket_depth,
                   centered=(True, False, False))
              .translate((0, -0.5, z_in - p.pocket_depth)))
    g = p.slot_gap
    slots = None
    for yc in tongue_y_positions(d):
        s = (cq.Workplane("XY")
             .box(p.tongue_length + 2 * g + 0.2, p.tongue_width + 2 * g,
                  p.tongue_thickness + g + 0.2, centered=(False, True, False))
             .translate((x_in - 0.2, yc, z_in - p.tongue_thickness - g)))
        slots = s if slots is None else slots.union(s)
    return pocket.union(_sym(slots))


def build_array_plate(p: Params, positions: int = 10) -> tuple[cq.Workplane, cq.Workplane]:
    """Plate plus the loaded stencils (sizes 0-9, one or two rows).

    10 positions: one row, sizes 0..9 in order.
    20 positions: two rows, a full set each; row 2 is turned 180 degrees so
                  the handles overhang opposite plate edges.
    Returns (plate, stencils).  Plate top face is z = 0, the stencil seat.
    """
    if positions not in (10, 20):
        raise ValueError("positions must be 10 or 20")
    rows = positions // 10
    dims = [nail_dims(replace(p, size=s)) for s in range(10)]
    halves = [_footprint_half_width(p, d) for d in dims]

    # Row pitch along X (same for both rows).
    xs, cursor = [], p.plate_pad
    for h in halves:
        xs.append(cursor + h)
        cursor += 2 * h + p.plate_gap
    plate_w = cursor - p.plate_gap + p.plate_pad

    row_len = max(d.length for d in dims) + p.stop_thickness + 2.0
    row_y = [0.0]
    if rows == 2:
        row_y.append(2 * row_len + p.plate_gap)      # row 2 rear edge (flipped)
    y_min = -p.plate_pad
    y_max = (row_y[-1] if rows == 2 else row_len) + p.plate_pad

    plate = (cq.Workplane("XY")
             .box(plate_w, y_max - y_min, p.plate_thickness, centered=False)
             .translate((0, y_min, -p.plate_thickness)))
    stencils = None
    for r in range(rows):
        for s in range(10):
            d, x = dims[s], xs[s]
            _, z_in = polar_xz(d, d.r_in, d.phi_shell)
            st = build_stencil(replace(p, size=s), with_tongues=True)
            neg = _slot_cutters(replace(p, size=s), d)

            def place(w: cq.Workplane) -> cq.Workplane:
                w = w.translate((0, 0, -z_in))               # rail seat -> z = 0
                if r == 1:
                    w = w.rotate((0, 0, 0), (0, 0, 1), 180)  # handle to +Y
                return w.translate((x, row_y[r], 0))

            plate = plate.cut(place(neg))
            st = place(st)
            stencils = st if stencils is None else stencils.add(st)
    return plate, stencils


# =============================================================================
# 7. EXPORT / VALIDATION
# =============================================================================


def report(name: str, shape: cq.Workplane) -> None:
    solids = shape.solids().vals()
    bb = shape.val().BoundingBox()
    ok = all(s.isValid() for s in solids)
    vol = sum(s.Volume() for s in solids)
    print(f"  {name}: {len(solids)} solid(s), valid={ok}, volume={vol:.1f} mm3, "
          f"bbox {bb.xlen:.1f} x {bb.ylen:.1f} x {bb.zlen:.1f} mm")


def export(p: Params, shape: cq.Workplane, path_no_ext: str, fmt: str) -> list[str]:
    written = []
    for ext in (["stl", "step"] if fmt == "both" else [fmt]):
        path = f"{path_no_ext}.{ext}"
        if ext == "stl":
            cq.exporters.export(shape, path, tolerance=p.stl_tolerance,
                                angularTolerance=p.stl_angular_tol)
        else:
            cq.exporters.export(shape, path)
        written.append(path)
    return written


def _compound(parts: cq.Workplane) -> cq.Workplane:
    return cq.Workplane("XY").newObject([cq.Compound.makeCompound(parts.solids().vals())])


# =============================================================================
# 8. COMMAND LINE
# =============================================================================


def parse_args(argv=None) -> tuple[argparse.Namespace, Params]:
    a = argparse.ArgumentParser(
        description="French-tip nail stencil and array-plate generator.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter)
    d = Params()
    a.add_argument("--size", default=str(d.size), help="0-9, or 'all' for a full set")
    a.add_argument("--style", default=d.style, help=f"{', '.join(STYLES)}, or 'all'")
    a.add_argument("--array", type=int, choices=(0, 10, 20), default=0,
                   help="also build an array plate with this many positions")
    a.add_argument("--outdir", default="out")
    a.add_argument("--format", choices=("stl", "step", "both"), default="both")
    a.add_argument("--wall", type=float, default=d.wall_body, help="rear wall, mm")
    a.add_argument("--wall-edge", type=float, default=d.wall_edge)
    a.add_argument("--lip", type=float, default=d.lip)
    a.add_argument("--bevel", type=float, default=d.bevel_deg, help="degrees")
    a.add_argument("--margin", type=float, default=d.margin, help="0.5 .. 1.5 mm")
    a.add_argument("--clearance", type=float, default=d.clearance)
    a.add_argument("--c-curve", type=float, default=None, help="radius override, mm")
    a.add_argument("--tip-length", type=float, default=None, help="length override, mm")
    a.add_argument("--max-wrap", type=float, default=d.max_wrap_deg)
    a.add_argument("--tip-zone", type=float, default=d.tip_zone_frac)
    a.add_argument("--handle-pitch", type=float, default=d.handle_pitch_deg)
    a.add_argument("--samples", type=int, default=d.samples)
    a.add_argument("--selftest", action="store_true",
                   help="build every size/style, check validity, write nothing")
    ns = a.parse_args(argv)

    if not 0.5 <= ns.margin <= 1.5:
        a.error("--margin must be between 0.5 and 1.5 mm")
    if ns.wall_edge >= ns.wall or ns.lip >= ns.wall_edge:
        a.error("need lip < wall-edge < wall")
    if not 0 < ns.bevel < 80:
        a.error("--bevel must be between 0 and 80 degrees")
    if ns.style != "all" and ns.style not in STYLES:
        a.error(f"--style must be one of {STYLES} or 'all'")
    if ns.size != "all" and not ns.size.isdigit():
        a.error("--size must be 0-9 or 'all'")

    p = replace(d, wall_body=ns.wall, wall_edge=ns.wall_edge, lip=ns.lip,
                bevel_deg=ns.bevel, margin=ns.margin, clearance=ns.clearance,
                c_curve_radius=ns.c_curve, tip_length=ns.tip_length,
                max_wrap_deg=ns.max_wrap, tip_zone_frac=ns.tip_zone,
                handle_pitch_deg=ns.handle_pitch, samples=ns.samples)
    return ns, p


def main(argv=None) -> int:
    ns, base = parse_args(argv)
    sizes = range(10) if ns.size == "all" else [int(ns.size)]
    styles = STYLES if ns.style == "all" else [ns.style]

    if ns.selftest:
        bad = 0
        for style in STYLES:
            for s in range(10):
                st = build_stencil(replace(base, size=s, style=style))
                n = len(st.solids().vals())
                ok = n == 1 and st.val().isValid() and st.val().Volume() > 0
                bad += not ok
                print(f"  size {s} {style:8s} solids={n} valid={ok}")
        print("selftest", "FAILED" if bad else "passed")
        return 1 if bad else 0

    os.makedirs(ns.outdir, exist_ok=True)
    for style in styles:
        for s in sizes:
            p = replace(base, size=s, style=style)
            print(f"Stencil size {s}, {style}")
            st = build_stencil(p)
            report("stencil", st)
            print("   ", *export(p, st, os.path.join(ns.outdir, f"stencil_{style}_size{s}"), ns.format))
        if ns.array:
            p = replace(base, style=style)
            print(f"Array plate, {ns.array} positions, {style}")
            plate, loaded = build_array_plate(p, ns.array)
            report("plate", plate)
            print("   ", *export(p, plate, os.path.join(ns.outdir, f"plate_{ns.array}_{style}"), ns.format))
            print("   ", *export(p, _compound(loaded),
                                 os.path.join(ns.outdir, f"plate_{ns.array}_{style}_loaded"), ns.format))
    return 0


if __name__ == "__main__":
    sys.exit(main())
