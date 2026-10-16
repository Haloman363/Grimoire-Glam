FeatureScript 2280;
import(path : "onshape/std/geometry.fs", version : "2280.0");

const NS_LENGTH_BOUNDS = { (millimeter) : [50, 75, 200] } as LengthBoundSpec;
const NS_HEIGHT_BOUNDS = { (millimeter) : [15, 25, 80] } as LengthBoundSpec;
const NS_THICKNESS_BOUNDS = { (millimeter) : [2, 6, 30] } as LengthBoundSpec;
const NS_KEYRING_BOUNDS = { (millimeter) : [2, 4.5, 10] } as LengthBoundSpec;

annotation { "Feature Type Name" : "Nail Saver Keychain Generator" }
export const nailSaverKeychain = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Tool length" }
        isLength(definition.toolLength, NS_LENGTH_BOUNDS);

        annotation { "Name" : "Tool height" }
        isLength(definition.toolHeight, NS_HEIGHT_BOUNDS);

        annotation { "Name" : "Body thickness" }
        isLength(definition.bodyThickness, NS_THICKNESS_BOUNDS);

        annotation { "Name" : "Keyring hole diameter" }
        isLength(definition.keyringHoleDiameter, NS_KEYRING_BOUNDS);
    }
    {
        const L = definition.toolLength;
        const H = definition.toolHeight;
        const mm0 = 0 * millimeter;

        // Fixed feature dimensions
        const noseHeight = 10 * millimeter;     // blunt pusher nose (full round, radius = noseHeight / 2)
        const slotWidth = 3 * millimeter;       // hook slot opening
        const slotDepth = 7 * millimeter;       // how far the slot climbs into the body
        const hookReach = 6 * millimeter;       // undercut pocket reaching toward the nose
        const gripDepth = 3 * millimeter;       // concave finger rest depth

        // Derived positions (all sketch X runs tail -> nose, sketch Y runs bottom -> up)
        const tailR = definition.keyringHoleDiameter / 2 + 3.5 * millimeter;  // tail radius around the keyring hole
        const noseBackX = L - noseHeight / 2;           // where the nose arc meets the bottom/top
        const slotX = 0.62 * L;                         // rear wall of the hook slot
        const gripW = 0.18 * L;
        const grip1Start = 0.17 * L;
        const grip1End = grip1Start + gripW;
        const grip2Start = 0.40 * L;
        const grip2End = grip2Start + gripW;
        const spineMidX = (tailR + noseBackX) / 2;

        // Sanity checks so the profile can never fold over itself
        if (H < 2 * tailR + 2 * millimeter)
            throw regenError("Tool height is too small for this keyring hole.", ["toolHeight", "keyringHoleDiameter"]);
        if (H < noseHeight + 3 * millimeter)
            throw regenError("Tool height is too small for the pusher nose.", ["toolHeight"]);
        if (grip1Start < tailR + 1 * millimeter)
            throw regenError("Tool is too short for this keyring hole.", ["toolLength", "keyringHoleDiameter"]);
        if (slotX + slotWidth + hookReach > noseBackX - 1 * millimeter)
            throw regenError("Tool is too short for the hook slot.", ["toolLength"]);

        // 1. Outer profile on the Front plane.
        // The sketch X axis is flipped so the sketch Y axis points up (+Z).
        var sketch1 = newSketchOnPlane(context, id + "profileSketch", {
                "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 1, 0), vector(-1, 0, 0))
        });

        // Rounded tail around the keyring hole
        skArc(sketch1, "tailArc", {
                "start" : vector(tailR, mm0),
                "mid" : vector(mm0, tailR),
                "end" : vector(tailR, 2 * tailR)
        });

        // Smooth curved upper spine (palm / thumb rest)
        skArc(sketch1, "spineArc", {
                "start" : vector(tailR, 2 * tailR),
                "mid" : vector(spineMidX, H),
                "end" : vector(noseBackX, noseHeight)
        });

        // Blunt rounded pusher nose
        skArc(sketch1, "noseArc", {
                "start" : vector(noseBackX, noseHeight),
                "mid" : vector(L, noseHeight / 2),
                "end" : vector(noseBackX, mm0)
        });

        // Bottom edge, running from the nose back toward the tail
        skLineSegment(sketch1, "bottomNose", {
                "start" : vector(noseBackX, mm0),
                "end" : vector(slotX + slotWidth, mm0)
        });

        // Under-cut hook slot (L-shaped notch whose pocket reaches toward the nose)
        skLineSegment(sketch1, "slotFront", {
                "start" : vector(slotX + slotWidth, mm0),
                "end" : vector(slotX + slotWidth, slotDepth - slotWidth)
        });
        skLineSegment(sketch1, "slotFloor", {
                "start" : vector(slotX + slotWidth, slotDepth - slotWidth),
                "end" : vector(slotX + slotWidth + hookReach, slotDepth - slotWidth)
        });
        skLineSegment(sketch1, "slotFar", {
                "start" : vector(slotX + slotWidth + hookReach, slotDepth - slotWidth),
                "end" : vector(slotX + slotWidth + hookReach, slotDepth)
        });
        skLineSegment(sketch1, "slotRoof", {
                "start" : vector(slotX + slotWidth + hookReach, slotDepth),
                "end" : vector(slotX, slotDepth)
        });
        skLineSegment(sketch1, "slotRear", {
                "start" : vector(slotX, slotDepth),
                "end" : vector(slotX, mm0)
        });

        // Bottom edge with two concave finger-grip rests
        skLineSegment(sketch1, "bottomA", {
                "start" : vector(slotX, mm0),
                "end" : vector(grip2End, mm0)
        });
        skArc(sketch1, "grip2", {
                "start" : vector(grip2End, mm0),
                "mid" : vector((grip2Start + grip2End) / 2, gripDepth),
                "end" : vector(grip2Start, mm0)
        });
        skLineSegment(sketch1, "bottomB", {
                "start" : vector(grip2Start, mm0),
                "end" : vector(grip1End, mm0)
        });
        skArc(sketch1, "grip1", {
                "start" : vector(grip1End, mm0),
                "mid" : vector((grip1Start + grip1End) / 2, gripDepth),
                "end" : vector(grip1Start, mm0)
        });
        skLineSegment(sketch1, "bottomC", {
                "start" : vector(grip1Start, mm0),
                "end" : vector(tailR, mm0)
        });

        skSolve(sketch1);

        // 2. Extrude the profile into a solid
        opExtrude(context, id + "bodyExtrude", {
                "entities" : qSketchRegion(id + "profileSketch"),
                "direction" : vector(0, 1, 0),
                "endBound" : BoundingType.BLIND,
                "endDepth" : definition.bodyThickness
        });

        // 3. Keyring hole near the rear tail (cut as a separate tool so it stays a true hole)
        var holeSketch = newSketchOnPlane(context, id + "holeSketch", {
                "sketchPlane" : plane(vector(0 * millimeter, -1 * millimeter, 0 * millimeter), vector(0, 1, 0), vector(-1, 0, 0))
        });
        skCircle(holeSketch, "keyringHole", {
                "center" : vector(tailR, tailR),
                "radius" : definition.keyringHoleDiameter / 2
        });
        skSolve(holeSketch);

        opExtrude(context, id + "holeExtrude", {
                "entities" : qSketchRegion(id + "holeSketch"),
                "direction" : vector(0, 1, 0),
                "endBound" : BoundingType.BLIND,
                "endDepth" : definition.bodyThickness + 2 * millimeter
        });

        opBoolean(context, id + "cutHole", {
                "tools" : qCreatedBy(id + "holeExtrude", EntityType.BODY),
                "targets" : qCreatedBy(id + "bodyExtrude", EntityType.BODY),
                "operationType" : BooleanOperationType.SUBTRACTION
        });

        // 4. Remove the construction sketch bodies
        opDeleteBodies(context, id + "deleteSketches", {
                "entities" : qUnion([
                        qCreatedBy(id + "profileSketch", EntityType.BODY),
                        qCreatedBy(id + "holeSketch", EntityType.BODY)
                ])
        });
    });
