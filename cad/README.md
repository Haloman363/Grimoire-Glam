# Articulated Blythe-style doll base (OpenSCAD)

`blythe_doll.scad` is a fully parametric, printable, strung fashion-doll body: head with removable
faceplate, ball-jointed neck/waist/shoulders/hips, pin-hinged elbows and knees, and swappable
peg-in hands and feet. No clothes, no hair. Units are mm; the assembled doll is ~280 mm tall
(head 76 mm wide). Scale the whole thing in your slicer for a smaller doll (bore and clearances
then scale too, so keep the bore ≥ 2.5 mm).

```
openscad -o doll.stl -D 'part="assembly"' blythe_doll.scad      # posed preview (slow, CGAL)
openscad -o thigh.stl -D 'part="thigh"' blythe_doll.scad        # one printable part
./export_all.sh                                                 # every part -> stl/
```

Parts: `head_front head_back neck chest pelvis upper_arm forearm hand thigh shin foot dowel`
(+ `assembly`, `plate`). Print quantities: upper_arm ×2, forearm ×2, hand ×2 (print one with
`mirror` in the slicer), thigh ×2, shin ×2, foot ×2, dowel ×2 (or cut 3 mm filament to 10 mm), rest ×1.
Any OpenSCAD ≥ 2021.01 works; Onshape/Fusion/Blender users can import the exported STLs.

## Joint mechanics

| Joint | Mechanism | Range (verified by mesh-collision tests) |
|---|---|---|
| Head ↔ neck, neck ↔ chest | ball (R 6.5) in snap-throat cup, 0.2 mm cavity clearance, 4 flex slits | nod ±35°, free rotation |
| Chest ↔ pelvis (waist) | ball (R 9) on pelvis stem in cup under chest | lean ±35° fwd/back, ±25° side, free twist |
| Shoulder | ball (R 8) in cup, swing-relief slots | 100° out, 100° forward, 70° back, 10° in, free twist |
| Hip | ball (R 10) in cup | 60° out, 110° forward, 35° back, 5° in, free twist |
| Elbow / knee | clevis with two integral Ø4 / Ø5 stub pins into tongue holes (+0.2 mm per side), fold-relief wedge | 0–120° (collision-free; ≥125° touches) |
| Wrist / ankle | Ø7 / Ø8 peg, 0.2 mm clearance socket, 0.5 mm detent ridge + groove | tool-free swap |
| Faceplate | 4× 6×2 mm magnets + 2× Ø3 dowels | pops off for eye/elastic access |

Every mating gap is `clr = 0.2` mm per side (top of the file); change it for your printer.
Ball joints rely on a snap throat (`throat_ratio`, default 0.85): print in PETG or tough PLA
with ≥ 3 perimeters; if a cup is too stiff or a ball too loose raise/lower `throat_ratio`
(0.80–0.92). Elbow/knee clevis arms flex ~1.5 mm when the tongue is pressed in – PETG recommended.

## Elastic (stringing) paths – `bore_d` = 2.6 mm (asserted ≥ 2.5)

* **Head cord**: hook bar inside the head → neck (through both ball bores) → chest vertical bore →
  pelvis waist ball → cavity with a cross bar in the pelvis. Loop it round the bar at each end.
* **Arm cord**: knot seat at the floor of one wrist socket → forearm → elbow (an elastic fan slot
  in the tongue keeps the route open through the whole bend) → upper arm → shoulder ball →
  chest cross-tunnel → other shoulder → … → other wrist. One continuous cord.
* **Leg cord**: same, ankle → shin → knee → thigh → hip → pelvis cross-tunnel → other leg.
* Tie a knot in the cord, pull it into the wrist/ankle socket (it seats on the socket floor under
  the peg), tension, and knot the other end in the opposite socket. Clamp, then plug in hands/feet.
  Use 1.0–1.5 mm round doll elastic (a doubled loop for legs).

## Head

* Rounded ellipsoid shell (wall 2.8 mm, hollow) split at `y_cut`; the rear cap is the faceplate.
* Eye seats: Ø24 mm recess (`eye_seat_d`, +0.2 clearance) with Ø18 mm window (`eye_win_d`) behind
  it – fit a Ø24 mm glass cabochon / eye chip, or mount a standard eye mechanism from behind.
* Magnet pockets are glued in with opposing polarity (check with a fridge-magnet test first).

## Print notes

`plate` shows a reference layout: head halves cut-face down, limbs on their side (hinge walls
vertical, so the clevis arms and pins are strong), feet sole-down. Use supports for the ball
domes, socket cups and eye windows; 0.2 mm layers, 0.4 mm nozzle. Test-print one elbow and one
shoulder first and adjust `clr`/`throat_ratio`.

## Verification done

Each part exports as a watertight single-body mesh (OpenSCAD 2021.01 + trimesh/manifold3d), with
zero interference at neutral and across every range quoted above, elastic centrelines clear
end-to-end through each part (including the elbow/knee fan slots), and no plate overlaps.
