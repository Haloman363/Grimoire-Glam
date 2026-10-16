# Pegboard display system (Onshape FeatureScript)

`pegboard_display.fs` is one parametric feature, "Pegboard display system", that builds a
flat-pack, 3D-printable pegboard market display as separate named solid bodies.

## Install
1. In Onshape open **Feature Studio** (+ tab at the bottom of a document) and paste in `pegboard_display.fs`.
2. Switch to a Part Studio, then use the feature from the toolbar (it appears as *Pegboard display system*).

The file targets `FeatureScript 2180`. If Onshape offers to upgrade the `import` versions, accept.
It has not been run in Onshape from this repo's environment, so expect to fix a small API mismatch or two on first compile.

## Geometry conventions
Board lies on the Top plane: X = width, Y = height, Z = thickness. Back face is z = 0, peg side is z = thickness.

## Profiles
| Profile | Inputs |
|---|---|
| B. Cathedral / gothic arch | `BoardWidth` (250), `BoardHeight` (450), `ArchRadius` (>= width / 2; = width gives an equilateral arch) |
| A. Character logo | A sketch region on the **Top plane**, plus a solid border (min 12 mm). Onshape cannot read an SVG inside a feature: import the DXF (or convert SVG to DXF in Inkscape) into a sketch with *Insert DXF*, close the outline, and pick its region. |
| C. Seasonal preset | Coffin, crescent moon, hexagon trio, hexagon flower, fitted inside `BoardWidth` x `BoardHeight` |

Logo border: the board is the silhouette grown by the border width with rounded corners, so thin hair points get a full 12 mm of material around them.

## Pegboard grid
25.4 mm pitch, 6.53 mm holes, 5.08 mm board. A hole is dropped when its edge would be closer than 8 mm to the final outer edge (for the logo, the edge is the border's outer edge).

## Flat-pack tiles and joints
* Tiles are sized for a 220 x 220, 256 x 256 or custom bed (joint overhang is accounted for).
* Seams are snapped to the midlines between hole rows/columns and joints are centred between holes, so a joint can never touch a peg hole.
* Inner seam faces are recessed by half the tolerance (default 0.15 mm total gap).
* **Integral** style: dovetail tabs on each tile's right/top edge, matching sockets (tab grown by the tolerance) on the neighbour's left/bottom edge. Tiles lock in-plane and are assembled by lowering one onto the other.
* **Separate keys** style: tiles are plain-cut and loose bow-tie keys drop into back-face pockets. Use a drop of glue or tape if you want them retained when the board is carried.

## Mounting
* **Tabletop**: two slotted shoe feet. The slot is cut in the board's frame and the foot's table face is rotated by the tilt angle (default 5 deg backward). Defaults suit flat-bottomed shapes; feet always follow the lowest board edge in their span.
* **Hanging**: eyelet loops (10 mm inner diameter) merged into the top edge at the chosen spacing.
* **Header plate**: optional banner plate with two pegs that press into a pair of holes in the top-most row.

## Output bodies
`Tile C#R#`, `Joint key #`, `Base foot #`, `Header plate`. Set *Layout* to *Print layout* to move everything flat onto a packed print sheet (feet and header are rotated to their best print orientation).

## Notes
* Eyelets and feet are placed from the board silhouette. If a tile exceeds the print bed the feature reports a warning naming it.
* Cells that contain no board material (for example the empty corners of a crescent) produce no tile.
