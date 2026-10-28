FeatureScript 3095;
import(path : "onshape/std/common.fs", version : "3095.0");

/**
 * Flip-Top Carton Fidget  (Onshape FeatureScript)
 * ===============================================
 * A cigarette-carton-styled fidget: a 2 x 6 block of mini soda cans fused onto
 * one plate that acts as a single big button on ONE MX mechanical switch.
 *
 *   Part 1  Carton: box + print-in-place knuckle/pin hinge + flip-top lid with
 *           magnet snap + MX switch socket + guide grooves.
 *   Part 2  Can block: plate + 12 cans + side guide ribs + MX keycap stem.
 *
 * COORDINATES   X = long side, Y = short side (front = -Y, hinge at the +Y rear),
 *               Z up.  The carton is centred on X/Y; Z = 0 is the bottom of the feet.
 *
 * HOW THE HEIGHTS ARE DERIVED (nothing here is typed in by hand)
 *   floor top  = footHeight + floorThickness
 *   switch     = housing top, travel and stem engagement are parameters below
 *   post       = free end rests (housingTop + travel + switchClearance) above the
 *                floor top, so it never reaches the housing at full travel
 *   plate      = underside sits one post length above that
 *   rim        = can-top + lidRestClearance, i.e. the cans always sit
 *                lidRestClearance below the upper rim of the carton body.
 *
 * PRINT / ASSEMBLY NOTES
 *   * Lid angle defaults to 90 degrees: the lid is printed standing open behind
 *     the body, so the hinge needs no supports.  Set 0 to see it closed.
 *   * The can block drops in from the top (the guide grooves are open at the top)
 *     and is held by friction on the switch stem.  Fit it before closing the lid.
 *   * Magnets: glue them in with matching faces attracting; mark one carton/lid
 *     pair so the polarity is always the same way round.
 *   * Switch dimensions are typical Cherry MX values; check them against your
 *     switch's datasheet and edit the constants.
 *   * Checked in Onshape (library 3095): builds three solids (carton, lid, block)
 *     and the lid, carton and block do not collide with the lid closed, at 90
 *     degrees, or part-way.  Not checked: the fit of the MX switch, stem and
 *     magnets, or print results.  The ribs are only as tall as the plate, so
 *     the block can rock a little in the channels; make plateThickness larger
 *     if it tilts.
 */

// ============================================================================
// CONFIGURATION  (edit these)
// ============================================================================

// ---- Requested parameters ---------------------------------------------------
const wallThickness      = 2.0 * millimeter;
const perimeterClearance = 0.5 * millimeter;  // gap between can block & inner carton walls
const hingeClearance     = 0.3 * millimeter;  // print-in-place gap
const magnetDiameter     = 4.2 * millimeter;  // 4 mm magnet + 0.2 mm tolerance
const magnetDepth        = 2.1 * millimeter;  // 2 mm magnet + 0.1 mm tolerance
const lidRestClearance   = 1.5 * millimeter;  // distance top of cans sits below the rim

// ---- Can block --------------------------------------------------------------
const canColumns   = 6;                       // along X
const canRows      = 2;                       // along Y
const canDiameter  = 13.0 * millimeter;
const canHeight    = 16.0 * millimeter;       // above the plate
const canPitch     = 15.0 * millimeter;       // centre-to-centre
const plateEdge    = 2.0 * millimeter;        // plate margin beyond the outer cans
const plateThickness = 3.0 * millimeter;

// ---- Guide ribs (block) and channels (carton walls) --------------------------
const ribWidth         = 1.6 * millimeter;
const channelDepth     = 0.6 * millimeter;    // groove cut into the 2 mm wall
const channelSlack     = 0.25 * millimeter;   // clearance on each side of a rib

// ---- MX switch (typical Cherry MX; verify) ----------------------------------
const socketSize          = 14.0 * millimeter; // square hole
const floorThickness      = 1.5 * millimeter;  // = MX plate thickness, so the clips latch
const switchBelowFloor    = 5.0 * millimeter;  // switch body hanging under the floor
const switchHousingTop    = 5.0 * millimeter;  // housing top above the floor top
const switchTravel        = 4.0 * millimeter;
const switchClearance     = 0.5 * millimeter;  // block never bottoms on the housing
const stemEngagement      = 3.6 * millimeter;  // depth of the cross socket
const stemOuterDiameter   = 5.5 * millimeter;
const crossLength         = 4.15 * millimeter; // + socket (4.0 stem + 0.15)
const crossWidth          = 1.35 * millimeter; // 1.3 stem arm + 0.05

// ---- Hinge ------------------------------------------------------------------
const hingeLength    = 60.0 * millimeter;
const hingeKnuckles  = 5;                     // odd: body, lid, body, lid, body
const knuckleRadius  = 3.5 * millimeter;
const pinRadius      = 1.5 * millimeter;

// ---- Lid and magnet lugs ----------------------------------------------------
const lugWidth       = 9.0 * millimeter;
const lugSkin        = 1.2 * millimeter;      // material behind each magnet
const lidRimWidth    = 1.0 * millimeter;
const lidRimDepth    = 1.0 * millimeter;      // < lidRestClearance so it never touches cans

export enum BuildPart
{
    annotation { "Name" : "Carton and can block" }
    BOTH,
    annotation { "Name" : "Carton only" }
    CARTON,
    annotation { "Name" : "Can block only" }
    BLOCK
}

export enum BlockPose
{
    annotation { "Name" : "Assembled (resting on switch)" }
    ASSEMBLED,
    annotation { "Name" : "Plate underside on the build plane (print / UV)" }
    ON_BED
}

export const LID_ANGLE_BOUNDS = { (degree) : [0, 90, 120] } as AngleBoundSpec;

annotation { "Feature Type Name" : "Flip-Top Carton Fidget" }
export const fidgetCarton = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Build" }
        definition.buildPart is BuildPart;

        annotation { "Name" : "Lid angle (90 = print pose, 0 = closed)" }
        isAngle(definition.lidAngle, LID_ANGLE_BOUNDS);

        annotation { "Name" : "Can block pose" }
        definition.blockPose is BlockPose;
    }
    {
        var c = cfg();

        if (definition.buildPart != BuildPart.BLOCK)
        {
            buildCarton(context, id + "carton", c);
            buildLid(context, id + "lid", c, definition.lidAngle / radian);
        }
        if (definition.buildPart != BuildPart.CARTON)
        {
            buildBlock(context, id + "block", c, definition.blockPose == BlockPose.ON_BED);
        }
    }, {
        buildPart : BuildPart.BOTH,
        lidAngle : 90 * degree,
        blockPose : BlockPose.ASSEMBLED
    });

// ============================================================================
// Derived dimensions (plain numbers, millimetres)
// ============================================================================

function cfg() returns map
{
    var c = {};
    c.wall = wallThickness / millimeter;
    c.clr = perimeterClearance / millimeter;
    c.hc = hingeClearance / millimeter;
    c.magD = magnetDiameter / millimeter;
    c.magH = magnetDepth / millimeter;
    c.lidRest = lidRestClearance / millimeter;
    c.canD = canDiameter / millimeter;
    c.canH = canHeight / millimeter;
    c.pitch = canPitch / millimeter;
    c.plateT = plateThickness / millimeter;
    c.floorT = floorThickness / millimeter;
    c.ribW = ribWidth / millimeter;
    c.chD = channelDepth / millimeter;
    c.chSlack = channelSlack / millimeter;

    // Can block footprint -> carton cavity -> carton outline.
    c.blockX = (canColumns - 1) * c.pitch + c.canD + 2 * plateEdge / millimeter;
    c.blockY = (canRows - 1) * c.pitch + c.canD + 2 * plateEdge / millimeter;
    c.innerX = c.blockX + 2 * c.clr;
    c.innerY = c.blockY + 2 * c.clr;
    c.outerX = c.innerX + 2 * c.wall;
    c.outerY = c.innerY + 2 * c.wall;

    // Vertical stack-up.
    c.footH = (switchBelowFloor / millimeter) + 1.0;          // feet keep the switch off the table
    c.floorTop = c.footH + c.floorT;
    var freeEndRest = (switchHousingTop + switchTravel + switchClearance) / millimeter;
    c.postLen = (stemEngagement / millimeter) + 0.9;           // 0.9 mm ceiling above the cross socket
    // The post's free end must stay above the housing even at full travel, so the
    // plate underside sits one post length higher still.
    c.plateUnder = c.floorTop + freeEndRest + c.postLen;       // plate underside, block at rest
    c.plateTop = c.plateUnder + c.plateT;
    c.canTop = c.plateTop + c.canH;
    c.zRim = c.canTop + c.lidRest;                             // cans sit lidRest below the rim

    // Magnet lugs (outward bosses on the rim) and lid.
    c.lugOut = c.magD + 2 * (lugSkin / millimeter);
    c.lugW = lugWidth / millimeter;
    c.lugH = c.magH + (lugSkin / millimeter);
    c.lidT = c.lugH;
    c.lidBottom = c.zRim + c.hc;

    // Hinge axis: behind the rear face, just above the rim.
    c.yRear = c.outerY / 2;
    c.knR = knuckleRadius / millimeter;
    c.pinR = pinRadius / millimeter;
    c.yA = c.yRear + c.knR;
    c.zA = c.lidBottom + c.knR;
    return c;
}

/** Centres of the four magnet pockets: [x, y, side] with side 0 = front lug, 1 = +X, 2 = -X. */
function magnetSites(c is map) returns array
{
    var yFront = -c.outerY / 2;
    var xs = c.outerX / 2;
    var fx = c.outerX / 2 - 16;
    var sy = yFront + 12;
    return [[-fx, yFront - c.lugOut / 2, 0], [fx, yFront - c.lugOut / 2, 0],
            [xs + c.lugOut / 2, sy, 1], [-xs - c.lugOut / 2, sy, 2]];
}

/** Plan rectangle [x0, x1, y0, y1] of the lug at a magnet site (overlaps 1 mm into the wall). */
function lugRect(c is map, s is array) returns array
{
    if (s[2] == 0)
        return [s[0] - c.lugW / 2, s[0] + c.lugW / 2, -c.outerY / 2 - c.lugOut, -c.outerY / 2 + 1];
    if (s[2] == 1)
        return [c.outerX / 2 - 1, c.outerX / 2 + c.lugOut, s[1] - c.lugW / 2, s[1] + c.lugW / 2];
    return [-c.outerX / 2 - c.lugOut, -c.outerX / 2 + 1, s[1] - c.lugW / 2, s[1] + c.lugW / 2];
}

// ============================================================================
// Primitive builders (all take millimetre numbers)
// ============================================================================

function mm3(x is number, y is number, z is number) returns Vector
{
    return vector(x, y, z) * millimeter;
}

/** Solid bodies created under an id prefix (sketch surfaces excluded). */
function solidsOf(id is Id) returns Query
{
    return qBodyType(qCreatedBy(id, EntityType.BODY), BodyType.SOLID);
}

/** Axis-aligned box: sketch a rectangle on a horizontal plane and extrude it up. */
function boxMM(context is Context, id is Id, x0 is number, x1 is number, y0 is number, y1 is number,
               z0 is number, z1 is number)
{
    var sk = newSketchOnPlane(context, id + "sk", { "sketchPlane" : plane(mm3(0, 0, z0), vector(0, 0, 1), vector(1, 0, 0)) });
    skRectangle(sk, "r", { "firstCorner" : vector(x0, y0) * millimeter, "secondCorner" : vector(x1, y1) * millimeter });
    skSolve(sk);
    opExtrude(context, id + "ext", { "entities" : qSketchRegion(id + "sk"), "direction" : vector(0, 0, 1),
                                      "endBound" : BoundingType.BLIND, "endDepth" : (z1 - z0) * millimeter });
    opDeleteBodies(context, id + "skDel", { "entities" : qCreatedBy(id + "sk", EntityType.BODY) });
}

/** Vertical cylinder at (x, y) from z0 to z1. */
function cylZ(context is Context, id is Id, x is number, y is number, z0 is number, z1 is number, r is number)
{
    var sk = newSketchOnPlane(context, id + "sk", { "sketchPlane" : plane(mm3(0, 0, z0), vector(0, 0, 1), vector(1, 0, 0)) });
    skCircle(sk, "c", { "center" : vector(x, y) * millimeter, "radius" : r * millimeter });
    skSolve(sk);
    opExtrude(context, id + "ext", { "entities" : qSketchRegion(id + "sk"), "direction" : vector(0, 0, 1),
                                      "endBound" : BoundingType.BLIND, "endDepth" : (z1 - z0) * millimeter });
    opDeleteBodies(context, id + "skDel", { "entities" : qCreatedBy(id + "sk", EntityType.BODY) });
}

/** Cylinder along X (hinge knuckles and pin), axis through (y, z), from x0 to x1. */
function cylX(context is Context, id is Id, x0 is number, x1 is number, y is number, z is number, r is number)
{
    // Plane normal +X with sketch x = Y and sketch y = Z.
    var sk = newSketchOnPlane(context, id + "sk", { "sketchPlane" : plane(mm3(x0, 0, 0), vector(1, 0, 0), vector(0, 1, 0)) });
    skCircle(sk, "c", { "center" : vector(y, z) * millimeter, "radius" : r * millimeter });
    skSolve(sk);
    opExtrude(context, id + "ext", { "entities" : qSketchRegion(id + "sk"), "direction" : vector(1, 0, 0),
                                      "endBound" : BoundingType.BLIND, "endDepth" : (x1 - x0) * millimeter });
    opDeleteBodies(context, id + "skDel", { "entities" : qCreatedBy(id + "sk", EntityType.BODY) });
}

/** x-range [x0, x1] of hinge knuckle i (consecutive knuckles are hingeClearance apart). */
function knuckleRange(c is map, i is number) returns array
{
    var len = (hingeLength / millimeter - (hingeKnuckles - 1) * c.hc) / hingeKnuckles;
    var x0 = -hingeLength / millimeter / 2 + i * (len + c.hc);
    return [x0, x0 + len];
}

// ============================================================================
// Part 1a - carton body
// ============================================================================

function buildCarton(context is Context, id is Id, c is map)
{
    var pos = [];
    var oX = c.outerX / 2;
    var oY = c.outerY / 2;

    // ---- Positive features (all built first so ids stay contiguous) --------
    // Outer shell from the underside of the floor up to the rim.
    boxMM(context, id + "p" + "shell", -oX, oX, -oY, oY, c.footH, c.zRim);
    pos = append(pos, solidsOf(id + "p" + "shell"));

    // Four feet so the switch body hanging under the floor never touches the table.
    var fi = 0;
    for (var sx in [-1, 1])
    {
        for (var sy in [-1, 1])
        {
            cylZ(context, id + "p" + ("foot" ~ fi), sx * (oX - 8), sy * (oY - 8), 0, c.footH + 0.5, 4);
            pos = append(pos, solidsOf(id + "p" + ("foot" ~ fi)));
            fi += 1;
        }
    }

    // Magnet lugs: outward bosses on the top rim that hold the magnets.
    var sites = magnetSites(c);
    for (var i = 0; i < size(sites); i += 1)
    {
        var r = lugRect(c, sites[i]);
        boxMM(context, id + "p" + ("lug" ~ i), r[0], r[1], r[2], r[3], c.zRim - c.lugH, c.zRim);
        pos = append(pos, solidsOf(id + "p" + ("lug" ~ i)));
    }

    // Hinge, body half: knuckles 0, 2, 4 with their webs, plus the pin through all five.
    cylX(context, id + "p" + "pin", -hingeLength / millimeter / 2, hingeLength / millimeter / 2, c.yA, c.zA, c.pinR);
    pos = append(pos, solidsOf(id + "p" + "pin"));
    for (var k = 0; k < hingeKnuckles; k += 2)
    {
        var kr = knuckleRange(c, k);
        cylX(context, id + "p" + ("kn" ~ k), kr[0], kr[1], c.yA, c.zA, c.knR);
        pos = append(pos, solidsOf(id + "p" + ("kn" ~ k)));
        // Web from the rear wall up to the axis, behind the lid's rear edge.
        boxMM(context, id + "p" + ("web" ~ k), kr[0], kr[1], c.yRear, c.yA, c.zRim - 6, c.zA);
        pos = append(pos, solidsOf(id + "p" + ("web" ~ k)));
        // Tie into the rear wall (overlaps 1 mm into it, below the rim only).
        boxMM(context, id + "p" + ("tie" ~ k), kr[0], kr[1], c.yRear - 1, c.yRear + 0.1, c.zRim - 6, c.zRim);
        pos = append(pos, solidsOf(id + "p" + ("tie" ~ k)));
    }
    opBoolean(context, id + "p" + "union", { "tools" : qUnion(pos), "operationType" : BooleanOperationType.UNION });

    // ---- Negative features ----------------------------------------------------
    var neg = [];
    // Cavity: the open top bay, floor at floorTop.
    boxMM(context, id + "n" + "cavity", -c.innerX / 2, c.innerX / 2, -c.innerY / 2, c.innerY / 2, c.floorTop, c.zRim + 1);
    neg = append(neg, solidsOf(id + "n" + "cavity"));

    // Centred 14 x 14 MX switch socket through the floor.
    var h = socketSize / millimeter / 2;
    boxMM(context, id + "n" + "socket", -h, h, -h, h, c.footH - 1, c.floorTop + 1);
    neg = append(neg, solidsOf(id + "n" + "socket"));

    // Guide channels: grooves in the inner walls, open at the top.  Two per
    // long wall, one per short wall.
    var gw = c.ribW / 2 + c.chSlack;
    var gi = 0;
    for (var sy in [-1, 1])
    {
        for (var gx in [-c.blockX / 4, c.blockX / 4])
        {
            var yIn = sy * c.innerY / 2;
            boxMM(context, id + "n" + ("gy" ~ gi), gx - gw, gx + gw, min(yIn, yIn + sy * c.chD), max(yIn, yIn + sy * c.chD),
                  c.floorTop, c.zRim + 1);
            neg = append(neg, solidsOf(id + "n" + ("gy" ~ gi)));
            gi += 1;
        }
    }
    for (var sx in [-1, 1])
    {
        var xIn = sx * c.innerX / 2;
        boxMM(context, id + "n" + ("gx" ~ gi), min(xIn, xIn + sx * c.chD), max(xIn, xIn + sx * c.chD), -gw, gw,
              c.floorTop, c.zRim + 1);
        neg = append(neg, solidsOf(id + "n" + ("gx" ~ gi)));
        gi += 1;
    }

    // Magnet pockets, drilled down from the top of each lug.
    for (var i = 0; i < size(sites); i += 1)
    {
        cylZ(context, id + "n" + ("mag" ~ i), sites[i][0], sites[i][1], c.zRim - c.magH, c.zRim + 1, c.magD / 2);
        neg = append(neg, solidsOf(id + "n" + ("mag" ~ i)));
    }

    opBoolean(context, id + "n" + "cut", { "tools" : qUnion(neg), "targets" : solidsOf(id + "p"),
                                            "operationType" : BooleanOperationType.SUBTRACTION });
}

// ============================================================================
// Part 1b - flip-top lid (built closed, then rotated about the hinge axis)
// ============================================================================

function buildLid(context is Context, id is Id, c is map, angle is number)
{
    var pos = [];
    var oX = c.outerX / 2;
    var oY = c.outerY / 2;
    var zb = c.lidBottom;
    var zt = zb + c.lidT;

    // Lid plate.  Its rear edge stops hingeClearance short of the body's rear
    // face so the lid never rubs the body knuckles' webs.
    boxMM(context, id + "p" + "plate", -oX, oX, -oY, oY - c.hc, zb, zt);
    pos = append(pos, solidsOf(id + "p" + "plate"));

    // Matching magnet lugs, same plan position as the body's.
    var sites = magnetSites(c);
    for (var i = 0; i < size(sites); i += 1)
    {
        var r = lugRect(c, sites[i]);
        boxMM(context, id + "p" + ("lug" ~ i), r[0], r[1], r[2], r[3], zb, zt);
        pos = append(pos, solidsOf(id + "p" + ("lug" ~ i)));
    }

    // Inner rim: a thin ring dropping lidRimDepth into the cavity opening.
    // Built as outer box minus inner box.  Its depth is below lidRestClearance,
    // so it stays clear of the can tops.
    var rimOut = [c.innerX / 2 - c.hc, c.innerY / 2 - c.hc];
    boxMM(context, id + "p" + "ringA", -rimOut[0], rimOut[0], -rimOut[1], rimOut[1], zb - lidRimDepth / millimeter, zb + 0.2);
    boxMM(context, id + "p" + "ringB", -rimOut[0] + lidRimWidth / millimeter, rimOut[0] - lidRimWidth / millimeter,
          -rimOut[1] + lidRimWidth / millimeter, rimOut[1] - lidRimWidth / millimeter, zb - lidRimDepth / millimeter - 1, zb + 1);
    opBoolean(context, id + "p" + "ringCut", { "tools" : solidsOf(id + "p" + "ringB"), "targets" : solidsOf(id + "p" + "ringA"),
                                                "operationType" : BooleanOperationType.SUBTRACTION });
    pos = append(pos, solidsOf(id + "p" + "ringA"));

    // Hinge, lid half: knuckles 1 and 3 with webs back to the plate.
    for (var k = 1; k < hingeKnuckles; k += 2)
    {
        var kr = knuckleRange(c, k);
        cylX(context, id + "p" + ("kn" ~ k), kr[0], kr[1], c.yA, c.zA, c.knR);
        pos = append(pos, solidsOf(id + "p" + ("kn" ~ k)));
        boxMM(context, id + "p" + ("web" ~ k), kr[0], kr[1], oY - c.hc - 0.5, c.yA, zb, c.zA);
        pos = append(pos, solidsOf(id + "p" + ("web" ~ k)));
    }
    opBoolean(context, id + "p" + "union", { "tools" : qUnion(pos), "operationType" : BooleanOperationType.UNION });

    // ---- Negatives: magnet pockets (up from the underside) and the pin bore ----
    var neg = [];
    for (var i = 0; i < size(sites); i += 1)
    {
        cylZ(context, id + "n" + ("mag" ~ i), sites[i][0], sites[i][1], zb - 1, zb + c.magH, c.magD / 2);
        neg = append(neg, solidsOf(id + "n" + ("mag" ~ i)));
    }
    // Bore = pin radius + hingeClearance, over the full hinge length.  Only the
    // lid knuckles are in the way; the body's pin stays untouched because the
    // lid is a separate body.
    cylX(context, id + "n" + "bore", -hingeLength / millimeter / 2 - 1, hingeLength / millimeter / 2 + 1, c.yA, c.zA,
         c.pinR + c.hc);
    neg = append(neg, solidsOf(id + "n" + "bore"));
    opBoolean(context, id + "n" + "cut", { "tools" : qUnion(neg), "targets" : solidsOf(id + "p"),
                                            "operationType" : BooleanOperationType.SUBTRACTION });

    // ---- Swing the lid open: rotating by -angle about +X lifts the front edge ----
    if (abs(angle) > 1e-6)
    {
        opTransform(context, id + "rot", { "bodies" : solidsOf(id + "p"),
                                           "transform" : rotationAround(line(mm3(0, c.yA, c.zA), vector(1, 0, 0)), -angle * radian) });
    }
}

// ============================================================================
// Part 2 - can block
// ============================================================================

function buildBlock(context is Context, id is Id, c is map, onBed is boolean)
{
    var pos = [];
    var bX = c.blockX / 2;
    var bY = c.blockY / 2;
    var zU = c.plateUnder;

    // Plate: one flat solid, flat underside.
    boxMM(context, id + "p" + "plate", -bX, bX, -bY, bY, zU, c.plateTop);
    pos = append(pos, solidsOf(id + "p" + "plate"));

    // Cans: revolve one tapered-neck can profile around each grid position.
    // Profile (r, z above plate): base, shoulder taper, flat top.
    var R = c.canD / 2;
    // The profile starts 0.2 mm inside the plate so each can fuses to it.
    var prof = [[0, 0], [R, 0], [R, c.canH - 2.8], [R - 1.0, c.canH - 1.3], [R - 1.0, c.canH + 0.2], [0, c.canH + 0.2]];
    var ci = 0;
    for (var col = 0; col < canColumns; col += 1)
    {
        for (var row = 0; row < canRows; row += 1)
        {
            var cx = (col - (canColumns - 1) / 2) * c.pitch;
            var cy = (row - (canRows - 1) / 2) * c.pitch;
            canBody(context, id + "p" + ("can" ~ ci), cx, cy, c.plateTop - 0.2, prof);
            pos = append(pos, solidsOf(id + "p" + ("can" ~ ci)));
            ci += 1;
        }
    }

    // Side guide ribs: stand out far enough to ride in the wall channels with channelSlack to spare.
    var ribOut = c.clr + c.chD - c.chSlack;
    var rw = c.ribW / 2;
    var ri = 0;
    for (var sy in [-1, 1])
    {
        for (var rx in [-c.blockX / 4, c.blockX / 4])
        {
            boxMM(context, id + "p" + ("rib" ~ ri), rx - rw, rx + rw, min(sy * bY, sy * (bY + ribOut)) - 0.1,
                  max(sy * bY, sy * (bY + ribOut)) + 0.1, zU, c.plateTop);
            pos = append(pos, solidsOf(id + "p" + ("rib" ~ ri)));
            ri += 1;
        }
    }
    for (var sx in [-1, 1])
    {
        boxMM(context, id + "p" + ("rib" ~ ri), min(sx * bX, sx * (bX + ribOut)) - 0.1, max(sx * bX, sx * (bX + ribOut)) + 0.1,
              -rw, rw, zU, c.plateTop);
        pos = append(pos, solidsOf(id + "p" + ("rib" ~ ri)));
        ri += 1;
    }

    // MX keycap stem: a round post hanging from the middle of the underside.
    var zFree = zU - c.postLen;
    cylZ(context, id + "p" + "post", 0, 0, zFree, zU + 0.2, stemOuterDiameter / millimeter / 2);
    pos = append(pos, solidsOf(id + "p" + "post"));
    opBoolean(context, id + "p" + "union", { "tools" : qUnion(pos), "operationType" : BooleanOperationType.UNION });

    // ---- Cross-shaped socket in the post's free end (two overlapping slots) ----
    var cl = crossLength / millimeter / 2;
    var cw = crossWidth / millimeter / 2;
    var depth = stemEngagement / millimeter;
    boxMM(context, id + "n" + "crossA", -cl, cl, -cw, cw, zFree - 1, zFree + depth);
    boxMM(context, id + "n" + "crossB", -cw, cw, -cl, cl, zFree - 1, zFree + depth);
    opBoolean(context, id + "n" + "cut", { "tools" : qUnion([solidsOf(id + "n" + "crossA"), solidsOf(id + "n" + "crossB")]),
                                            "targets" : solidsOf(id + "p"),
                                            "operationType" : BooleanOperationType.SUBTRACTION });

    // UV / print pose: slide the whole block so the plate underside sits on z = 0.
    if (onBed)
    {
        opTransform(context, id + "pose", { "bodies" : solidsOf(id + "p"), "transform" : transform(mm3(0, 0, -zU)) });
    }
}

/** One can: a closed (r, z) polygon revolved about the vertical axis through (cx, cy). */
function canBody(context is Context, id is Id, cx is number, cy is number, z0 is number, prof is array)
{
    // Plane through the can axis: sketch x = X, sketch y = Z.
    var sk = newSketchOnPlane(context, id + "sk", { "sketchPlane" : plane(mm3(cx, cy, z0), vector(0, -1, 0), vector(1, 0, 0)) });
    var n = size(prof);
    for (var i = 0; i < n; i += 1)
    {
        var a = prof[i];
        var b = prof[(i + 1) % n];
        skLineSegment(sk, "l" ~ i, { "start" : vector(a[0], a[1]) * millimeter, "end" : vector(b[0], b[1]) * millimeter });
    }
    skSolve(sk);
    opRevolve(context, id + "rev", { "entities" : qSketchRegion(id + "sk"),
                                      "axis" : line(mm3(cx, cy, z0), vector(0, 0, 1)),
                                      "angleForward" : 2 * PI * radian });
    opDeleteBodies(context, id + "skDel", { "entities" : qCreatedBy(id + "sk", EntityType.BODY) });
}
