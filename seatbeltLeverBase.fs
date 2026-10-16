FeatureScript 2280;
import(path : "onshape/std/geometry.fs", version : "2280.0");

const BUCKLE_WIDTH_BOUNDS = { (millimeter) : [1, 50, 500] } as LengthBoundSpec;
const BUCKLE_DEPTH_BOUNDS = { (millimeter) : [1, 30, 500] } as LengthBoundSpec;
const WALL_THICKNESS_BOUNDS = { (millimeter) : [0.5, 3.5, 50] } as LengthBoundSpec;
const LEVER_LENGTH_BOUNDS = { (millimeter) : [1, 45, 500] } as LengthBoundSpec;

annotation { "Feature Type Name" : "Seatbelt Lever Base Generator" }
export const seatbeltLeverBase = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "Buckle housing width" }
        isLength(definition.buckleWidth, BUCKLE_WIDTH_BOUNDS);

        annotation { "Name" : "Buckle housing depth" }
        isLength(definition.buckleDepth, BUCKLE_DEPTH_BOUNDS);

        annotation { "Name" : "Wall thickness" }
        isLength(definition.wallThickness, WALL_THICKNESS_BOUNDS);

        annotation { "Name" : "Lever extension length" }
        isLength(definition.leverLength, LEVER_LENGTH_BOUNDS);
    }
    {
        const collarHeight = 25 * millimeter;
        const lugRadius = 6 * millimeter;
        const pinHoleRadius = 2.5 * millimeter;
        const lugProtrusion = 8 * millimeter;

        const innerW = definition.buckleWidth;
        const innerD = definition.buckleDepth;
        const outerW = innerW + 2 * definition.wallThickness;
        const outerD = innerD + 2 * definition.wallThickness;

        // 1. Outer collar block
        var outerSketch = newSketchOnPlane(context, id + "outerSketch", {
                "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1))
        });
        skRectangle(outerSketch, "outerRect", {
                "firstCorner" : vector(-outerW / 2, -outerD / 2),
                "secondCorner" : vector(outerW / 2, outerD / 2)
        });
        skSolve(outerSketch);

        opExtrude(context, id + "outerExtrude", {
                "entities" : qSketchRegion(id + "outerSketch"),
                "direction" : vector(0, 0, 1),
                "endBound" : BoundingType.BLIND,
                "endDepth" : collarHeight
        });

        // 2. Inner cavity, cut through the full height to hollow the collar
        var innerSketch = newSketchOnPlane(context, id + "innerSketch", {
                "sketchPlane" : plane(vector(0, 0, 0) * millimeter, vector(0, 0, 1))
        });
        skRectangle(innerSketch, "innerRect", {
                "firstCorner" : vector(-innerW / 2, -innerD / 2),
                "secondCorner" : vector(innerW / 2, innerD / 2)
        });
        skSolve(innerSketch);

        opExtrude(context, id + "innerExtrude", {
                "entities" : qSketchRegion(id + "innerSketch"),
                "direction" : vector(0, 0, 1),
                "endBound" : BoundingType.BLIND,
                "endDepth" : collarHeight
        });

        opBoolean(context, id + "hollow", {
                "tools" : qCreatedBy(id + "innerExtrude", EntityType.BODY),
                "targets" : qCreatedBy(id + "outerExtrude", EntityType.BODY),
                "operationType" : BooleanOperationType.SUBTRACTION
        });

        // 3. Hinge lug on the back (+Y) wall.
        // Sketch plane normal is +Y; X direction is -X so the sketch Y axis is world +Z.
        // The lug starts inside the wall (so it fuses to it) and protrudes outward.
        var lugSketch = newSketchOnPlane(context, id + "lugSketch", {
                "sketchPlane" : plane(vector(0 * millimeter, outerD / 2 - definition.wallThickness, 0 * millimeter), vector(0, 1, 0), vector(-1, 0, 0))
        });
        skCircle(lugSketch, "lugCircle", {
                "center" : vector(0 * millimeter, collarHeight / 2),
                "radius" : lugRadius
        });
        skSolve(lugSketch);

        opExtrude(context, id + "lugExtrude", {
                "entities" : qSketchRegion(id + "lugSketch"),
                "direction" : vector(0, 1, 0),
                "endBound" : BoundingType.BLIND,
                "endDepth" : lugProtrusion + definition.wallThickness
        });

        opBoolean(context, id + "addLug", {
                "tools" : qUnion([qCreatedBy(id + "outerExtrude", EntityType.BODY), qCreatedBy(id + "lugExtrude", EntityType.BODY)]),
                "operationType" : BooleanOperationType.UNION
        });

        // 4. Pin hole through the lug only (starts at the outer face of the back wall)
        var holeSketch = newSketchOnPlane(context, id + "holeSketch", {
                "sketchPlane" : plane(vector(0 * millimeter, outerD / 2, 0 * millimeter), vector(0, 1, 0), vector(-1, 0, 0))
        });
        skCircle(holeSketch, "holeCircle", {
                "center" : vector(0 * millimeter, collarHeight / 2),
                "radius" : pinHoleRadius
        });
        skSolve(holeSketch);

        opExtrude(context, id + "holeExtrude", {
                "entities" : qSketchRegion(id + "holeSketch"),
                "direction" : vector(0, 1, 0),
                "endBound" : BoundingType.BLIND,
                "endDepth" : lugProtrusion + 1 * millimeter
        });

        opBoolean(context, id + "cutHole", {
                "tools" : qCreatedBy(id + "holeExtrude", EntityType.BODY),
                "targets" : qCreatedBy(id + "outerExtrude", EntityType.BODY),
                "operationType" : BooleanOperationType.SUBTRACTION
        });

        // 5. Remove the construction sketch bodies
        opDeleteBodies(context, id + "deleteSketches", {
                "entities" : qUnion([
                        qCreatedBy(id + "outerSketch", EntityType.BODY),
                        qCreatedBy(id + "innerSketch", EntityType.BODY),
                        qCreatedBy(id + "lugSketch", EntityType.BODY),
                        qCreatedBy(id + "holeSketch", EntityType.BODY)
                ])
        });
    });
