FeatureScript 3095;
import(path : "onshape/std/common.fs", version : "3095.0");

/**
 * French Tip Jig Generator  (Onshape FeatureScript)
 * =================================================
 * Builds a flexible clip-on French-tip stencil (print in TPU 95A) for one nail
 * size, and optionally a 10-position array plate that holds the full 0-9 set
 * for flatbed UV printing or batch assembly.
 *
 * COORDINATE FRAME (fixed to the Part Studio origin, Z up)
 *   +X  across the nail        +Y  from cuticle (Y = 0) to free edge (Y = L)
 *   +Z  out of the nail's dorsal surface.
 * The C-curve is a cylinder of radius R about the line (x = 0, z = -R) parallel
 * to Y, so the dorsal apex is at (0, y, 0).  theta is measured around that axis
 * from +Z towards +X.
 *
 * HOW THE UI MAPS TO GEOMETRY
 *   Nail size (0-9)        width = 9 + (9 - size) mm       (size 9 = 9, size 0 = 18)
 *                          R     = 6.0 + 2.5 * t           (t = (9 - size) / 9)
 *                          L     = 15 + 15 * t
 *   Tip style              shape of the smile line, see smileRise()
 *   Bleed margin           arc length the side rails wrap past the nail plate
 *   Wall thickness         rear wall, tapering to the edge wall at the smile
 *   Bevel angle / lip      opChamfer on the outer edge of the window
 *
 * NOTES
 *   * A chord wider than 2R cannot lie on a circle of radius R (sizes 0-2).
 *     The radius is raised to (width / 2) / sin(max wrap angle) and the feature
 *     reports it as an info message.
 *   * The window is cut with a vertical (Z) prism and then chamfered.  The lip
 *     is measured along the cut wall, so on the steep flanks of the arch the
 *     remaining wall is slightly taller than at the apex.
 *   * Handle pitch: negative tilts down towards the finger.
 *   * Tested in Onshape (library 3095): all four styles at sizes 5 and 0 build as
 *     one solid with the 45-degree bevel applied, and the 10-position plate
 *     builds without errors.  Bevel angles other than 45 use a two-offset
 *     chamfer that has not been exercised; if the bevel leans the wrong way,
 *     tick "Flip bevel direction".  Fit on a real nail is also untested, so
 *     print one size first and adjust "Fit clearance".
 */

export enum TipStyle
{
    annotation { "Name" : "Classic curve" }
    CLASSIC,
    annotation { "Name" : "Deep curve" }
    DEEP,
    annotation { "Name" : "V-shape" }
    VSHAPE,
    annotation { "Name" : "Straight" }
    STRAIGHT
}

export const NAIL_SIZE_BOUNDS = { (unitless) : [0, 5, 9] } as IntegerBoundSpec;
export const BLEED_BOUNDS = { (millimeter) : [0.5, 1.0, 1.5] } as LengthBoundSpec;
export const WALL_BOUNDS = { (millimeter) : [0.5, 1.0, 3.0] } as LengthBoundSpec;
export const EDGE_WALL_BOUNDS = { (millimeter) : [0.25, 0.4, 1.0] } as LengthBoundSpec;
export const LIP_BOUNDS = { (millimeter) : [0.05, 0.2, 0.5] } as LengthBoundSpec;
export const FIT_BOUNDS = { (millimeter) : [0.0, 0.1, 0.5] } as LengthBoundSpec;
export const BEVEL_BOUNDS = { (degree) : [10, 45, 80] } as AngleBoundSpec;
export const HANDLE_ANGLE_BOUNDS = { (degree) : [-60, -15, 60] } as AngleBoundSpec;
export const CUSTOM_LENGTH_BOUNDS = { (millimeter) : [5, 20, 60] } as LengthBoundSpec;
export const HANDLE_BOUNDS = { (millimeter) : [1, 6, 60] } as LengthBoundSpec;

annotation { "Feature Type Name" : "French Tip Jig Generator" }
export const frenchTipJig = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Nail size (0 widest - 9 narrowest)" }
        isInteger(definition.nailSize, NAIL_SIZE_BOUNDS);

        annotation { "Name" : "Tip style" }
        definition.tipStyle is TipStyle;

        annotation { "Name" : "Override radius and length", "Default" : false }
        definition.useCustom is boolean;

        if (definition.useCustom)
        {
            annotation { "Name" : "C-curve radius" }
            isLength(definition.customRadius, CUSTOM_LENGTH_BOUNDS);

            annotation { "Name" : "Tip length" }
            isLength(definition.customLength, CUSTOM_LENGTH_BOUNDS);
        }

        annotation { "Name" : "Bleed margin" }
        isLength(definition.margin, BLEED_BOUNDS);

        annotation { "Name" : "Rear wall thickness" }
        isLength(definition.wallBody, WALL_BOUNDS);

        annotation { "Name" : "Edge wall thickness" }
        isLength(definition.wallEdge, EDGE_WALL_BOUNDS);

        annotation { "Name" : "Bevel angle" }
        isAngle(definition.bevel, BEVEL_BOUNDS);

        annotation { "Name" : "Lip thickness" }
        isLength(definition.lip, LIP_BOUNDS);

        annotation { "Name" : "Flip bevel direction (only if angle is not 45)", "Default" : false }
        definition.flipBevel is boolean;

        annotation { "Name" : "Fit clearance" }
        isLength(definition.clearance, FIT_BOUNDS);

        annotation { "Name" : "Handle length" }
        isLength(definition.handleLength, HANDLE_BOUNDS);

        annotation { "Name" : "Handle width" }
        isLength(definition.handleWidth, HANDLE_BOUNDS);

        annotation { "Name" : "Handle thickness" }
        isLength(definition.handleThickness, HANDLE_BOUNDS);

        annotation { "Name" : "Handle pitch" }
        isAngle(definition.handlePitch, HANDLE_ANGLE_BOUNDS);

        annotation { "Name" : "Build 10-position array plate", "Default" : false }
        definition.buildArray is boolean;
    }
    {
        var P = parametersFromDefinition(definition);

        if (!definition.buildArray)
        {
            var d = nailDims(context, id, definition.nailSize, P);
            buildStencil(context, id + "stencil", d, P, false);
        }
        else
        {
            buildArrayPlate(context, id, P);
        }
    }, {
        nailSize : 5,
        tipStyle : TipStyle.CLASSIC,
        useCustom : false,
        customRadius : 7 * millimeter,
        customLength : 22 * millimeter,
        margin : 1.0 * millimeter,
        wallBody : 1.0 * millimeter,
        wallEdge : 0.4 * millimeter,
        bevel : 45 * degree,
        lip : 0.2 * millimeter,
        flipBevel : false,
        clearance : 0.1 * millimeter,
        handleLength : 20 * millimeter,
        handleWidth : 6 * millimeter,
        handleThickness : 1.5 * millimeter,
        handlePitch : -15 * degree,
        buildArray : false
    });

// ============================================================================
// Parameters (everything below works in plain millimetres / radians)
// ============================================================================

function parametersFromDefinition(definition is map) returns map
{
    return {
        "style" : definition.tipStyle,
        "useCustom" : definition.useCustom,
        "customRadius" : definition.useCustom ? definition.customRadius / millimeter : 0,
        "customLength" : definition.useCustom ? definition.customLength / millimeter : 0,
        "margin" : definition.margin / millimeter,
        "wallBody" : definition.wallBody / millimeter,
        "wallEdge" : min(definition.wallEdge, definition.wallBody) / millimeter,
        "bevel" : definition.bevel / radian,
        "lip" : definition.lip / millimeter,
        "flipBevel" : definition.flipBevel,
        "clearance" : definition.clearance / millimeter,
        "handleLength" : definition.handleLength / millimeter,
        "handleWidth" : definition.handleWidth / millimeter,
        "handleThickness" : definition.handleThickness / millimeter,
        "handlePitch" : definition.handlePitch / radian,
        // fixed constants
        "nailThickness" : 0.6,
        "maxWrap" : 70 * PI / 180,
        "tipZoneFrac" : 0.35,
        "straightFillet" : 0.5,
        "clipDrop" : 2.0,
        "clipWidth" : 2.5,
        "clipHook" : 0.7,
        "stopThickness" : 1.0,
        "stopWidth" : 2.5,
        "ledgeLength" : 3.0,
        "tongueLength" : 3.0,
        "tongueWidth" : 2.2,
        "tongueThickness" : 1.2,
        "slotGap" : -0.1,
        "plateThickness" : 8.0,
        "pocketDepth" : 5.0,
        "plateGap" : 3.0,
        "plateMargin" : 5.0,
        "samples" : 33
    };
}

/** Size number -> nail dimensions (see header for the mapping). */
function nailDims(context is Context, id is Id, size is number, P is map) returns map
{
    var t = (9 - size) / 9;
    var width = 9 + 9 * t;
    var R = P.useCustom ? P.customRadius : 6.0 + 2.5 * t;
    var L = P.useCustom ? P.customLength : 15 + 15 * t;

    // sin(phi) = (width / 2) / R  needs R >= width / 2.
    var rNeeded = (width / 2) / sin(P.maxWrap * radian);
    if (R < rNeeded)
    {
        reportFeatureInfo(context, id, "Size " ~ size ~ ": width " ~ width ~ " mm cannot sit on R "
            ~ roundToPrecision(R, 2) ~ " mm; using R " ~ roundToPrecision(rNeeded, 2) ~ " mm.");
        R = rNeeded;
    }
    var rIn = R + P.clearance;
    var phiNail = asin((width / 2) / R) / radian;
    var yApex = L * (1 - P.tipZoneFrac);
    var depth = smileDepth(P.style, width);
    depth = min(depth, max(L - 1 - yApex, 0));
    return {
        "size" : size, "width" : width, "R" : R, "L" : L, "rIn" : rIn,
        "rOut" : rIn + P.wallBody,
        "phiNail" : phiNail,
        "phiShell" : phiNail + P.margin / rIn,
        "yApex" : yApex, "depth" : depth, "yHi" : yApex + depth
    };
}

// ============================================================================
// Smile-line mathematics
// ============================================================================
// f(u) = yApex + depth * g(u), u in [-1, 1] (u = theta / phiNail).
//   classic  g = u^2                parabola
//   deep     g = 1 - sqrt(1 - u^2)  quarter ellipse, vertical at the sidewalls
//   vshape   g = |u|                straight rakes, sharp apex
//   straight flat; corners rounded with 0.5 mm arcs (built as arcs, not samples)

function smileDepth(style, width is number) returns number
{
    if (style == TipStyle.CLASSIC)
        return 0.20 * width;
    if (style == TipStyle.DEEP)
        return 0.40 * width;
    if (style == TipStyle.VSHAPE)
        return 0.30 * width;
    return 0;
}

function smileRise(style, u is number, depth is number) returns number
{
    var a = abs(u);
    if (style == TipStyle.CLASSIC)
        return depth * a * a;
    if (style == TipStyle.DEEP)
        return depth * (1 - sqrt(max(1 - a * a, 0)));
    if (style == TipStyle.VSHAPE)
        return depth * a;
    return 0;
}

/** n+1 positions in [-1, 1], u = sin(tau): dense near the sidewalls, exact 0 in the middle. */
function smileSamples(n is number) returns array
{
    var out = [];
    for (var k = 0; k <= n; k += 1)
        out = append(out, sin(PI / 2 * (2 * k / n - 1) * radian));
    return out;
}

// ============================================================================
// Small geometry helpers
// ============================================================================

function mm3(x is number, y is number, z is number) returns Vector
{
    return vector(x, y, z) * millimeter;
}

function axisLine(R is number) returns Line
{
    return line(mm3(0, 0, -R), vector(0, 1, 0));
}

/**
 * Plane containing the C-curve axis at angle theta.  Sketch x = radius r,
 * sketch y = -Y (the normal is the tangent direction).
 */
function radialPlane(R is number, theta is number) returns Plane
{
    return plane(mm3(0, 0, -R),
                 vector(cos(theta * radian), 0, -sin(theta * radian)),
                 vector(sin(theta * radian), 0, cos(theta * radian)));
}

/** Remove the sketch's surface bodies.  delId must not be a child of sketchId. */
function deleteSketchBodies(context is Context, delId is Id, sketchId is Id)
{
    opDeleteBodies(context, delId, { "entities" : qCreatedBy(sketchId, EntityType.BODY) });
}

/** Closed polygon of (r, y) points in the radial plane at th0, revolved by sweep radians. */
function revolvePoly(context is Context, id is Id, R is number, th0 is number, sweep is number, pts is array)
{
    var sk = newSketchOnPlane(context, id + "sk", { "sketchPlane" : radialPlane(R, th0) });
    var n = size(pts);
    for (var i = 0; i < n; i += 1)
    {
        var a = pts[i];
        var b = pts[(i + 1) % n];
        skLineSegment(sk, "l" ~ i, { "start" : vector(a[0], -a[1]) * millimeter,
                                     "end" : vector(b[0], -b[1]) * millimeter });
    }
    skSolve(sk);
    opRevolve(context, id + "rev", { "entities" : qSketchRegion(id + "sk"),
                                      "axis" : axisLine(R),
                                      "angleForward" : sweep * radian });
    deleteSketchBodies(context, id + "skDel", id + "sk");
}

/** Piece of a cylindrical shell: radius r0..r1, angle th0..th1, Y y0..y1. */
function ringSector(context is Context, id is Id, R is number, r0 is number, r1 is number,
                    th0 is number, th1 is number, y0 is number, y1 is number)
{
    revolvePoly(context, id, R, th0, th1 - th0, [[r0, y0], [r1, y0], [r1, y1], [r0, y1]]);
}

/** The same sector on the +X and -X side. */
function ringSectorPair(context is Context, id is Id, R is number, r0 is number, r1 is number,
                        th0 is number, th1 is number, y0 is number, y1 is number)
{
    ringSector(context, id + "p", R, r0, r1, th0, th1, y0, y1);
    ringSector(context, id + "m", R, r0, r1, -th1, -th0, y0, y1);
}

/** Axis-aligned box: x0..x1, y0..y1, from z = zTop downwards by depth. */
function boxDown(context is Context, id is Id, x0 is number, x1 is number, y0 is number, y1 is number,
                 zTop is number, depth is number)
{
    var sk = newSketchOnPlane(context, id + "sk", { "sketchPlane" : plane(mm3(0, 0, zTop), vector(0, 0, 1), vector(1, 0, 0)) });
    skRectangle(sk, "r", { "firstCorner" : vector(x0, y0) * millimeter, "secondCorner" : vector(x1, y1) * millimeter });
    skSolve(sk);
    opExtrude(context, id + "ext", { "entities" : qSketchRegion(id + "sk"),
                                      "direction" : vector(0, 0, -1),
                                      "endBound" : BoundingType.BLIND,
                                      "endDepth" : depth * millimeter });
    deleteSketchBodies(context, id + "skDel", id + "sk");
}

// ============================================================================
// Component A - stencil
// ============================================================================

function buildStencil(context is Context, id is Id, d is map, P is map, withTongues is boolean)
{
    var R = d.R;
    var shellId = id + "shell";

    // ---- Arch shell: tapered wall, revolved about the C-curve axis ---------
    // Wall = wallBody at the rear, tapering linearly to wallEdge, which is
    // reached 0.8 mm before the smile apex and kept from there on, so the whole
    // smile edge sees the thin wall.  Ending the taper short of the apex also
    // keeps the cone/cylinder seam off the smile edge, which makes the bevel
    // chamfer fail on a tangent edge.
    var yTe = d.yApex - 0.8;
    var yTs = 0.4 * yTe;
    revolvePoly(context, shellId, R, -d.phiShell, 2 * d.phiShell, [
        [d.rIn, 0], [d.rIn, d.L],
        [d.rIn + P.wallEdge, d.L], [d.rIn + P.wallEdge, yTe],
        [d.rIn + P.wallBody, yTs], [d.rIn + P.wallBody, 0]]);
    var shellQ = qCreatedBy(shellId, EntityType.BODY);

    // ---- Smile-line window: sketch in plan view, cut straight through ------
    cutWindow(context, id + "win", d, P);
    opBoolean(context, id + "winBool", { "tools" : qCreatedBy(id + "win", EntityType.BODY),
                                          "targets" : shellQ,
                                          "operationType" : BooleanOperationType.SUBTRACTION });

    // ---- Bevel: chamfer the outer edge of the window ------------------------
    bevelWindow(context, id + "bevel", id + "win", shellQ, d, P);

    // ---- Side clips (lateral tabs): arm down the sidewall + inward hook -----
    var t = 0.7;
    var th0 = d.phiShell - 0.5 / d.rIn;
    var th1 = d.phiShell + P.clipDrop / d.rIn;
    var thHook = th1 - 0.8 / d.rIn;
    var ys = [0.30 * d.L, 0.70 * d.L];
    for (var i = 0; i < 2; i += 1)
    {
        var y0 = ys[i] - P.clipWidth / 2;
        var y1 = ys[i] + P.clipWidth / 2;
        ringSectorPair(context, id + ("arm" ~ i), R, d.rIn, d.rIn + t, th0, th1, y0, y1);
        ringSectorPair(context, id + ("hook" ~ i), R, d.rIn - P.clipHook, d.rIn + t, thHook, th1, y0, y1);
    }

    // ---- Underslung registration tabs: stop block + ledge under the tip -----
    var thLo = d.phiNail - P.stopWidth / d.rIn;
    var rUnderTop = d.rIn - P.nailThickness - 0.1;
    var rUnderBot = rUnderTop - 0.8;
    ringSectorPair(context, id + "stop", R, rUnderBot, d.rOut, thLo, d.phiShell, d.L - 0.1, d.L + P.stopThickness);
    ringSectorPair(context, id + "ledge", R, rUnderBot, rUnderTop, thLo, d.phiShell, d.L - P.ledgeLength, d.L + P.stopThickness);

    // ---- Handle -------------------------------------------------------------
    buildHandle(context, id + "handle", d, P);

    // ---- Key tongues for the array plate -----------------------------------
    if (withTongues)
    {
        var xIn = d.rIn * sin(d.phiShell * radian);
        var zIn = -R + d.rIn * cos(d.phiShell * radian);
        var yt = tongueYs(d);
        for (var i = 0; i < 2; i += 1)
        {
            boxDown(context, id + ("tp" ~ i), xIn - 0.2, xIn + P.tongueLength, yt[i] - P.tongueWidth / 2,
                    yt[i] + P.tongueWidth / 2, zIn + 0.5, P.tongueThickness + 0.5);
            boxDown(context, id + ("tm" ~ i), -xIn - P.tongueLength, -xIn + 0.2, yt[i] - P.tongueWidth / 2,
                    yt[i] + P.tongueWidth / 2, zIn + 0.5, P.tongueThickness + 0.5);
        }
    }

    // ---- One body ------------------------------------------------------------
    opBoolean(context, id + "union", { "tools" : qCreatedBy(id, EntityType.BODY),
                                        "operationType" : BooleanOperationType.UNION });
}

function tongueYs(d is map) returns array
{
    return [0.25 * d.L, 0.75 * d.L];
}

/**
 * Window cutter: the region distal of the smile line, drawn in plan view
 * (x = chord position, y along the nail), then extruded symmetrically.
 * Classic and deep use skFitSpline through sampled points, V uses two lines,
 * straight uses a flat floor with two 0.5 mm corner arcs.
 */
function cutWindow(context is Context, id is Id, d is map, P is map)
{
    var sk = newSketchOnPlane(context, id + "sk", { "sketchPlane" : plane(mm3(0, 0, 0), vector(0, 0, 1), vector(1, 0, 0)) });
    var a = d.rIn * sin(d.phiNail * radian);      // half chord at the nail edge
    var yFar = d.L + 2;
    var yStart;       // smile height at x = -a
    var yEnd;         // smile height at x = +a

    if (P.style == TipStyle.STRAIGHT)
    {
        var rho = P.straightFillet;
        var y0 = d.yApex;
        skArc(sk, "cornerL", { "start" : vector(-a + rho, y0) * millimeter,
                               "mid" : vector(-a + rho - rho * cos(45 * degree), y0 + rho - rho * sin(45 * degree)) * millimeter,
                               "end" : vector(-a, y0 + rho) * millimeter });
        skArc(sk, "cornerR", { "start" : vector(a - rho, y0) * millimeter,
                               "mid" : vector(a - rho + rho * cos(45 * degree), y0 + rho - rho * sin(45 * degree)) * millimeter,
                               "end" : vector(a, y0 + rho) * millimeter });
        skLineSegment(sk, "floor", { "start" : vector(-a + rho, y0) * millimeter, "end" : vector(a - rho, y0) * millimeter });
        yStart = y0 + rho;
        yEnd = y0 + rho;
    }
    else if (P.style == TipStyle.VSHAPE)
    {
        yStart = d.yHi;
        yEnd = d.yHi;
        skLineSegment(sk, "rakeL", { "start" : vector(-a, d.yHi) * millimeter, "end" : vector(0, d.yApex) * millimeter });
        skLineSegment(sk, "rakeR", { "start" : vector(0, d.yApex) * millimeter, "end" : vector(a, d.yHi) * millimeter });
    }
    else
    {
        var us = smileSamples(P.samples - 1);
        var pts = [];
        for (var i = 0; i < size(us); i += 1)
        {
            var th = us[i] * d.phiNail;
            pts = append(pts, vector(d.rIn * sin(th * radian), d.yApex + smileRise(P.style, us[i], d.depth)) * millimeter);
        }
        skFitSpline(sk, "smile", { "points" : pts });
        yStart = d.yApex + d.depth;
        yEnd = yStart;
    }

    skLineSegment(sk, "wallL", { "start" : vector(-a, yStart) * millimeter, "end" : vector(-a, yFar) * millimeter });
    skLineSegment(sk, "wallR", { "start" : vector(a, yEnd) * millimeter, "end" : vector(a, yFar) * millimeter });
    skLineSegment(sk, "top", { "start" : vector(-a, yFar) * millimeter, "end" : vector(a, yFar) * millimeter });
    skSolve(sk);

    opExtrude(context, id + "ext", { "entities" : qSketchRegion(id + "sk"),
                                      "direction" : vector(0, 0, 1),
                                      "endBound" : BoundingType.THROUGH_ALL,
                                      "startBound" : BoundingType.THROUGH_ALL });
    deleteSketchBodies(context, id + "skDel", id + "sk");
}

/**
 * Bevel the outer edge of the window.  Along the smile edge the wall is
 * wallEdge thick, so the chamfer removes (wallEdge - lip) of the wall and
 * (wallEdge - lip) * tan(bevel) of the outer surface, leaving a `lip`-high
 * land at the nail surface.
 */
function bevelWindow(context is Context, id is Id, winId is Id, shellQ is Query, d is map, P is map)
{
    var depthAlongWall = P.wallEdge - P.lip;
    if (depthAlongWall < 0.01)
        return;

    // The cutter's faces are not tracked once the boolean consumes it, so the
    // window edge is found geometrically: edges of the shell that lie on the
    // outer surface (r = rIn + wallEdge), between the smile apex and the free
    // edge, and inside the nail-plate width (this excludes the rail edges, the
    // end-face arcs and the inner-surface edges).
    var a = d.rIn * sin(d.phiNail * radian);
    var candidates = evaluateQuery(context, qOwnedByBody(shellQ, EntityType.EDGE));
    var outer = [];
    for (var e in candidates)
    {
        var p = evEdgeTangentLine(context, { "edge" : e, "parameter" : 0.5 }).origin / millimeter;
        var r = sqrt(p[0] ^ 2 + (p[2] + d.R) ^ 2);
        if (abs(r - (d.rIn + P.wallEdge)) < 0.05 && p[1] > d.yApex - 0.01 && p[1] < d.L - 0.05 && abs(p[0]) < a + 0.05)
            outer = append(outer, e);
    }
    if (size(outer) == 0)
    {
        reportFeatureInfo(context, id, "Bevel skipped: could not find the window edge.");
        return;
    }

    var width = depthAlongWall * tan(P.bevel * radian);
    var chamferDef = { "entities" : qUnion(outer), "tangentPropagation" : true };
    if (abs(P.bevel * 180 / PI - 45) < 0.01)
    {
        chamferDef.chamferType = ChamferType.EQUAL_OFFSETS;
        chamferDef.width = depthAlongWall * millimeter;
    }
    else
    {
        chamferDef.chamferType = ChamferType.TWO_OFFSETS;
        chamferDef.width1 = depthAlongWall * millimeter;
        chamferDef.width2 = width * millimeter;
        chamferDef.oppositeDirection = P.flipBevel;
    }
    try
    {
        opChamfer(context, id, chamferDef);
    }
    catch
    {
        reportFeatureInfo(context, id, "Bevel chamfer failed; window left square.");
    }
}

/**
 * Rear pull tab, built directly in its pitched plane.  The sketch plane's
 * normal (0, sin p, cos p) is the tab's thickness direction and the sketch
 * y axis points forward (towards +Y), so the tab runs from y = -length to +1;
 * the extra 1 mm overlaps into the shell.
 */
function buildHandle(context is Context, id is Id, d is map, P is map)
{
    var p = P.handlePitch;
    var zIn = d.rIn - d.R;
    var sk = newSketchOnPlane(context, id + "sk", {
        "sketchPlane" : plane(mm3(0, 0, zIn), vector(0, sin(p * radian), cos(p * radian)), vector(1, 0, 0)) });
    skRectangle(sk, "r", { "firstCorner" : vector(-P.handleWidth / 2, -P.handleLength) * millimeter,
                           "secondCorner" : vector(P.handleWidth / 2, 1) * millimeter });
    skSolve(sk);
    opExtrude(context, id + "ext", { "entities" : qSketchRegion(id + "sk"),
                                      "direction" : vector(0, sin(p * radian), cos(p * radian)),
                                      "endBound" : BoundingType.BLIND,
                                      "endDepth" : P.handleThickness * millimeter });
    deleteSketchBodies(context, id + "skDel", id + "sk");
}

// ============================================================================
// Component B - 10-position array plate
// ============================================================================

function buildArrayPlate(context is Context, id is Id, P is map)
{
    var halves = [];
    var dims = [];
    for (var s = 0; s < 10; s += 1)
    {
        var d = nailDims(context, id + ("dims" ~ s), s, P);
        dims = append(dims, d);
        halves = append(halves, d.rIn * sin(d.phiShell * radian) + P.tongueLength + 0.5);
    }

    // Row pitch along X
    var xs = [];
    var cursor = P.plateMargin;
    for (var s = 0; s < 10; s += 1)
    {
        xs = append(xs, cursor + halves[s]);
        cursor += 2 * halves[s] + P.plateGap;
    }
    var plateW = cursor - P.plateGap + P.plateMargin;
    var maxL = 0;
    for (var s = 0; s < 10; s += 1)
        maxL = max(maxL, dims[s].L);
    var yMin = -P.plateMargin;
    var yMax = maxL + P.stopThickness + 2 + P.plateMargin;

    // Plate: top face at z = 0 (the stencil seat)
    var skp = newSketchOnPlane(context, id + "plateSk", { "sketchPlane" : plane(mm3(0, 0, 0), vector(0, 0, 1), vector(1, 0, 0)) });
    skRectangle(skp, "r", { "firstCorner" : vector(0, yMin) * millimeter, "secondCorner" : vector(plateW, yMax) * millimeter });
    skSolve(skp);
    opExtrude(context, id + "plate", { "entities" : qSketchRegion(id + "plateSk"),
                                        "direction" : vector(0, 0, -1),
                                        "endBound" : BoundingType.BLIND,
                                        "endDepth" : P.plateThickness * millimeter });
    deleteSketchBodies(context, id + "plateSkDel", id + "plateSk");
    var plateQ = qCreatedBy(id + "plate", EntityType.BODY);

    for (var s = 0; s < 10; s += 1)
    {
        var d = dims[s];
        var sid = id + ("pos" ~ s);
        var zIn = -d.R + d.rIn * cos(d.phiShell * radian);
        var xIn = d.rIn * sin(d.phiShell * radian);
        var move = transform(mm3(xs[s], 0, -zIn));

        // Stencil (with key tongues)
        buildStencil(context, sid + "st", d, P, true);
        opTransform(context, sid + "mvSt", { "bodies" : qCreatedBy(sid + "st", EntityType.BODY), "transform" : move });

        // Negative: pocket under the arch + press-fit tongue slots
        var xPocket = xIn - 0.3;
        boxDown(context, sid + "neg" + "pocket", -xPocket, xPocket, -0.5, d.L + P.stopThickness + 1,
                zIn, P.pocketDepth);
        var g = P.slotGap;
        var yt = tongueYs(d);
        for (var i = 0; i < 2; i += 1)
        {
            boxDown(context, sid + ("neg" ~ "sp" ~ i), xIn - 0.2, xIn + P.tongueLength + 2 * g + 0.2,
                    yt[i] - P.tongueWidth / 2 - g, yt[i] + P.tongueWidth / 2 + g,
                    zIn, P.tongueThickness + g + 0.2);
            boxDown(context, sid + ("neg" ~ "sm" ~ i), -xIn - P.tongueLength - 2 * g - 0.2, -xIn + 0.2,
                    yt[i] - P.tongueWidth / 2 - g, yt[i] + P.tongueWidth / 2 + g,
                    zIn, P.tongueThickness + g + 0.2);
        }
        var negQ = qCreatedBy(sid + "neg", EntityType.BODY);
        opTransform(context, sid + "mvNeg", { "bodies" : negQ, "transform" : move });
        opBoolean(context, sid + "cut", { "tools" : negQ, "targets" : plateQ,
                                           "operationType" : BooleanOperationType.SUBTRACTION });
    }
}
