# Modular pegboard display (Onshape FeatureScript)

`pegboard_display.fs` defines one custom feature, **Pegboard display**.

## Install
1. In an Onshape document: **+ > Add Feature Studio**, paste the contents of `pegboard_display.fs`.
2. Open a Part Studio, pick **Pegboard display** from the feature toolbar (the custom-feature icon).

## Parameters
`BoardHeight`, `BoardWidth`, `PegSpacing` (25.4 mm), `HoleDiameter` (6.53 mm), `Thickness` (4.76-6.35 mm),
`ArchRadius` (must be >= BoardWidth/2; equal to the width gives an equilateral gothic arch), plus edge clearance
(min 19.05 mm), printer bed X/Y (256 mm), fit tolerance (0.175 mm), dovetail size/angle/spacing, eyelets and feet.

## Shapes
- **Gothic arch**: flat bottom, vertical sides, two-arc pointed top.
- **Framed header plate**: chamfered plate, hole-free header band with a shallow logo recess.
- **Hex cluster**: seven honeycomb hexagons, scaled to fit the width/height.
- **Custom logo silhouette**: sketch a closed outline on the **Top plane** (bottom edge on Y=0, centred on X=0), then pick its region(s).
  Holes are placed on the bounding box (width x height) minus edge clearance and the header band, so keep the silhouette roughly filling that box.

## Geometry notes
- Front face is Z = Thickness, back is Z = 0. Tiles are modelled flat in the assembled position.
- Tile seams fall midway between hole rows/columns, so no hole is split. Tile size is the largest whole number of pegs that fits the bed once the dovetail is added.
- Dovetails slide together along Z. Each flank carries a small crush rib (tolerance + interference) for a friction/snap fit.
- Eyelets (0.375 in hole) are ears fused to the board top; the feet are separate push-fit blocks with a slot for the bottom edge.

## Status
Written without access to an Onshape instance, so it has **not been run**. Expect to fix small API or syntax issues on the first regenerate;
the error messages in the feature dialog point to the failing line.
