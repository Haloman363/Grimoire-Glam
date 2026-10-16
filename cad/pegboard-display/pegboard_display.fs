FeatureScript 2180;
import(path : "onshape/std/common.fs", version : "2180.0");
import(path : "onshape/std/boolean.fs", version : "2180.0");
import(path : "onshape/std/extrude.fs", version : "2180.0");
import(path : "onshape/std/sketch.fs", version : "2180.0");
import(path : "onshape/std/transform.fs", version : "2180.0");
import(path : "onshape/std/evaluate.fs", version : "2180.0");
import(path : "onshape/std/properties.fs", version : "2180.0");
import(path : "onshape/std/deleteBodies.fs", version : "2180.0");

/**
 * Modular pegboard market-display system.
 *
 * Coordinate system (all geometry is built on / above the Top plane):
 *   X = board width, Y = board height (up), Z = thickness.
 *   Back face of the board is z = 0, front (peg side) is z = thickness.
 *
 * Output (separate, named solid bodies):
 *   "Tile Cx Ry"   flat-pack board tiles, each sized to fit the print bed
 *   "Joint key n"  loose bow-tie keys (only when Joint style = Separate keys)
 *   "Base foot n"  slotted shoe feet with the backward tilt
 *   "Header plate" optional peg-in banner plate
 *
 * Hanging eyelets are merged into the board (and therefore into the tiles).
 *
 * Internally all 2D maths is done on plain numbers in millimetres; units are
 * only re-applied when geometry is handed to Onshape.
 */

const PI_ = 3.141592653589793;
const BIG = 3000; // mm - size of the "outside the tile" cutting slabs

// ------------------------------------------------------------------ enums

export enum BoardProfile
{
    annotation { "Name" : "B - Cathedral / gothic arch" }
    ARCH,
    annotation { "Name" : "A - Character logo silhouette (imported DXF/SVG sketch)" }
    LOGO,
    annotation { "Name" : "C - Seasonal / event preset" }
    PRESET
}

export enum SeasonalPreset
{
    annotation { "Name" : "Coffin" }
    COFFIN,
    annotation { "Name" : "Crescent moon" }
    CRESCENT,
    annotation { "Name" : "Hexagon trio" }
    HEX_TRIO,
    annotation { "Name" : "Hexagon flower (7)" }
    HEX_FLOWER
}

export enum PrintBed
{
    annotation { "Name" : "220 x 220 mm" }
    BED_220,
    annotation { "Name" : "256 x 256 mm" }
    BED_256,
    annotation { "Name" : "Custom" }
    CUSTOM
}

export enum JointStyle
{
    annotation { "Name" : "Integral dovetail tabs and sockets" }
    INTEGRAL,
    annotation { "Name" : "Separate bow-tie keys in back pockets" }
    KEYS
}

export enum LayoutMode
{
    annotation { "Name" : "Assembled (display position)" }
    ASSEMBLED,
    annotation { "Name" : "Print layout (flat, packed in rows)" }
    PRINT
}

// ---------------------------------------------------------------- feature

annotation { "Feature Type Name" : "Pegboard display system", "Feature Name Template" : "Pegboard display" }
export const pegboardDisplay = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Group Name" : "1. Board shape", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Profile" }
            definition.profile is BoardProfile;

            if (definition.profile == BoardProfile.LOGO)
            {
                annotation { "Name" : "Silhouette region (sketch on Top plane)", "Filter" : EntityType.FACE && SketchObject.YES, "MaxNumberOfPicks" : 1 }
                definition.logoRegion is Query;

                annotation { "Name" : "Solid outer border (min 12 mm)" }
                isLength(definition.logoBorder, { (millimeter) : [12, 12, 60] } as LengthBoundSpec);
            }
            else
            {
                annotation { "Name" : "BoardWidth" }
                isLength(definition.boardWidth, { (millimeter) : [60, 250, 1500] } as LengthBoundSpec);

                annotation { "Name" : "BoardHeight" }
                isLength(definition.boardHeight, { (millimeter) : [60, 450, 1500] } as LengthBoundSpec);

                if (definition.profile == BoardProfile.ARCH)
                {
                    annotation { "Name" : "ArchRadius (>= half the width)" }
                    isLength(definition.archRadius, { (millimeter) : [30, 250, 3000] } as LengthBoundSpec);
                }
                else
                {
                    annotation { "Name" : "Preset" }
                    definition.seasonal is SeasonalPreset;
                }
            }
        }

        annotation { "Group Name" : "2. Pegboard grid", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Hole spacing (1.0 in)" }
            isLength(definition.pitch, { (millimeter) : [15, 25.4, 60] } as LengthBoundSpec);

            annotation { "Name" : "Hole diameter (0.257 in)" }
            isLength(definition.holeDia, { (millimeter) : [3, 6.53, 14] } as LengthBoundSpec);

            annotation { "Name" : "Board thickness (0.20 in)" }
            isLength(definition.thickness, { (millimeter) : [3, 5.08, 15] } as LengthBoundSpec);

            annotation { "Name" : "Hole-to-edge clearance" }
            isLength(definition.edgeClearance, { (millimeter) : [2, 8, 30] } as LengthBoundSpec);

            annotation { "Name" : "Grid offset X" }
            isLength(definition.gridOffsetX, { (millimeter) : [-30, 0, 30] } as LengthBoundSpec);

            annotation { "Name" : "Grid offset Y" }
            isLength(definition.gridOffsetY, { (millimeter) : [-30, 0, 30] } as LengthBoundSpec);
        }

        annotation { "Group Name" : "3. Flat-pack tiles and joints", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Print bed" }
            definition.printBed is PrintBed;

            if (definition.printBed == PrintBed.CUSTOM)
            {
                annotation { "Name" : "Max tile X" }
                isLength(definition.tileMaxX, { (millimeter) : [60, 220, 600] } as LengthBoundSpec);

                annotation { "Name" : "Max tile Y" }
                isLength(definition.tileMaxY, { (millimeter) : [60, 220, 600] } as LengthBoundSpec);
            }

            annotation { "Name" : "Joint style" }
            definition.jointStyle is JointStyle;

            annotation { "Name" : "Fit tolerance (per gap)" }
            isLength(definition.tolerance, { (millimeter) : [0.02, 0.15, 0.6] } as LengthBoundSpec);

            annotation { "Name" : "Dovetail depth (half-length for keys)" }
            isLength(definition.dovetailDepth, { (millimeter) : [3, 6, 9] } as LengthBoundSpec);

            annotation { "Name" : "Dovetail neck width" }
            isLength(definition.dovetailNeck, { (millimeter) : [3, 7, 14] } as LengthBoundSpec);

            annotation { "Name" : "Dovetail head width" }
            isLength(definition.dovetailHead, { (millimeter) : [4, 11, 18] } as LengthBoundSpec);

            annotation { "Name" : "Target joint spacing along seam" }
            isLength(definition.jointSpacing, { (millimeter) : [30, 90, 300] } as LengthBoundSpec);

            if (definition.jointStyle == JointStyle.KEYS)
            {
                annotation { "Name" : "Key thickness" }
                isLength(definition.keyThickness, { (millimeter) : [1.5, 3, 8] } as LengthBoundSpec);
            }
        }

        annotation { "Group Name" : "4. Tabletop feet", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Add slotted base feet" }
            definition.addFeet is boolean;

            if (definition.addFeet)
            {
                annotation { "Name" : "Backward tilt" }
                isAngle(definition.tiltAngle, { (degree) : [0, 5, 20] } as AngleBoundSpec);

                annotation { "Name" : "Foot spacing (centre to centre)" }
                isLength(definition.footSpacing, { (millimeter) : [20, 140, 800] } as LengthBoundSpec);

                annotation { "Name" : "Foot width" }
                isLength(definition.footWidth, { (millimeter) : [20, 55, 150] } as LengthBoundSpec);

                annotation { "Name" : "Slot depth" }
                isLength(definition.footSlotDepth, { (millimeter) : [8, 18, 50] } as LengthBoundSpec);

                annotation { "Name" : "Floor thickness under slot" }
                isLength(definition.footFloor, { (millimeter) : [3, 5, 20] } as LengthBoundSpec);

                annotation { "Name" : "Reach behind board" }
                isLength(definition.footReachBack, { (millimeter) : [15, 55, 150] } as LengthBoundSpec);

                annotation { "Name" : "Reach in front of board" }
                isLength(definition.footReachFront, { (millimeter) : [15, 30, 150] } as LengthBoundSpec);
            }
        }

        annotation { "Group Name" : "5. Hanging eyelets", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Add top eyelet loops" }
            definition.addEyelets is boolean;

            if (definition.addEyelets)
            {
                annotation { "Name" : "Eyelet count" }
                isInteger(definition.eyeletCount, { (unitless) : [1, 2, 4] } as IntegerBoundSpec);

                annotation { "Name" : "Eyelet spacing" }
                isLength(definition.eyeletSpacing, { (millimeter) : [20, 120, 600] } as LengthBoundSpec);

                annotation { "Name" : "Eyelet inner diameter" }
                isLength(definition.eyeletID, { (millimeter) : [4, 10, 30] } as LengthBoundSpec);

                annotation { "Name" : "Eyelet wall" }
                isLength(definition.eyeletWall, { (millimeter) : [2, 5, 12] } as LengthBoundSpec);
            }
        }

        annotation { "Group Name" : "6. Header plate", "Collapsed By Default" : true }
        {
            annotation { "Name" : "Add peg-in header plate" }
            definition.addHeader is boolean;

            if (definition.addHeader)
            {
                annotation { "Name" : "Header width" }
                isLength(definition.headerWidth, { (millimeter) : [40, 150, 600] } as LengthBoundSpec);

                annotation { "Name" : "Header height" }
                isLength(definition.headerHeight, { (millimeter) : [15, 40, 200] } as LengthBoundSpec);

                annotation { "Name" : "Header thickness" }
                isLength(definition.headerThickness, { (millimeter) : [1.5, 3, 10] } as LengthBoundSpec);

                annotation { "Name" : "Peg distance (in hole pitches)" }
                isInteger(definition.headerPegSpan, { (unitless) : [1, 4, 12] } as IntegerBoundSpec);

                annotation { "Name" : "Header vertical offset" }
                isLength(definition.headerOffset, { (millimeter) : [-60, 0, 60] } as LengthBoundSpec);
            }
        }

        annotation { "Group Name" : "7. Output", "Collapsed By Default" : false }
        {
            annotation { "Name" : "Layout" }
            definition.layout is LayoutMode;
        }
    }
    {
        // ------------------------------------------------------------ unpack
        var t = definition.thickness / millimeter;
        var p = definition.pitch / millimeter;
        var r = definition.holeDia / millimeter / 2;
        var margin = definition.edgeClearance / millimeter;
        var tol = definition.tolerance / millimeter;
        var dDepth = definition.dovetailDepth / millimeter;
        var nHalf = definition.dovetailNeck / millimeter / 2;
        var hHalf = definition.dovetailHead / millimeter / 2;
        var warnings = [];

        if (hHalf <= nHalf)
            throw regenError("Dovetail head width must be larger than the neck width.");

        var bedX = 220;
        var bedY = 220;
        if (definition.printBed == PrintBed.BED_256)
        {
            bedX = 256;
            bedY = 256;
        }
        else if (definition.printBed == PrintBed.CUSTOM)
        {
            bedX = definition.tileMaxX / millimeter;
            bedY = definition.tileMaxY / millimeter;
        }

        // ----------------------------------------------- 1. board silhouette
        var G = { "t" : t, "kind" : "poly", "poly" : [], "centers" : [], "groups" : 1, "border" : 0, "lugs" : [] };
        var segs = [];
        var bb = {};
        var border = 0;

        if (definition.profile == BoardProfile.LOGO)
        {
            border = definition.logoBorder / millimeter;
            var fp = evPlane(context, { "face" : definition.logoRegion });
            if (abs(fp.normal[2]) < 0.999 || abs(fp.origin[2] / millimeter) > 0.01)
                throw regenError("The silhouette sketch must lie on the Top plane (z = 0). Import the DXF/SVG into a sketch on the Top plane.");
            var sampled = sampleFaceEdges(context, definition.logoRegion, 4);
            segs = sampled.segs;
            var b0 = bboxOf(sampled.pts);
            bb = { "x0" : b0.x0 - border, "x1" : b0.x1 + border, "y0" : b0.y0 - border, "y1" : b0.y1 + border };
            G.kind = "logo";
            G.logoFace = definition.logoRegion;
            G.centers = sampled.pts;
            G.groups = ceil((2 * border + 0.5) / 4);
            G.border = border;
        }
        else
        {
            var W = definition.boardWidth / millimeter;
            var H = definition.boardHeight / millimeter;
            var poly = [];
            if (definition.profile == BoardProfile.ARCH)
            {
                var R = definition.archRadius / millimeter;
                if (R < W / 2 + 0.01)
                    throw regenError("ArchRadius must be at least half of BoardWidth.");
                var rise = sqrt(R * R - (R - W / 2) * (R - W / 2));
                if (H < rise + 20)
                    throw regenError("BoardHeight is too small for this ArchRadius (the arch alone needs " ~ toString(floor(rise)) ~ " mm plus 20 mm of straight wall).");
                poly = archPolygon(W, H, R);
            }
            else if (definition.seasonal == SeasonalPreset.COFFIN)
                poly = coffinPolygon(W, H);
            else if (definition.seasonal == SeasonalPreset.CRESCENT)
                poly = fitUniform(crescentPolygon(), W, H);
            else if (definition.seasonal == SeasonalPreset.HEX_TRIO)
                poly = fitUniform(hexClusterOutline([[0, 0], [1, 0], [0, 1]]), W, H);
            else
                poly = fitUniform(hexClusterOutline([[0, 0], [1, 0], [-1, 0], [0, 1], [0, -1], [1, -1], [-1, 1]]), W, H);
            segs = polyToSegs(poly);
            bb = bboxOf(poly);
            G.poly = poly;
        }

        var cxB = (bb.x0 + bb.x1) / 2;
        var gx = cxB + definition.gridOffsetX / millimeter;
        var gy = (bb.y0 + bb.y1) / 2 + definition.gridOffsetY / millimeter;

        // ------------------------------------------------- 2. peg holes
        var holes = computeHoles(segs, bb, p, gx, gy, r, margin, border);
        if (size(holes) == 0)
            throw regenError("No peg holes fit inside the board with the current edge clearance.");

        // ------------------------------------------- 3. hanging eyelets
        if (definition.addEyelets)
        {
            var ri = definition.eyeletID / millimeter / 2;
            var ro = ri + definition.eyeletWall / millimeter;
            var nE = definition.eyeletCount;
            var lugRise = 0;
            for (var k = 0; k < nE; k += 1)
            {
                var ex = cxB + (k - (nE - 1) / 2) * definition.eyeletSpacing / millimeter;
                var top = boardEdgeY(segs, border, ex, bb.y0 - 5, bb.y1 + 5, 1);
                if (top == undefined)
                {
                    warnings = append(warnings, "Eyelet " ~ (k + 1) ~ " is outside the board and was skipped");
                    continue;
                }
                var lug = { "x" : ex, "cy" : top + ri + 3, "yb" : top - 10, "ro" : ro, "ri" : ri, "bw" : ro + 4 };
                G.lugs = append(G.lugs, lug);
                lugRise = max(lugRise, lug.cy + ro - bb.y1);
            }
            bb.y1 = bb.y1 + max(0, lugRise);
        }

        // ------------------------------------------ 4. tile grid and joints
        var lap = (definition.jointStyle == JointStyle.INTEGRAL) ? dDepth + tol : 0;
        var cellMaxX = bedX - lap;
        var cellMaxY = bedY - lap;
        if (cellMaxX <= 2 * p || cellMaxY <= 2 * p)
            throw regenError("The print bed is too small for this hole pitch and dovetail depth.");

        var vSeams = seamPositions(bb.x0, bb.x1, cellMaxX, gx, p);
        var hSeams = seamPositions(bb.y0, bb.y1, cellMaxY, gy, p);
        var nCols = size(vSeams) + 1;
        var nRows = size(hSeams) + 1;

        var across = dDepth + tol;
        var along = hHalf + 2 * tol;
        if (nCols + nRows > 2)
        {
            if (across >= p / 2 || along >= p / 2)
                throw regenError("Joint footprint is larger than half a hole pitch - reduce the dovetail size.");
            var cornerGap = hyp(p / 2 - across, p / 2 - along) - r;
            if (cornerGap < 1.5)
                throw regenError("Joints would sit closer than 1.5 mm to a peg hole - reduce the dovetail size.");
        }

        var allJoints = [];
        var maleMap = {};
        var femaleMap = {};
        var spacing = definition.jointSpacing / millimeter;
        for (var s = 0; s < size(vSeams); s += 1)
        {
            for (var j = 0; j < nRows; j += 1)
            {
                var lo = (j == 0) ? bb.y0 : hSeams[j - 1];
                var hi = (j == nRows - 1) ? bb.y1 : hSeams[j];
                for (var c in seamJoints(segs, border, 0, vSeams[s], lo, hi, gy, p, across, along, spacing))
                {
                    var jt = { "x" : vSeams[s], "y" : c, "dir" : 0 };
                    allJoints = append(allJoints, jt);
                    maleMap = addTo(maleMap, tkey(s, j), jt);
                    femaleMap = addTo(femaleMap, tkey(s + 1, j), jt);
                }
            }
        }
        for (var s = 0; s < size(hSeams); s += 1)
        {
            for (var i = 0; i < nCols; i += 1)
            {
                var lo = (i == 0) ? bb.x0 : vSeams[i - 1];
                var hi = (i == nCols - 1) ? bb.x1 : vSeams[i];
                for (var c in seamJoints(segs, border, 1, hSeams[s], lo, hi, gx, p, across, along, spacing))
                {
                    var jt = { "x" : c, "y" : hSeams[s], "dir" : 1 };
                    allJoints = append(allJoints, jt);
                    maleMap = addTo(maleMap, tkey(i, s), jt);
                    femaleMap = addTo(femaleMap, tkey(i, s + 1), jt);
                }
            }
        }
        if (nCols + nRows > 2 && size(allJoints) == 0)
            warnings = append(warnings, "No joint position fits on the seams - the tiles have no interlock");

        var keyT = (definition.jointStyle == JointStyle.KEYS) ? definition.keyThickness / millimeter : 0;
        if (definition.jointStyle == JointStyle.KEYS && keyT + tol + 1.2 > t)
            throw regenError("Key thickness leaves less than 1.2 mm of board in front of the key pocket.");

        // ------------------------------------------------------ 5. tiles
        var items = [];
        var noRot = undefined;
        for (var j = 0; j < nRows; j += 1)
        {
            for (var i = 0; i < nCols; i += 1)
            {
                // Inner seam faces are recessed by half the tolerance so that
                // neighbouring tiles never fuse together while printing.
                var xa = (i == 0) ? undefined : vSeams[i - 1] + tol / 2;
                var xb = (i == nCols - 1) ? undefined : vSeams[i] - tol / 2;
                var ya = (j == 0) ? undefined : hSeams[j - 1] + tol / 2;
                var yb = (j == nRows - 1) ? undefined : hSeams[j] - tol / 2;
                if (!cellHasMaterial(segs, border, bb, xa, xb, ya, yb))
                    continue;

                var tid = id + ("tile" ~ i ~ "_" ~ j);
                var tileQ = buildMaster(context, tid + "m", G);

                // clip to the cell
                var slabs = [];
                if (xa != undefined)
                    slabs = append(slabs, [[xa - BIG, -BIG], [xa, -BIG], [xa, BIG], [xa - BIG, BIG]]);
                if (xb != undefined)
                    slabs = append(slabs, [[xb, -BIG], [xb + BIG, -BIG], [xb + BIG, BIG], [xb, BIG]]);
                if (ya != undefined)
                    slabs = append(slabs, [[-BIG, ya - BIG], [BIG, ya - BIG], [BIG, ya], [-BIG, ya]]);
                if (yb != undefined)
                    slabs = append(slabs, [[-BIG, yb], [BIG, yb], [BIG, yb + BIG], [-BIG, yb + BIG]]);
                if (size(slabs) > 0)
                    subtractFrom(context, tid + "clip", tileQ, prism(context, tid + "slab", -1, t + 2, slabs, []));

                // peg holes that belong to this cell
                var circles = [];
                for (var h in holes)
                {
                    if (inCell(h[0], h[1], xa, xb, ya, yb))
                        circles = append(circles, [h[0], h[1], r]);
                }
                if (size(circles) > 0)
                    subtractFrom(context, tid + "holes", tileQ, prism(context, tid + "hp", -1, t + 2, [], circles));

                // joints
                var males = (maleMap[tkey(i, j)] == undefined) ? [] : maleMap[tkey(i, j)];
                var females = (femaleMap[tkey(i, j)] == undefined) ? [] : femaleMap[tkey(i, j)];
                if (definition.jointStyle == JointStyle.INTEGRAL)
                {
                    if (size(females) > 0)
                    {
                        var sockets = [];
                        for (var jt in females)
                            sockets = append(sockets, dovetailPoly(jt, dDepth, nHalf, hHalf, tol, 1.5));
                        subtractFrom(context, tid + "sock", tileQ, prism(context, tid + "sp", -1, t + 2, sockets, []));
                    }
                    if (size(males) > 0)
                    {
                        var tabs = [];
                        for (var jt in males)
                            tabs = append(tabs, dovetailPoly(jt, dDepth, nHalf, hHalf, 0, 1.5));
                        var tabQ = prism(context, tid + "tab", 0, t, tabs, []);
                        opBoolean(context, tid + "tabU", { "tools" : qUnion([tileQ, tabQ]), "operationType" : BooleanOperationType.UNION });
                        tileQ = qUnion([tileQ, tabQ]);
                    }
                }
                else
                {
                    var pockets = [];
                    for (var jt in males)
                        pockets = append(pockets, keyPoly(jt, dDepth, nHalf, hHalf, tol));
                    for (var jt in females)
                        pockets = append(pockets, keyPoly(jt, dDepth, nHalf, hHalf, tol));
                    if (size(pockets) > 0)
                        subtractFrom(context, tid + "pocket", tileQ, prism(context, tid + "kp", -1, keyT + tol + 1, pockets, []));
                }

                var tileName = "Tile C" ~ (i + 1) ~ "R" ~ (j + 1);
                setProperty(context, { "entities" : tileQ, "propertyType" : PropertyType.NAME, "value" : tileName });
                var tb = evBox3d(context, { "topLevelId" : id, "entities" : tileQ });
                var tsz = (tb.maxCorner - tb.minCorner) / millimeter;
                if (tsz[0] > bedX + 0.01 || tsz[1] > bedY + 0.01)
                    warnings = append(warnings, tileName ~ " is " ~ round(tsz[0]) ~ " x " ~ round(tsz[1]) ~ " mm and exceeds the print bed");
                items = append(items, { "q" : tileQ, "printRot" : noRot });
            }
        }

        // -------------------------------------------------- 6. joint keys
        if (definition.jointStyle == JointStyle.KEYS && size(allJoints) > 0)
        {
            var keyLoops = [];
            for (var jt in allJoints)
                keyLoops = append(keyLoops, keyPoly(jt, dDepth, nHalf, hHalf, 0));
            var keyBodies = evaluateQuery(context, prism(context, id + "keys", 0, keyT, keyLoops, []));
            for (var k = 0; k < size(keyBodies); k += 1)
            {
                setProperty(context, { "entities" : keyBodies[k], "propertyType" : PropertyType.NAME, "value" : "Joint key " ~ (k + 1) });
                items = append(items, { "q" : keyBodies[k], "printRot" : noRot });
            }
        }

        // ---------------------------------------------------- 7. base feet
        var xAxis = line(vector(0, 0, 0) * millimeter, vector(1, 0, 0));
        if (definition.addFeet)
        {
            var tilt = definition.tiltAngle;
            var cs = cos(tilt);
            var sn = sin(tilt);
            var fw = definition.footWidth / millimeter;
            var slotD = definition.footSlotDepth / millimeter;
            var floorT = definition.footFloor / millimeter;
            var back = definition.footReachBack / millimeter;
            var front = definition.footReachFront / millimeter;
            var fSpacing = definition.footSpacing / millimeter;
            var feet = [];
            var kMin = 1e12;
            for (var f = 0; f < 2; f += 1)
            {
                var fx = cxB + (f - 0.5) * fSpacing;
                var baseY = 1e12;
                for (var x = fx - fw / 2; x <= fx + fw / 2 + 1e-6; x += 2)
                {
                    var yEdge = boardEdgeY(segs, border, x, bb.y0 - 5, bb.y1 + 5, -1);
                    if (yEdge == undefined)
                        throw regenError("A base foot sits outside the board silhouette - reduce Foot spacing or Foot width.");
                    baseY = min(baseY, yEdge);
                }
                // world-vertical coordinate / world-depth coordinate of the slot floor centre
                var yw = baseY * cs + (t / 2) * sn;
                var zw = -baseY * sn + (t / 2) * cs;
                kMin = min(kMin, yw);
                feet = append(feet, { "x" : fx, "baseY" : baseY, "yw" : yw, "zw" : zw });
            }
            var kBottom = kMin - floorT - 4;
            var slotC = tol;
            for (var f = 0; f < 2; f += 1)
            {
                var ft = feet[f];
                var fid = id + ("foot" ~ f);
                var xa2 = ft.x - fw / 2;
                var xb2 = ft.x + fw / 2;
                var blockQ = prism(context, fid + "blk", ft.zw - back, back + front,
                        [[[xa2, kBottom], [xb2, kBottom], [xb2, ft.yw + slotD], [xa2, ft.yw + slotD]]], []);
                opTransform(context, fid + "rot", { "bodies" : blockQ, "transform" : rotationAround(xAxis, tilt) });
                var slotQ = prism(context, fid + "slot", -slotC, t + 2 * slotC,
                        [[[xa2 - 1, ft.baseY], [xb2 + 1, ft.baseY], [xb2 + 1, ft.baseY + 500], [xa2 - 1, ft.baseY + 500]]], []);
                subtractFrom(context, fid + "cut", blockQ, slotQ);
                setProperty(context, { "entities" : blockQ, "propertyType" : PropertyType.NAME, "value" : "Base foot " ~ (f + 1) });
                items = append(items, { "q" : blockQ, "printRot" : rotationAround(xAxis, 90 * degree - tilt) });
            }
        }

        // ------------------------------------------------- 8. header plate
        if (definition.addHeader)
        {
            var span = definition.headerPegSpan;
            var pegs = pickHeaderPegs(holes, span, p, cxB);
            if (pegs == undefined)
                warnings = append(warnings, "Header plate skipped: no two peg holes " ~ span ~ " pitches apart exist in one row");
            else
            {
                var hw = definition.headerWidth / millimeter;
                var hh = definition.headerHeight / millimeter;
                var hT = definition.headerThickness / millimeter;
                var cxh = (pegs.x1 + pegs.x2) / 2;
                var cyh = pegs.y + definition.headerOffset / millimeter;
                var ch = min(6, min(hw, hh) / 4);
                var plate = [[cxh - hw / 2 + ch, cyh - hh / 2], [cxh + hw / 2 - ch, cyh - hh / 2], [cxh + hw / 2, cyh - hh / 2 + ch],
                        [cxh + hw / 2, cyh + hh / 2 - ch], [cxh + hw / 2 - ch, cyh + hh / 2], [cxh - hw / 2 + ch, cyh + hh / 2],
                        [cxh - hw / 2, cyh + hh / 2 - ch], [cxh - hw / 2, cyh - hh / 2 + ch]];
                var plateQ = prism(context, id + "hdr", t, hT, [plate], []);
                var pegR = r - tol;
                var pegQ = prism(context, id + "hpeg", 0, t, [], [[pegs.x1, pegs.y, pegR], [pegs.x2, pegs.y, pegR]]);
                opBoolean(context, id + "hdrU", { "tools" : qUnion([plateQ, pegQ]), "operationType" : BooleanOperationType.UNION });
                var hdrQ = qUnion([plateQ, pegQ]);
                setProperty(context, { "entities" : hdrQ, "propertyType" : PropertyType.NAME, "value" : "Header plate" });
                items = append(items, { "q" : hdrQ, "printRot" : rotationAround(xAxis, 180 * degree) });
            }
        }

        // ---------------------------------------------------- 9. layout
        if (definition.layout == LayoutMode.PRINT)
            layoutItems(context, id, items, bb.x0, bb.y0, 3 * max(bedX, bedY), 10);

        if (size(warnings) > 0)
            reportFeatureWarning(context, id, joinStrings(warnings, "; "));
    });

// ================================================================ helpers

function hyp(a is number, b is number) returns number
{
    return sqrt(a * a + b * b);
}

function cosR(a is number) returns number
{
    return cos(a * radian);
}

function sinR(a is number) returns number
{
    return sin(a * radian);
}

function joinStrings(parts is array, sep is string) returns string
{
    var out = "";
    for (var i = 0; i < size(parts); i += 1)
        out = out ~ ((i == 0) ? "" : sep) ~ parts[i];
    return out;
}

function tkey(i is number, j is number) returns string
{
    return toString(i) ~ "_" ~ toString(j);
}

function addTo(m is map, key is string, v) returns map
{
    var arr = (m[key] == undefined) ? [] : m[key];
    m[key] = append(arr, v);
    return m;
}

function bboxOf(pts is array) returns map
{
    var x0 = 1e12;
    var x1 = -1e12;
    var y0 = 1e12;
    var y1 = -1e12;
    for (var q in pts)
    {
        x0 = min(x0, q[0]);
        x1 = max(x1, q[0]);
        y0 = min(y0, q[1]);
        y1 = max(y1, q[1]);
    }
    return { "x0" : x0, "x1" : x1, "y0" : y0, "y1" : y1 };
}

// ------------------------------------------------------- 2D signed distance

function distToSeg(px is number, py is number, ax is number, ay is number, bx is number, by is number) returns number
{
    var dx = bx - ax;
    var dy = by - ay;
    var l2 = dx * dx + dy * dy;
    var u = 0;
    if (l2 > 1e-12)
        u = min(1, max(0, ((px - ax) * dx + (py - ay) * dy) / l2));
    var ex = ax + u * dx - px;
    var ey = ay + u * dy - py;
    return sqrt(ex * ex + ey * ey);
}

function insideSegs(segs is array, px is number, py is number) returns boolean
{
    var inside = false;
    for (var s in segs)
    {
        var ay = s[1];
        var by = s[3];
        if ((ay > py) != (by > py))
        {
            var xint = s[0] + (py - ay) * (s[2] - s[0]) / (by - ay);
            if (px < xint)
                inside = !inside;
        }
    }
    return inside;
}

// Negative inside the silhouette, positive outside.
function signedDist(segs is array, px is number, py is number) returns number
{
    var d = 1e12;
    for (var s in segs)
        d = min(d, distToSeg(px, py, s[0], s[1], s[2], s[3]));
    return insideSegs(segs, px, py) ? -d : d;
}

function polyToSegs(pts is array) returns array
{
    var segs = [];
    var n = size(pts);
    for (var i = 0; i < n; i += 1)
    {
        var a = pts[i];
        var b = pts[(i + 1) % n];
        segs = append(segs, [a[0], a[1], b[0], b[1]]);
    }
    return segs;
}

// Y of the board edge at x. dir = +1 for the top edge, -1 for the bottom edge.
function boardEdgeY(segs is array, border is number, x is number, ylo is number, yhi is number, dir is number)
{
    var n = ceil(yhi - ylo);
    var outside = (dir > 0) ? yhi : ylo;
    for (var i = 0; i <= n; i += 1)
    {
        var y = (dir > 0) ? yhi - i : ylo + i;
        if (insideSegs(segs, x, y))
        {
            var a = y;
            var b = outside;
            for (var k = 0; k < 24; k += 1)
            {
                var m = (a + b) / 2;
                if (insideSegs(segs, x, m))
                {
                    a = m;
                }
                else
                {
                    b = m;
                }
            }
            var edge = a;
            if (border > 0)
            {
                for (var q = 0; q < 200; q += 1)
                {
                    var ny = edge + dir * 0.5;
                    if (signedDist(segs, x, ny) - border <= 0)
                    {
                        edge = ny;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return edge;
        }
        outside = y;
    }
    return undefined;
}

function cellHasMaterial(segs is array, border is number, bb is map, xa, xb, ya, yb) returns boolean
{
    var x0 = (xa == undefined) ? bb.x0 : max(xa, bb.x0);
    var x1 = (xb == undefined) ? bb.x1 : min(xb, bb.x1);
    var y0 = (ya == undefined) ? bb.y0 : max(ya, bb.y0);
    var y1 = (yb == undefined) ? bb.y1 : min(yb, bb.y1);
    if (x1 <= x0 || y1 <= y0)
        return false;
    for (var x = x0 + 0.1; x <= x1; x += 5)
    {
        for (var y = y0 + 0.1; y <= y1; y += 5)
        {
            if (border > 0)
            {
                if (signedDist(segs, x, y) <= border)
                    return true;
            }
            else if (insideSegs(segs, x, y))
                return true;
        }
    }
    return false;
}

function inCell(x is number, y is number, xa, xb, ya, yb) returns boolean
{
    return (xa == undefined || x >= xa) && (xb == undefined || x <= xb) && (ya == undefined || y >= ya) && (yb == undefined || y <= yb);
}

// ------------------------------------------------------------------ holes

// A hole is kept only if its outer edge stays `margin` away from the final
// board edge. The final board edge lies `border` outside the silhouette.
function computeHoles(segs is array, bb is map, p is number, gx is number, gy is number, r is number, margin is number, border is number) returns array
{
    var holes = [];
    var i0 = floor((bb.x0 - gx) / p) - 1;
    var i1 = ceil((bb.x1 - gx) / p) + 1;
    var j0 = floor((bb.y0 - gy) / p) - 1;
    var j1 = ceil((bb.y1 - gy) / p) + 1;
    for (var j = j0; j <= j1; j += 1)
    {
        for (var i = i0; i <= i1; i += 1)
        {
            var x = gx + i * p;
            var y = gy + j * p;
            if (x < bb.x0 || x > bb.x1 || y < bb.y0 || y > bb.y1)
                continue;
            var distToEdge = border - signedDist(segs, x, y);
            if (distToEdge >= margin + r - 1e-6)
                holes = append(holes, [x, y, i, j]);
        }
    }
    return holes;
}

function pickHeaderPegs(holes is array, span is number, p is number, cx is number)
{
    var present = {};
    for (var h in holes)
        present[tkey(h[2], h[3])] = true;
    var best = undefined;
    var bestJ = -1e9;
    var bestDx = 1e12;
    for (var h in holes)
    {
        if (present[tkey(h[2] + span, h[3])] == undefined)
            continue;
        var dx = abs(h[0] + span * p / 2 - cx);
        if (h[3] > bestJ || (h[3] == bestJ && dx < bestDx))
        {
            bestJ = h[3];
            bestDx = dx;
            best = { "x1" : h[0], "x2" : h[0] + span * p, "y" : h[1] };
        }
    }
    return best;
}

// ----------------------------------------------------------- tiling / joints

// Seam coordinates are snapped to the midlines between hole rows/columns so
// that neither seams nor joints can ever touch a peg hole.
function seamPositions(lo is number, hi is number, cellMax is number, gridOrigin is number, p is number) returns array
{
    var len = hi - lo;
    if (len <= cellMax)
        return [];
    var n = ceil(len / (cellMax - p));
    var seams = [];
    for (var k = 1; k < n; k += 1)
    {
        var target = lo + len * k / n;
        var m = round((target - gridOrigin) / p - 0.5);
        seams = append(seams, gridOrigin + (m + 0.5) * p);
    }
    return seams;
}

// dir 0: vertical seam at x = seamPos, joints spread along Y.
// dir 1: horizontal seam at y = seamPos, joints spread along X.
function seamJoints(segs is array, border is number, dir is number, seamPos is number, lo is number, hi is number,
        gridAlong is number, p is number, acrossHalf is number, alongHalf is number, spacing is number) returns array
{
    var e = max(acrossHalf, alongHalf) + 4;
    var cands = [];
    var m0 = floor((lo + e - gridAlong) / p - 0.5);
    var m1 = ceil((hi - e - gridAlong) / p - 0.5);
    for (var m = m0; m <= m1; m += 1)
    {
        var c = gridAlong + (m + 0.5) * p;
        if (c < lo + e - 1e-6 || c > hi - e + 1e-6)
            continue;
        var ok = true;
        for (var sa = -1; sa <= 1; sa += 1)
        {
            for (var sb = -1; sb <= 1; sb += 1)
            {
                var px = (dir == 0) ? seamPos + sa * acrossHalf : c + sb * alongHalf;
                var py = (dir == 0) ? c + sb * alongHalf : seamPos + sa * acrossHalf;
                if (signedDist(segs, px, py) - border > -3)
                    ok = false;
            }
        }
        if (ok)
            cands = append(cands, c);
    }
    if (size(cands) == 0)
        return [];
    var len = hi - lo;
    var count = max(1, round(len / spacing));
    var chosen = [];
    for (var k = 0; k < count; k += 1)
    {
        var target = lo + len * (k + 0.5) / count;
        var best = cands[0];
        for (var cc in cands)
        {
            if (abs(cc - target) < abs(best - target))
                best = cc;
        }
        var dup = false;
        for (var ch in chosen)
        {
            if (abs(ch - best) < 1e-6)
                dup = true;
        }
        if (!dup)
            chosen = append(chosen, best);
    }
    return chosen;
}

// Joint-local (u across the seam, v along the seam) to global XY.
function toGlobal(jt is map, u is number, v is number) returns array
{
    return (jt.dir == 0) ? [jt.x + u, jt.y + v] : [jt.x + v, jt.y + u];
}

// Male dovetail tab (grow = 0) or the matching socket (grow = tolerance).
// Wider at the tip than at the neck, so it cannot be pulled out in-plane.
function dovetailPoly(jt is map, d is number, nh is number, hh is number, grow is number, rootLap is number) returns array
{
    var slope = (hh - nh) / d;
    var w0 = nh + grow * sqrt(1 + slope * slope);
    var tip = d + grow;
    var w1 = w0 + slope * tip;
    var loc = [[-rootLap, -w0], [0, -w0], [tip, -w1], [tip, w1], [0, w0], [-rootLap, w0]];
    var out = [];
    for (var q in loc)
        out = append(out, toGlobal(jt, q[0], q[1]));
    return out;
}

// Double dovetail ("butterfly") key, or its pocket when grow = tolerance.
function keyPoly(jt is map, d is number, nh is number, hh is number, grow is number) returns array
{
    var slope = (hh - nh) / d;
    var w0 = nh + grow * sqrt(1 + slope * slope);
    var tip = d + grow;
    var w1 = w0 + slope * tip;
    var loc = [[-tip, -w1], [0, -w0], [tip, -w1], [tip, w1], [0, w0], [-tip, w1]];
    var out = [];
    for (var q in loc)
        out = append(out, toGlobal(jt, q[0], q[1]));
    return out;
}

// ------------------------------------------------------------ profiles

function archPolygon(W is number, H is number, R is number) returns array
{
    var d = R - W / 2;
    var rise = sqrt(R * R - d * d);
    var yS = H - rise;
    var th = atan2(rise, d) / radian;
    var n = max(8, ceil(th / 0.03));
    var pts = [[-W / 2, 0], [W / 2, 0]];
    var cxR = W / 2 - R;
    for (var i = 0; i <= n; i += 1)
    {
        var a = th * i / n;
        pts = append(pts, [cxR + R * cosR(a), yS + R * sinR(a)]);
    }
    var cxL = R - W / 2;
    for (var i = 1; i <= n; i += 1)
    {
        var a = PI_ - th + th * i / n;
        pts = append(pts, [cxL + R * cosR(a), yS + R * sinR(a)]);
    }
    return pts;
}

function coffinPolygon(W is number, H is number) returns array
{
    var sy = 0.68 * H;
    return [[-0.15 * W, 0], [0.15 * W, 0], [0.5 * W, sy], [0.25 * W, H], [-0.25 * W, H], [-0.5 * W, sy]];
}

// Unit crescent: outer circle radius 1 minus an offset inner circle.
function crescentPolygon() returns array
{
    var R = 1;
    var ri = 0.78;
    var s = 0.36;
    var xs = (s * s + R * R - ri * ri) / (2 * s);
    var ys = sqrt(R * R - xs * xs);
    var alpha = atan2(ys, xs) / radian;
    var n = 90;
    var pts = [];
    for (var i = 0; i <= n; i += 1)
    {
        var a = alpha + (2 * PI_ - 2 * alpha) * i / n;
        pts = append(pts, [R * cosR(a), R * sinR(a)]);
    }
    var aBot = atan2(-ys, xs - s) / radian;
    var aTop = atan2(ys, xs - s) / radian;
    var sweep = (aTop - 2 * PI_) - aBot;
    for (var i = 1; i < n; i += 1)
    {
        var a = aBot + sweep * i / n;
        pts = append(pts, [s + ri * cosR(a), ri * sinR(a)]);
    }
    return pts;
}

function ptKey(x is number, y is number) returns string
{
    return toString(floor(x * 1000 + 0.5)) ~ "," ~ toString(floor(y * 1000 + 0.5));
}

// Outline of a cluster of pointy-top hexagons (axial coords, circumradius 1).
// Every cell contributes 6 counter-clockwise edges; edges shared by two cells
// cancel, the rest chain into the outer loop.
function hexClusterOutline(cells is array) returns array
{
    var sq3 = sqrt(3);
    var edges = {};
    for (var c in cells)
    {
        var cx = sq3 * (c[0] + c[1] / 2);
        var cy = 1.5 * c[1];
        var corners = [];
        for (var k = 0; k < 6; k += 1)
        {
            var ang = (30 + 60 * k) * degree;
            corners = append(corners, [cx + cos(ang), cy + sin(ang)]);
        }
        for (var k = 0; k < 6; k += 1)
        {
            var a = corners[k];
            var b = corners[(k + 1) % 6];
            edges[ptKey(a[0], a[1]) ~ ">" ~ ptKey(b[0], b[1])] = [a[0], a[1], b[0], b[1]];
        }
    }
    var next = {};
    var start = undefined;
    for (var key, e in edges)
    {
        var rev = ptKey(e[2], e[3]) ~ ">" ~ ptKey(e[0], e[1]);
        if (edges[rev] == undefined)
        {
            next[ptKey(e[0], e[1])] = [e[2], e[3]];
            start = [e[0], e[1]];
        }
    }
    var outline = [];
    var cur = start;
    for (var i = 0; i < 400; i += 1)
    {
        outline = append(outline, cur);
        var nxt = next[ptKey(cur[0], cur[1])];
        if (nxt == undefined)
            break;
        cur = nxt;
        if (ptKey(cur[0], cur[1]) == ptKey(start[0], start[1]))
            break;
    }
    return outline;
}

// Uniformly scale a polygon to fit W x H, centred on X = 0, sitting on Y = 0.
function fitUniform(pts is array, W is number, H is number) returns array
{
    var b = bboxOf(pts);
    var s = min(W / (b.x1 - b.x0), H / (b.y1 - b.y0));
    var cx = (b.x0 + b.x1) / 2;
    var out = [];
    for (var q in pts)
        out = append(out, [(q[0] - cx) * s, (q[1] - b.y0) * s]);
    return out;
}

// Sample every edge of the silhouette face. Returns the segments used for the
// signed-distance field and the sample points used as border-disc centres.
function sampleFaceEdges(context is Context, face is Query, step is number) returns map
{
    var segs = [];
    var pts = [];
    for (var e in evaluateQuery(context, qAdjacent(face, AdjacencyType.EDGE, EntityType.EDGE)))
    {
        var len = evLength(context, { "entities" : e }) / millimeter;
        var n = max(2, ceil(len / step));
        var params = [];
        for (var i = 0; i <= n; i += 1)
            params = append(params, i / n);
        var lines = evEdgeTangentLines(context, { "edge" : e, "parameters" : params });
        var prev = undefined;
        for (var ln in lines)
        {
            var q = ln.origin / millimeter;
            var cur = [q[0], q[1]];
            if (prev != undefined)
                segs = append(segs, [prev[0], prev[1], cur[0], cur[1]]);
            pts = append(pts, cur);
            prev = cur;
        }
    }
    if (size(segs) == 0)
        throw regenError("Could not read any edges from the silhouette region.");
    return { "segs" : segs, "pts" : pts };
}

// --------------------------------------------------------- solid builders

function zPlane(z is number) returns Plane
{
    return plane(vector(0, 0, z) * millimeter, vector(0, 0, 1), vector(1, 0, 0));
}

// Extrude closed polygon loops and/or circles ([x, y, r]) drawn at height z0
// by `depth` along +Z. Returns the new body(ies). All numbers are mm.
function prism(context is Context, id is Id, z0 is number, depth is number, loops is array, circles is array) returns Query
{
    var sk = newSketchOnPlane(context, id + "sk", { "sketchPlane" : zPlane(z0) });
    for (var li = 0; li < size(loops); li += 1)
    {
        var pts = loops[li];
        var n = size(pts);
        for (var i = 0; i < n; i += 1)
        {
            var a = pts[i];
            var b = pts[(i + 1) % n];
            if (hyp(b[0] - a[0], b[1] - a[1]) > 1e-4)
                skLineSegment(sk, "l" ~ li ~ "_" ~ i, { "start" : vector(a[0], a[1]) * millimeter, "end" : vector(b[0], b[1]) * millimeter });
        }
    }
    for (var ci = 0; ci < size(circles); ci += 1)
    {
        var c = circles[ci];
        skCircle(sk, "c" ~ ci, { "center" : vector(c[0], c[1]) * millimeter, "radius" : c[2] * millimeter });
    }
    skSolve(sk);
    opExtrude(context, id + "ex", {
                "entities" : qSketchRegion(id + "sk"),
                "direction" : vector(0, 0, 1),
                "endBound" : BoundingType.BLIND,
                "endDepth" : depth * millimeter
            });
    opDeleteBodies(context, id + "del", { "entities" : qCreatedBy(id + "sk", EntityType.BODY) });
    return qCreatedBy(id + "ex", EntityType.BODY);
}

function subtractFrom(context is Context, id is Id, target is Query, tools is Query)
{
    opBoolean(context, id, { "tools" : tools, "targets" : target, "operationType" : BooleanOperationType.SUBTRACTION });
}

// Builds the un-clipped, hole-free board (silhouette + border + eyelets).
// Rebuilt per tile so each tile starts from its own independent body.
function buildMaster(context is Context, id is Id, G is map) returns Query
{
    var t = G.t;
    var master = qNothing();
    if (G.kind == "poly")
    {
        master = prism(context, id + "base", 0, t, [G.poly], []);
    }
    else
    {
        // Silhouette + a disc of radius `border` on every boundary sample =
        // the silhouette offset outward by `border` with rounded corners.
        // Discs are split into interleaved groups so that discs inside one
        // sketch rarely overlap (keeps the sketch regions simple).
        opExtrude(context, id + "sil", {
                    "entities" : G.logoFace,
                    "direction" : vector(0, 0, 1),
                    "endBound" : BoundingType.BLIND,
                    "endDepth" : t * millimeter
                });
        var parts = [qCreatedBy(id + "sil", EntityType.BODY)];
        for (var g = 0; g < G.groups; g += 1)
        {
            var circles = [];
            for (var k = g; k < size(G.centers); k += G.groups)
                circles = append(circles, [G.centers[k][0], G.centers[k][1], G.border]);
            if (size(circles) > 0)
                parts = append(parts, prism(context, id + ("ring" ~ g), 0, t, [], circles));
        }
        opBoolean(context, id + "ringU", { "tools" : qUnion(parts), "operationType" : BooleanOperationType.UNION });
        master = qUnion(parts);
    }

    if (size(G.lugs) > 0)
    {
        var lugLoops = [];
        var lugCircles = [];
        var holeCircles = [];
        for (var lg in G.lugs)
        {
            lugLoops = append(lugLoops, [[lg.x - lg.ro, lg.cy], [lg.x + lg.ro, lg.cy], [lg.x + lg.bw, lg.yb], [lg.x - lg.bw, lg.yb]]);
            lugCircles = append(lugCircles, [lg.x, lg.cy, lg.ro]);
            holeCircles = append(holeCircles, [lg.x, lg.cy, lg.ri]);
        }
        var lugQ = prism(context, id + "lug", 0, t, lugLoops, lugCircles);
        opBoolean(context, id + "lugU", { "tools" : qUnion([master, lugQ]), "operationType" : BooleanOperationType.UNION });
        master = qUnion([master, lugQ]);
        subtractFrom(context, id + "lugH", master, prism(context, id + "lugHole", -1, t + 2, [], holeCircles));
    }
    return master;
}

// Place items in rows on a flat "print sheet" starting at (originX, originY).
function layoutItems(context is Context, id is Id, items is array, originX is number, originY is number, rowW is number, gap is number)
{
    var cx = 0;
    var cy = 0;
    var rowD = 0;
    for (var k = 0; k < size(items); k += 1)
    {
        var item = items[k];
        var lid = id + ("lay" ~ k);
        if (item.printRot != undefined)
            opTransform(context, lid + "rot", { "bodies" : item.q, "transform" : item.printRot });
        var box = evBox3d(context, { "topLevelId" : id, "entities" : item.q });
        var mn = box.minCorner / millimeter;
        var mx = box.maxCorner / millimeter;
        var w = mx[0] - mn[0];
        var dd = mx[1] - mn[1];
        if (cx > 0 && cx + w > rowW)
        {
            cx = 0;
            cy += rowD + gap;
            rowD = 0;
        }
        opTransform(context, lid + "mv", {
                    "bodies" : item.q,
                    "transform" : transform(vector(originX + cx - mn[0], originY + cy - mn[1], -mn[2]) * millimeter)
                });
        cx += w + gap;
        rowD = max(rowD, dd);
    }
}
