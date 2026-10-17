FeatureScript 2144;
import(path : "onshape/std/common.fs", version : "2144.0");
import(path : "onshape/std/geometry.fs", version : "2144.0");
import(path : "onshape/std/sketch.fs", version : "2144.0");
import(path : "onshape/std/primitives.fs", version : "2144.0");
import(path : "onshape/std/boolean.fs", version : "2144.0");
import(path : "onshape/std/valueBounds.fs", version : "2144.0");

/**
 * Modular pegboard display for craft-market booths.
 *
 * Coordinate frame (all geometry is built in world coordinates):
 *   X = board width (centred on 0), Y = board height (bottom edge at 0),
 *   Z = board thickness (front face at Z = Thickness, back at Z = 0).
 *
 * Board shapes: Gothic arch, framed header plate, geometric hex cluster,
 * or a custom logo silhouette drawn by the user on the Top plane.
 *
 * The board is split into tiles that fit the printer bed. Seams run between
 * hole columns/rows (so no peg hole is ever cut in half) and are joined with
 * sliding dovetails (assembled by sliding along Z) with crush ribs for a
 * friction/snap fit. Everything is produced as separate solids.
 */

const BOARD_H_BOUNDS = { (meter) : [0.1, 0.55, 1.5], (centimeter) : 55.0, (millimeter) : 550.0, (inch) : 21.653543, (foot) : 1.804462, (yard) : 0.601487 } as LengthBoundSpec;
const BOARD_W_BOUNDS = { (meter) : [0.1, 0.4, 1.5], (centimeter) : 40.0, (millimeter) : 400.0, (inch) : 15.748031, (foot) : 1.312336, (yard) : 0.437445 } as LengthBoundSpec;
const PEG_BOUNDS = { (meter) : [0.01, 0.0254, 0.08], (centimeter) : 2.54, (millimeter) : 25.4, (inch) : 1.0, (foot) : 0.083333, (yard) : 0.027778 } as LengthBoundSpec;
const HOLE_BOUNDS = { (meter) : [0.003, 0.00653, 0.015], (centimeter) : 0.653, (millimeter) : 6.53, (inch) : 0.257087, (foot) : 0.021424, (yard) : 0.007141 } as LengthBoundSpec;
const THK_BOUNDS = { (meter) : [0.003, 0.00476, 0.012], (centimeter) : 0.476, (millimeter) : 4.76, (inch) : 0.187402, (foot) : 0.015617, (yard) : 0.005206 } as LengthBoundSpec;
const ARCHR_BOUNDS = { (meter) : [0.05, 0.4, 2.0], (centimeter) : 40.0, (millimeter) : 400.0, (inch) : 15.748031, (foot) : 1.312336, (yard) : 0.437445 } as LengthBoundSpec;
const EDGE_BOUNDS = { (meter) : [0.01905, 0.01905, 0.1], (centimeter) : 1.905, (millimeter) : 19.05, (inch) : 0.75, (foot) : 0.0625, (yard) : 0.020833 } as LengthBoundSpec;
const TOL_BOUNDS = { (meter) : [5e-05, 0.000175, 0.0005], (centimeter) : 0.0175, (millimeter) : 0.175, (inch) : 0.00689, (foot) : 0.000574, (yard) : 0.000191 } as LengthBoundSpec;
const BED_BOUNDS = { (meter) : [0.1, 0.256, 0.5], (centimeter) : 25.6, (millimeter) : 256.0, (inch) : 10.07874, (foot) : 0.839895, (yard) : 0.279965 } as LengthBoundSpec;
const DTLEN_BOUNDS = { (meter) : [0.004, 0.008, 0.02], (centimeter) : 0.8, (millimeter) : 8.0, (inch) : 0.314961, (foot) : 0.026247, (yard) : 0.008749 } as LengthBoundSpec;
const DTWID_BOUNDS = { (meter) : [0.006, 0.01, 0.03], (centimeter) : 1.0, (millimeter) : 10.0, (inch) : 0.393701, (foot) : 0.032808, (yard) : 0.010936 } as LengthBoundSpec;
const FEETW_BOUNDS = { (meter) : [0.02, 0.05, 0.15], (centimeter) : 5.0, (millimeter) : 50.0, (inch) : 1.968504, (foot) : 0.164042, (yard) : 0.054681 } as LengthBoundSpec;
const FEETD_BOUNDS = { (meter) : [0.04, 0.09, 0.25], (centimeter) : 9.0, (millimeter) : 90.0, (inch) : 3.543307, (foot) : 0.295276, (yard) : 0.098425 } as LengthBoundSpec;
const FEETH_BOUNDS = { (meter) : [0.01, 0.022, 0.08], (centimeter) : 2.2, (millimeter) : 22.0, (inch) : 0.866142, (foot) : 0.072178, (yard) : 0.024059 } as LengthBoundSpec;
const SLOTD_BOUNDS = { (meter) : [0.005, 0.015, 0.05], (centimeter) : 1.5, (millimeter) : 15.0, (inch) : 0.590551, (foot) : 0.049213, (yard) : 0.016404 } as LengthBoundSpec;
const FEETSP_BOUNDS = { (meter) : [0.02, 0.2, 1.0], (centimeter) : 20.0, (millimeter) : 200.0, (inch) : 7.874016, (foot) : 0.656168, (yard) : 0.218723 } as LengthBoundSpec;
const EYEHOLE_BOUNDS = { (meter) : [0.003, 0.009525, 0.03], (centimeter) : 0.9525, (millimeter) : 9.525, (inch) : 0.375, (foot) : 0.03125, (yard) : 0.010417 } as LengthBoundSpec;
const EYEWALL_BOUNDS = { (meter) : [0.002, 0.004, 0.012], (centimeter) : 0.4, (millimeter) : 4.0, (inch) : 0.15748, (foot) : 0.013123, (yard) : 0.004374 } as LengthBoundSpec;
const EYESP_BOUNDS = { (meter) : [0.02, 0.2, 1.0], (centimeter) : 20.0, (millimeter) : 200.0, (inch) : 7.874016, (foot) : 0.656168, (yard) : 0.218723 } as LengthBoundSpec;
const HEADERB_BOUNDS = { (meter) : [0.01, 0.06, 0.3], (centimeter) : 6.0, (millimeter) : 60.0, (inch) : 2.362205, (foot) : 0.19685, (yard) : 0.065617 } as LengthBoundSpec;
const CHAMF_BOUNDS = { (meter) : [0.001, 0.012, 0.05], (centimeter) : 1.2, (millimeter) : 12.0, (inch) : 0.472441, (foot) : 0.03937, (yard) : 0.013123 } as LengthBoundSpec;
const RECESSD_BOUNDS = { (meter) : [0.0002, 0.0012, 0.003], (centimeter) : 0.12, (millimeter) : 1.2, (inch) : 0.047244, (foot) : 0.003937, (yard) : 0.001312 } as LengthBoundSpec;
const HOLECOMP_BOUNDS = { (meter) : [0.0, 0.0001, 0.0005], (centimeter) : 0.01, (millimeter) : 0.1, (inch) : 0.003937, (foot) : 0.000328, (yard) : 0.000109 } as LengthBoundSpec;
const CRUSH_BOUNDS = { (meter) : [0.0, 0.0001, 0.0004], (centimeter) : 0.01, (millimeter) : 0.1, (inch) : 0.003937, (foot) : 0.000328, (yard) : 0.000109 } as LengthBoundSpec;

const ANGLE_BOUNDS = { (degree) : [5, 15, 30], (radian) : 0.2618 } as AngleBoundSpec;
const EVERY_BOUNDS = { (unitless) : [1, 4, 20] } as IntegerBoundSpec;

const PI = 3.14159265358979;

export enum PegboardShape
{
    annotation { "Name" : "Gothic arch (cathedral)" }
    GOTHIC_ARCH,
    annotation { "Name" : "Framed header plate" }
    HEADER_PLATE,
    annotation { "Name" : "Hex cluster (7 hexagons)" }
    HEX_CLUSTER,
    annotation { "Name" : "Custom logo silhouette (sketch)" }
    CUSTOM_SILHOUETTE
}

annotation { "Feature Type Name" : "Pegboard display", "Feature Type Description" : "Modular, printable pegboard display with dovetail joints, hanging eyelets and push-fit feet" }
export const pegboardDisplay = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Board shape" }
        definition.BoardShape is PegboardShape;

        annotation { "Name" : "Board height (BoardHeight)" }
        isLength(definition.BoardHeight, BOARD_H_BOUNDS);

        annotation { "Name" : "Board width (BoardWidth)" }
        isLength(definition.BoardWidth, BOARD_W_BOUNDS);

        annotation { "Name" : "Thickness" }
        isLength(definition.Thickness, THK_BOUNDS);

        if (definition.BoardShape == PegboardShape.GOTHIC_ARCH)
        {
            annotation { "Name" : "Arch radius (ArchRadius, >= width / 2)" }
            isLength(definition.ArchRadius, ARCHR_BOUNDS);
        }

        if (definition.BoardShape == PegboardShape.HEADER_PLATE)
        {
            annotation { "Name" : "Corner chamfer" }
            isLength(definition.CornerChamfer, CHAMF_BOUNDS);
        }

        if (definition.BoardShape == PegboardShape.HEADER_PLATE || definition.BoardShape == PegboardShape.CUSTOM_SILHOUETTE)
        {
            annotation { "Name" : "Header band height (no holes)" }
            isLength(definition.HeaderBand, HEADERB_BOUNDS);
        }

        if (definition.BoardShape == PegboardShape.HEADER_PLATE)
        {
            annotation { "Name" : "Logo recess depth" }
            isLength(definition.RecessDepth, RECESSD_BOUNDS);
        }

        if (definition.BoardShape == PegboardShape.CUSTOM_SILHOUETTE)
        {
            annotation { "Name" : "Logo sketch region(s)", "Filter" : EntityType.FACE && GeometryType.PLANE, "MaxNumberOfPicks" : 8 }
            definition.LogoRegions is Query;
        }

        annotation { "Group Name" : "Pegboard grid", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Hole spacing (PegSpacing)" }
            isLength(definition.PegSpacing, PEG_BOUNDS);

            annotation { "Name" : "Hole diameter (HoleDiameter)" }
            isLength(definition.HoleDiameter, HOLE_BOUNDS);

            annotation { "Name" : "Hole print compensation (added to diameter)" }
            isLength(definition.HoleComp, HOLECOMP_BOUNDS);

            annotation { "Name" : "Edge clearance (hole centre to outline)" }
            isLength(definition.EdgeClearance, EDGE_BOUNDS);
        }

        annotation { "Group Name" : "Printing & joints", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Printer bed X" }
            isLength(definition.BedX, BED_BOUNDS);

            annotation { "Name" : "Printer bed Y" }
            isLength(definition.BedY, BED_BOUNDS);

            annotation { "Name" : "Fit tolerance (gap per joint face)" }
            isLength(definition.Tolerance, TOL_BOUNDS);

            annotation { "Name" : "Dovetail length" }
            isLength(definition.DovetailLength, DTLEN_BOUNDS);

            annotation { "Name" : "Dovetail neck width" }
            isLength(definition.DovetailWidth, DTWID_BOUNDS);

            annotation { "Name" : "Dovetail flank angle" }
            isAngle(definition.DovetailAngle, ANGLE_BOUNDS);

            annotation { "Name" : "Dovetail every N pegs along seam" }
            isInteger(definition.DovetailEvery, EVERY_BOUNDS);

            annotation { "Name" : "Crush-rib interference" }
            isLength(definition.CrushRib, CRUSH_BOUNDS);
        }

        annotation { "Group Name" : "Hanging eyelets", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Add two eyelets", "Default" : true }
            definition.AddEyelets is boolean;

            if (definition.AddEyelets)
            {
                annotation { "Name" : "Eyelet hole diameter (0.375 in)" }
                isLength(definition.EyeletHole, EYEHOLE_BOUNDS);

                annotation { "Name" : "Eyelet wall" }
                isLength(definition.EyeletWall, EYEWALL_BOUNDS);

                annotation { "Name" : "Eyelet centre-to-centre spacing" }
                isLength(definition.EyeletSpacing, EYESP_BOUNDS);
            }
        }

        annotation { "Group Name" : "Detachable feet", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Add two feet", "Default" : true }
            definition.AddFeet is boolean;

            if (definition.AddFeet)
            {
                annotation { "Name" : "Foot width (X)" }
                isLength(definition.FootWidth, FEETW_BOUNDS);

                annotation { "Name" : "Foot depth (Z)" }
                isLength(definition.FootDepth, FEETD_BOUNDS);

                annotation { "Name" : "Foot total height" }
                isLength(definition.FootHeight, FEETH_BOUNDS);

                annotation { "Name" : "Slot depth" }
                isLength(definition.SlotDepth, SLOTD_BOUNDS);

                annotation { "Name" : "Foot centre-to-centre spacing" }
                isLength(definition.FootSpacing, FEETSP_BOUNDS);
            }
        }
    }
    {
        // ---------- numbers in millimetres ----------
        const W = definition.BoardWidth / millimeter;
        const H = definition.BoardHeight / millimeter;
        const T = definition.Thickness / millimeter;
        const P = definition.PegSpacing / millimeter;
        const hr = (definition.HoleDiameter + definition.HoleComp) / millimeter / 2;
        const E = definition.EdgeClearance / millimeter;
        const tol = definition.Tolerance / millimeter;
        const bedX = definition.BedX / millimeter;
        const bedY = definition.BedY / millimeter;
        const DT = definition.DovetailLength / millimeter;
        const wn = definition.DovetailWidth / millimeter / 2;
        const alpha = definition.DovetailAngle / radian;
        const every = definition.DovetailEvery;
        const crush = definition.CrushRib / millimeter;
        const shape = definition.BoardShape;

        // ---------- shape description ----------
        var sp = { "W" : W, "H" : H };
        if (shape == PegboardShape.GOTHIC_ARCH)
        {
            const R = definition.ArchRadius / millimeter;
            if (R < W / 2)
                throw regenError("Arch radius must be at least half the board width.", ["ArchRadius"]);
            const d = W / 2 - R;
            const rise = sqrt(R * R - d * d);
            if (rise > H - 2 * E)
                throw regenError("Board height is too small for this arch radius and width.", ["BoardHeight", "ArchRadius"]);
            sp.kind = "arch";
            sp.R = R;
            sp.d = d;
            sp.rise = rise;
            sp.s = H - rise;
        }
        else if (shape == PegboardShape.HEADER_PLATE)
        {
            sp.kind = "plate";
            sp.c = definition.CornerChamfer / millimeter;
            sp.band = definition.HeaderBand / millimeter;
        }
        else if (shape == PegboardShape.HEX_CLUSTER)
        {
            // flat-top hexagons, centre hex plus six neighbours: 5r wide, 3*sqrt(3)r tall
            const r = min(W / 5, H / (3 * sqrt(3)));
            sp.kind = "hex";
            sp.r = r;
            sp.H = 3 * sqrt(3) * r;
            sp.cy = 1.5 * sqrt(3) * r;
            sp.W = 5 * r;
        }
        else
        {
            sp.kind = "rect";
            sp.band = definition.HeaderBand / millimeter;
        }
        const Hs = sp.H;
        const Ws = sp.W;

        // ---------- tile grid (seams fall midway between hole rows/columns) ----------
        const mX = max(1, floor((bedX - DT - 2) / P));
        const earExtra = definition.AddEyelets ? (definition.EyeletHole / millimeter + 2 * definition.EyeletWall / millimeter + 4) : 0;
        const mY = max(1, floor((bedY - DT - 2 - earExtra) / P));
        const nCols = max(1, ceil(Ws / (mX * P) - 0.000001));
        const totalW = nCols * mX * P;
        const x0 = -totalW / 2;
        const y0 = E - P / 2;
        const nRows = max(1, ceil((Hs - y0) / (mY * P) - 0.000001));
        const nColCells = nCols * mX;
        const nRowCells = nRows * mY;

        // ---------- 1. board solid ----------
        const topPlane = plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1), vector(1, 0, 0));
        if (sp.kind == "arch")
        {
            const sk = newSketchOnPlane(context, id + "boardSk", { "sketchPlane" : topPlane });
            const hw = W / 2;
            const thetaMid = atan2(sp.rise, -sp.d) / radian / 2;
            const mx = sp.d + sp.R * cos(thetaMid * radian);
            const my = sp.s + sp.R * sin(thetaMid * radian);
            skLineSegment(sk, "bottom", { "start" : vector(-hw, 0) * millimeter, "end" : vector(hw, 0) * millimeter });
            skLineSegment(sk, "right", { "start" : vector(hw, 0) * millimeter, "end" : vector(hw, sp.s) * millimeter });
            skArc(sk, "arcR", { "start" : vector(hw, sp.s) * millimeter, "mid" : vector(mx, my) * millimeter, "end" : vector(0, H) * millimeter });
            skArc(sk, "arcL", { "start" : vector(0, H) * millimeter, "mid" : vector(-mx, my) * millimeter, "end" : vector(-hw, sp.s) * millimeter });
            skLineSegment(sk, "left", { "start" : vector(-hw, sp.s) * millimeter, "end" : vector(-hw, 0) * millimeter });
            skSolve(sk);
            extrudeUp(context, id + "board", qSketchRegion(id + "boardSk"), T);
        }
        else if (sp.kind == "plate")
        {
            const sk = newSketchOnPlane(context, id + "boardSk", { "sketchPlane" : topPlane });
            const hw = W / 2;
            const c = sp.c;
            polyLoop(sk, "p", [[-hw + c, 0], [hw - c, 0], [hw, c], [hw, H - c], [hw - c, H], [-hw + c, H], [-hw, H - c], [-hw, c]]);
            skSolve(sk);
            extrudeUp(context, id + "board", qSketchRegion(id + "boardSk"), T);
        }
        else if (sp.kind == "hex")
        {
            const centers = hexCenters(sp);
            for (var i = 0; i < size(centers); i += 1)
            {
                const sk = newSketchOnPlane(context, id + ("hexSk" ~ i), { "sketchPlane" : topPlane });
                var pts = [];
                for (var k = 0; k < 6; k += 1)
                    pts = append(pts, [centers[i][0] + sp.r * cos(60 * k * degree), centers[i][1] + sp.r * sin(60 * k * degree)]);
                polyLoop(sk, "h", pts);
                skSolve(sk);
                extrudeUp(context, id + ("hex" ~ i), qSketchRegion(id + ("hexSk" ~ i)), T);
            }
            opBoolean(context, id + "hexUnion", { "tools" : qCreatedBy(id, EntityType.BODY), "operation" : BooleanOperationType.UNION });
        }
        else
        {
            if (size(evaluateQuery(context, definition.LogoRegions)) == 0)
                throw regenError("Select the closed sketch region(s) of your logo silhouette (sketch on the Top plane).", ["LogoRegions"]);
            opExtrude(context, id + "board", {
                        "entities" : definition.LogoRegions,
                        "direction" : vector(0, 0, 1),
                        "endBound" : BoundingType.BLIND,
                        "depth" : T * millimeter
                    });
        }

        // ---------- 2. hanging eyelets ----------
        var cutters = [];
        if (definition.AddEyelets)
        {
            const eyeHole = definition.EyeletHole / millimeter / 2;
            const wall = definition.EyeletWall / millimeter;
            const Ro = eyeHole + wall;
            for (var side = 0; side < 2; side += 1)
            {
                const ex = (side == 0 ? -1 : 1) * definition.EyeletSpacing / millimeter / 2;
                var yMax = -1;
                var yMin = 1e9;
                for (var i = 0; i <= 8; i += 1)
                {
                    const ty = topY(sp, ex - Ro + 2 * Ro * i / 8);
                    if (ty < 0)
                        throw regenError("Eyelet position is outside the board outline; reduce the eyelet spacing.", ["EyeletSpacing"]);
                    yMax = max(yMax, ty);
                    yMin = min(yMin, ty);
                }
                const cy = yMax + eyeHole + wall;
                const earId = id + "ear" + ("e" ~ side);
                fCuboid(context, earId + "box", {
                            "corner1" : vector(ex - Ro, yMin - 4, 0) * millimeter,
                            "corner2" : vector(ex + Ro, cy, T) * millimeter
                        });
                fCylinder(context, earId + "round", {
                            "topCenter" : vector(ex, cy, T) * millimeter,
                            "bottomCenter" : vector(ex, cy, 0) * millimeter,
                            "radius" : Ro * millimeter
                        });
            }
            opBoolean(context, id + "earUnion", { "tools" : qCreatedBy(id, EntityType.BODY), "operation" : BooleanOperationType.UNION });
            for (var side = 0; side < 2; side += 1)
            {
                const ex = (side == 0 ? -1 : 1) * definition.EyeletSpacing / millimeter / 2;
                var yMax = -1;
                for (var i = 0; i <= 8; i += 1)
                    yMax = max(yMax, topY(sp, ex - Ro + 2 * Ro * i / 8));
                const cy = yMax + eyeHole + wall;
                fCylinder(context, id + "cut" + ("eye" ~ side), {
                            "topCenter" : vector(ex, cy, T + 1) * millimeter,
                            "bottomCenter" : vector(ex, cy, -1) * millimeter,
                            "radius" : eyeHole * millimeter
                        });
            }
        }

        // ---------- 3. peg holes (+ logo recess) ----------
        var holePts = [];
        for (var i = 0; i < nColCells; i += 1)
        {
            for (var j = 0; j < nRowCells; j += 1)
            {
                const hx = x0 + (i + 0.5) * P;
                const hy = y0 + (j + 0.5) * P;
                if (!okInset(sp, hx, hy, E))
                    continue;
                if ((sp.kind == "plate" || sp.kind == "rect") && hy > Hs - sp.band - hr - 6)
                    continue;
                holePts = append(holePts, [hx, hy]);
            }
        }
        if (size(holePts) > 0)
        {
            const hsk = newSketchOnPlane(context, id + "holeSk", { "sketchPlane" : plane(vector(0, 0, -1) * millimeter, vector(0, 0, 1), vector(1, 0, 0)) });
            for (var n = 0; n < size(holePts); n += 1)
                skCircle(hsk, "h" ~ n, { "center" : vector(holePts[n][0], holePts[n][1]) * millimeter, "radius" : hr * millimeter });
            skSolve(hsk);
            extrudeUp(context, id + "cut" + "holes", qSketchRegion(id + "holeSk"), T + 2);
        }
        if (sp.kind == "plate")
        {
            const inset = max(sp.c, 8) + 4;
            fCuboid(context, id + "cut" + "recess", {
                        "corner1" : vector(-W / 2 + inset, H - sp.band + 4, T - definition.RecessDepth / millimeter) * millimeter,
                        "corner2" : vector(W / 2 - inset, H - 4, T + 1) * millimeter
                    });
        }
        if (size(evaluateQuery(context, qCreatedBy(id + "cut", EntityType.BODY))) > 0)
        {
            opBoolean(context, id + "cutBoolean", {
                        "targets" : qSubtraction(qCreatedBy(id, EntityType.BODY), qCreatedBy(id + "cut", EntityType.BODY)),
                        "tools" : qCreatedBy(id + "cut", EntityType.BODY),
                        "operation" : BooleanOperationType.SUBTRACTION
                    });
        }

        // ---------- 4. split into tiles with kerfs + dovetails ----------
        var seamsX = [];
        for (var j = 1; j < nCols; j += 1)
            seamsX = append(seamsX, x0 + j * mX * P);
        var seamsY = [];
        for (var j = 1; j < nRows; j += 1)
            seamsY = append(seamsY, y0 + j * mY * P);

        if (size(seamsX) + size(seamsY) > 0)
        {
            const g = tol;                       // seam gap
            const wt = wn + (DT - g / 2) * tan(alpha * radian);
            const cp = tol / cos(alpha * radian); // horizontal clearance on the slanted flank
            const big = 2000;

            // dovetail positions
            var dts = [];
            for (var s = 0; s < size(seamsX); s += 1)
            {
                for (var k = 1; k < nRowCells; k += 1)
                {
                    if (k % mY == 0 || k % every != floor((every - 1) / 2))
                        continue;
                    const dt = { "vertical" : true, "seam" : seamsX[s], "c" : y0 + k * P };
                    if (dovetailFits(sp, dt, g, DT, wn, wt, cp, tol))
                        dts = append(dts, dt);
                }
            }
            for (var s = 0; s < size(seamsY); s += 1)
            {
                for (var k = 1; k < nColCells; k += 1)
                {
                    if (k % mX == 0 || k % every != floor((every - 1) / 2))
                        continue;
                    const dt = { "vertical" : false, "seam" : seamsY[s], "c" : x0 + k * P };
                    if (dovetailFits(sp, dt, g, DT, wn, wt, cp, tol))
                        dts = append(dts, dt);
                }
            }

            // kerf strips
            for (var s = 0; s < size(seamsX); s += 1)
                fCuboid(context, id + "kerf" + ("vs" ~ s), {
                            "corner1" : vector(seamsX[s] - g / 2, -big, -1) * millimeter,
                            "corner2" : vector(seamsX[s] + g / 2, big, T + 1) * millimeter
                        });
            for (var s = 0; s < size(seamsY); s += 1)
                fCuboid(context, id + "kerf" + ("hs" ~ s), {
                            "corner1" : vector(-big, seamsY[s] - g / 2, -1) * millimeter,
                            "corner2" : vector(big, seamsY[s] + g / 2, T + 1) * millimeter
                        });

            if (size(dts) > 0)
            {
                const sockPlane = plane(vector(0, 0, -1) * millimeter, vector(0, 0, 1), vector(1, 0, 0));
                const sockSk = newSketchOnPlane(context, id + "sockSk", { "sketchPlane" : sockPlane });
                const tailSk = newSketchOnPlane(context, id + "tailSk", { "sketchPlane" : sockPlane });
                const ribSk = newSketchOnPlane(context, id + "ribSk", { "sketchPlane" : sockPlane });
                const ribR = 0.8;
                const ribProtrude = tol + crush;
                for (var n = 0; n < size(dts); n += 1)
                {
                    const dt = dts[n];
                    polyLoop(sockSk, "s" ~ n, mapAll(dt, [[-wn - cp, -g / 2], [wn + cp, -g / 2], [wn + cp, g / 2], [wt + cp, DT + tol], [-wt - cp, DT + tol], [-wn - cp, g / 2]]));
                    polyLoop(tailSk, "t" ~ n, mapAll(dt, [[-wn, -g / 2 - 1], [wn, -g / 2 - 1], [wn, g / 2], [wt, DT], [-wt, DT], [-wn, g / 2]]));
                    // crush ribs on both flanks (centre inside the tail, protruding past the clearance)
                    const mu = (wn + wt) / 2;
                    const mv = (g / 2 + DT) / 2;
                    const off = ribProtrude - ribR;
                    const rc = [mu + cos(alpha * radian) * off, mv - sin(alpha * radian) * off];
                    for (var sg = 0; sg < 2; sg += 1)
                    {
                        const ctr = dtMap(dt, (sg == 0 ? 1 : -1) * rc[0], rc[1]);
                        skCircle(ribSk, "r" ~ n ~ "_" ~ sg, { "center" : vector(ctr[0], ctr[1]) * millimeter, "radius" : ribR * millimeter });
                    }
                }
                skSolve(sockSk);
                skSolve(tailSk);
                skSolve(ribSk);
                extrudeUp(context, id + "kerf" + "sockets", qSketchRegion(id + "sockSk"), T + 2);
                extrudeUp(context, id + "keep" + "tails", qSketchRegion(id + "tailSk"), T + 2);
                extrudeUp(context, id + "keep" + "ribs", qSketchRegion(id + "ribSk"), T + 2);
                // the dovetail tail (and ribs) are not part of the kerf
                opBoolean(context, id + "kerfTrim", {
                            "targets" : qCreatedBy(id + "kerf", EntityType.BODY),
                            "tools" : qCreatedBy(id + "keep", EntityType.BODY),
                            "operation" : BooleanOperationType.SUBTRACTION
                        });
            }
            opBoolean(context, id + "splitBoolean", {
                        "targets" : qSubtraction(qCreatedBy(id, EntityType.BODY), qCreatedBy(id + "kerf", EntityType.BODY)),
                        "tools" : qCreatedBy(id + "kerf", EntityType.BODY),
                        "operation" : BooleanOperationType.SUBTRACTION
                    });

            // warn if an eyelet straddles a seam
            if (definition.AddEyelets)
            {
                const er = definition.EyeletHole / millimeter / 2 + definition.EyeletWall / millimeter;
                for (var side = 0; side < 2; side += 1)
                {
                    const ex = (side == 0 ? -1 : 1) * definition.EyeletSpacing / millimeter / 2;
                    for (var s = 0; s < size(seamsX); s += 1)
                        if (abs(seamsX[s] - ex) < er + 1)
                            reportFeatureInfo(context, id, "An eyelet sits on a tile seam. Change the eyelet spacing so it lands inside one tile.");
                }
            }
            reportFeatureInfo(context, id, (nCols * nRows) ~ " tiles (" ~ nCols ~ " x " ~ nRows ~ "), " ~ size(dts) ~ " dovetails. Largest tile footprint about "
                    ~ roundTo(mX * P + DT, 1) ~ " x " ~ roundTo(mY * P + DT + earExtra, 1) ~ " mm.");
        }

        // ---------- 5. detachable feet ----------
        if (definition.AddFeet)
        {
            const fw = definition.FootWidth / millimeter;
            const fd = definition.FootDepth / millimeter;
            const fh = definition.FootHeight / millimeter;
            const sd = definition.SlotDepth / millimeter;
            if (sd >= fh)
                throw regenError("Slot depth must be smaller than the foot height.", ["SlotDepth"]);
            var fxs = [-definition.FootSpacing / millimeter / 2, definition.FootSpacing / millimeter / 2];
            if (sp.kind == "hex")
            {
                fxs = [0];
                reportFeatureInfo(context, id, "Hex cluster has a single flat bottom edge: one centred foot is generated.");
            }
            const S = T + tol;                 // slot width
            const zc = T / 2;
            const p = tol + crush;             // rib protrusion into slot
            const rr = 1.2;
            for (var i = 0; i < size(fxs); i += 1)
            {
                const fx = fxs[i];
                const fw2 = (sp.kind == "hex") ? min(fw, sp.r - 12) : fw;
                const fid = id + ("foot" ~ i);
                fCuboid(context, fid + "body", {
                            "corner1" : vector(fx - fw2 / 2, -(fh - sd), zc - fd / 2) * millimeter,
                            "corner2" : vector(fx + fw2 / 2, sd, zc + fd / 2) * millimeter
                        });
                fCuboid(context, fid + "slot", {
                            "corner1" : vector(fx - fw2 / 2 - 1, 0, zc - S / 2) * millimeter,
                            "corner2" : vector(fx + fw2 / 2 + 1, sd + 1, zc + S / 2) * millimeter
                        });
                opBoolean(context, fid + "cutSlot", {
                            "targets" : qCreatedBy(fid + "body", EntityType.BODY),
                            "tools" : qCreatedBy(fid + "slot", EntityType.BODY),
                            "operation" : BooleanOperationType.SUBTRACTION
                        });
                // push-fit crush ribs, one on each slot wall
                fCylinder(context, fid + "ribA", {
                            "topCenter" : vector(fx - fw2 / 4, sd, zc - S / 2 + p - rr) * millimeter,
                            "bottomCenter" : vector(fx - fw2 / 4, 0, zc - S / 2 + p - rr) * millimeter,
                            "radius" : rr * millimeter
                        });
                fCylinder(context, fid + "ribB", {
                            "topCenter" : vector(fx + fw2 / 4, sd, zc + S / 2 - p + rr) * millimeter,
                            "bottomCenter" : vector(fx + fw2 / 4, 0, zc + S / 2 - p + rr) * millimeter,
                            "radius" : rr * millimeter
                        });
                opBoolean(context, fid + "ribUnion", { "tools" : qCreatedBy(fid, EntityType.BODY), "operation" : BooleanOperationType.UNION });
            }
        }
    }, {
            BoardShape : PegboardShape.GOTHIC_ARCH,
            HeaderBand : 60 * millimeter,
            RecessDepth : 1.2 * millimeter,
            CornerChamfer : 12 * millimeter,
            AddEyelets : true,
            EyeletHole : 9.525 * millimeter,
            EyeletWall : 4 * millimeter,
            EyeletSpacing : 200 * millimeter,
            AddFeet : true,
            FootWidth : 50 * millimeter,
            FootDepth : 90 * millimeter,
            FootHeight : 22 * millimeter,
            SlotDepth : 15 * millimeter,
            FootSpacing : 200 * millimeter
        });

// ---------------------------------------------------------------- helpers

/** Extrude all regions of a sketch along +Z by `depth` mm. */
function extrudeUp(context is Context, id is Id, entities is Query, depth is number)
{
    opExtrude(context, id, {
                "entities" : entities,
                "direction" : vector(0, 0, 1),
                "endBound" : BoundingType.BLIND,
                "depth" : depth * millimeter
            });
}

/** Closed polygon from [[x, y], ...] in mm. */
function polyLoop(sk is Sketch, name is string, pts is array)
{
    const n = size(pts);
    for (var i = 0; i < n; i += 1)
    {
        const a = pts[i];
        const b = pts[(i + 1) % n];
        skLineSegment(sk, name ~ "_" ~ i, {
                    "start" : vector(a[0], a[1]) * millimeter,
                    "end" : vector(b[0], b[1]) * millimeter
                });
    }
}

function roundTo(x is number, digits is number) returns number
{
    const f = 10 ^ digits;
    return round(x * f) / f;
}

/** Hex cluster centres (flat-top hexagons) in mm. */
function hexCenters(sp is map) returns array
{
    var c = [[0, sp.cy]];
    for (var k = 0; k < 6; k += 1)
    {
        const a = (30 + 60 * k) * degree;
        c = append(c, [sqrt(3) * sp.r * cos(a), sp.cy + sqrt(3) * sp.r * sin(a)]);
    }
    return c;
}

/** Is the point (mm) inside the board outline? */
function insideShape(sp is map, x is number, y is number) returns boolean
{
    if (sp.kind == "arch")
    {
        if (abs(x) > sp.W / 2 || y < 0 || y > sp.H)
            return false;
        if (y <= sp.s)
            return true;
        const dy = y - sp.s;
        return (x - sp.d) * (x - sp.d) + dy * dy <= sp.R * sp.R && (x + sp.d) * (x + sp.d) + dy * dy <= sp.R * sp.R;
    }
    if (sp.kind == "plate")
    {
        const hw = sp.W / 2;
        if (abs(x) > hw || y < 0 || y > sp.H)
            return false;
        const ax = abs(x) - (hw - sp.c);
        if (ax > 0 && y > sp.H - sp.c && ax + (y - (sp.H - sp.c)) > sp.c)
            return false;
        if (ax > 0 && y < sp.c && ax + (sp.c - y) > sp.c)
            return false;
        return true;
    }
    if (sp.kind == "hex")
    {
        const ap = sqrt(3) / 2 * sp.r;
        const centers = hexCenters(sp);
        for (var i = 0; i < size(centers); i += 1)
        {
            const dx = x - centers[i][0];
            const dy = y - centers[i][1];
            if (abs(dy) <= ap && abs(dx * sqrt(3) / 2 + dy * 0.5) <= ap && abs(-dx * sqrt(3) / 2 + dy * 0.5) <= ap)
                return true;
        }
        return false;
    }
    // custom silhouette: assumed to fill its bounding box (width x height)
    return abs(x) <= sp.W / 2 && y >= 0 && y <= sp.H;
}

/** Point inside the outline with at least `m` mm of material all around (sampled). */
function okInset(sp is map, x is number, y is number, m is number) returns boolean
{
    if (!insideShape(sp, x, y))
        return false;
    for (var k = 0; k < 36; k += 1)
    {
        const a = k * 10 * degree;
        if (!insideShape(sp, x + m * cos(a), y + m * sin(a)))
            return false;
    }
    return true;
}

/** Highest Y of the outline at X, or -1 when X is outside. */
function topY(sp is map, x is number) returns number
{
    for (var y = sp.H; y >= 0; y -= 0.25)
    {
        if (insideShape(sp, x, y))
            return y;
    }
    return -1;
}

/** Map dovetail-local (u along the seam, v across it) to world XY in mm. */
function dtMap(dt is map, u is number, v is number) returns array
{
    return dt.vertical ? [dt.seam + v, dt.c + u] : [dt.c + u, dt.seam + v];
}

function mapAll(dt is map, pts is array) returns array
{
    var out = [];
    for (var i = 0; i < size(pts); i += 1)
        out = append(out, dtMap(dt, pts[i][0], pts[i][1]));
    return out;
}

/** Does the whole dovetail (tail, socket and a 3 mm wall around it) stay inside the board? */
function dovetailFits(sp is map, dt is map, g is number, DT is number, wn is number, wt is number, cp is number, tol is number) returns boolean
{
    const probes = [[0, -g / 2 - 3], [wn + cp, -g / 2 - 3], [-wn - cp, -g / 2 - 3], [0, DT + tol], [wt + cp, DT + tol], [-wt - cp, DT + tol], [0, DT / 2], [wn + cp, DT / 2], [-wn - cp, DT / 2]];
    for (var i = 0; i < size(probes); i += 1)
    {
        const q = dtMap(dt, probes[i][0], probes[i][1]);
        if (!okInset(sp, q[0], q[1], 3))
            return false;
    }
    return true;
}
