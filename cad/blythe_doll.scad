/*
  Articulated Blythe-style fashion doll BASE (no clothes, no hair)
  ---------------------------------------------------------------
  OpenSCAD 2021.01+.   Units: mm.   Doll height ~ 280 mm (head ~76 mm wide).

  World frame (assembly): +Z up, FRONT of the doll faces -Y, doll's right arm is +X.

  Choose what to render with `part`:
     "assembly"  posed doll (see pose_* below)          "plate"  everything laid out for printing
     "head_front" "head_back" "neck" "chest" "pelvis"
     "upper_arm" "forearm" "hand" "thigh" "shin" "foot" "dowel"
  Command line example:
     openscad -o head_front.stl -D 'part="head_front"' blythe_doll.scad

  Mechanics (all interlocking parts use clr = 0.2 mm per side):
   * Ball-and-socket: ball R, cavity R+clr; cup has a snap throat (throat_ratio*R) with
     4 flex slits, plus swing-relief slots so limbs can swing well past the throat.
   * Elbows / knees: clevis with two conical-free stub pins (pin_d) into tongue holes
     (pin_d + 2*clr); tongue is slot - 2*clr wide.  120 deg+ of bend (relief wedge).
   * Wrists / ankles: peg (socket_d - 2*clr) with detent ridge into a socket with groove.
   * Head: faceplate (rear cap) held with 6x2 mm magnets + 2 locating pins.
   * Elastic: every joint is bored with bore_d (>= 2.5 mm).  Continuous runs:
        head hook -> neck -> chest -> waist ball -> pelvis hook bar          (head cord)
        wrist knot seat -> forearm -> elbow -> upper arm -> shoulder ->
            chest cross tunnel -> other shoulder ... -> other wrist           (arm cord)
        ankle knot seat -> shin -> knee -> thigh -> hip -> pelvis cross
            tunnel -> other hip ... -> other ankle                            (leg cord)
*/

// ------------------------------------------------------------------ selection
part = "assembly";

// ------------------------------------------------------------------ pose (assembly only)
pose_head_tilt   = 0;    // forward(+)/back(-) nod, deg
pose_head_turn   = 0;    // deg about vertical
pose_waist_lean  = 0;    // forward(+) lean, deg
pose_waist_turn  = 0;
pose_arm_raise_x = 20;   // sideways raise, deg
pose_arm_raise_y = 0;    // forward raise, deg
pose_elbow       = 50;   // 0..120
pose_hip_y       = 0;    // forward lift, deg
pose_knee        = 0;    // 0..125

// ------------------------------------------------------------------ global tolerances
clr     = 0.2;     // clearance per side on every mating surface
bore_d  = 2.6;     // elastic bore (>= 2.5)
assert(bore_d >= 2.5, "elastic bore must be >= 2.5 mm");
throat_ratio = 0.85;   // snap throat radius / ball radius (raise toward 0.92 if sockets are too stiff)
slit_w  = 0.8;
cup_wall = 2.5;

// ------------------------------------------------------------------ head
head_r   = [38, 36, 38];     // outer semi-axes x,y,z
head_wall = 2.8;
y_cut    = 20;               // faceplate split plane (y)
eye_x = 17; eye_z = 3;
eye_seat_d = 24;             // glass cabochon / eye-chip seat
eye_win_d  = 18;
mag_d = 6; mag_h = 2;        // 6x2 mm disc magnets
neck_c = [0, 3, -33];        // neck socket ball centre inside head
NR = 6.5;                    // neck ball radius
NRS = 3.6;                   // neck stem radius
neck_len = 17;               // ball centre to ball centre

// ------------------------------------------------------------------ torso
WR = 9;                      // waist ball radius
SR = 8;                      // shoulder ball radius
HR = 10;                     // hip ball radius
chest_neck_z = 35;
sh_x = 27.5; sh_z = 26;
hip_x = 15;  hip_z = -26;    // pelvis frame (origin = waist ball centre)

// ------------------------------------------------------------------ limbs
L_ua = 44;  rr_a = 8;        // upper arm: shoulder centre -> elbow pivot, knuckle radius
L_fa = 40;
L_th = 60;  rr_l = 10;
L_sh = 56;
foot_h = 14;
peg_w_d = 7;   peg_w_l = 7;  // wrist peg
peg_a_d = 8;   peg_a_l = 9;  // ankle peg
ridge = 0.5;

$fn = 40;

// ====================================================================== helpers
function dz(R)  = sqrt((R+clr)*(R+clr) - pow(throat_ratio*R,2));  // throat plane depth below ball centre
function tr(R)  = throat_ratio*R;

module ellipsoid(r, fn=56) scale(r) sphere(1, $fn=fn);

// cone with apex at origin, axis along direction v, half-angle ha, length L
module cone_dir(v, ha, L) {
    n = v/norm(v);
    rotate([0, acos(n[2]), atan2(n[1], n[0])])
        cylinder(h=L, r1=0, r2=L*tan(ha), $fn=32);
}

// cylinder from point a to point b
module cyl_ab(a, b, d) {
    v = b - a; L = norm(v);
    translate(a) rotate([0, acos(v[2]/L), atan2(v[1], v[0])]) cylinder(h=L, d=d, $fn=24);
}

// ====================================================================== ball & socket
// Socket frame: ball centre at origin, opening (stem exit) toward -Z.
module socket_pos(R) {
    hull() {
        sphere(R + cup_wall, $fn=40);
        translate([0, 0, -dz(R) - 1.5]) cylinder(r=R + cup_wall, h=1, $fn=40);
    }
}

// swept stem volume (swing relief) toward direction az, up to ang degrees
module stem_sweep(rs, ang, az) {
    if (ang > 0) rotate([0, 0, az])
        for (a = [0 : 12 : ang - 0.01]) hull() {
            rotate([0, -a, 0]) translate([0, 0, -40]) cylinder(r=rs+0.7, h=40, $fn=18);
            rotate([0, -min(a+12, ang), 0]) translate([0, 0, -40]) cylinder(r=rs+0.7, h=40, $fn=18);
        }
}

// sw = [+X, -X, +Y, -Y] swing relief angles ; vdir/vha = elastic vent cone
module socket_neg(R, rs, sw=[0,0,0,0], vdir=[0,0,1], vha=35) {
    union() {
        sphere(R + clr, $fn=48);                                        // 0.2 mm clearance cavity
        translate([0, 0, -dz(R) - 30]) cylinder(r=tr(R), h=30, $fn=40); // snap throat
        translate([0, 0, -dz(R) - 2]) cylinder(r1=tr(R)+2, r2=tr(R), h=2, $fn=40); // lead-in chamfer
        stem_sweep(rs, sw[0], 0);
        stem_sweep(rs, sw[1], 180);
        stem_sweep(rs, sw[2], 90);
        stem_sweep(rs, sw[3], 270);
        intersection() {                                                  // elastic vent (dome window only)
            cone_dir(vdir, vha, 2*(R + cup_wall + 8));
            sphere(R + cup_wall + 1, $fn=40);
        }
        for (k = [0:3]) rotate([0, 0, 45 + 90*k])                        // flex slits
            translate([0, -slit_w/2, -dz(R) - 3]) cube([R + cup_wall + 1, slit_w, 8]);
    }
}

// Ball with stem along +Z (stem exits away from the socket dome), bored through.
module ball_pos(R, rs, L) {
    sphere(R, $fn=48);
    cylinder(r=rs, h=L, $fn=32);
}
module ball_bore(R, L) translate([0,0,-R-1]) cylinder(d=bore_d, h=R+L+2, $fn=20);

// ====================================================================== HEAD
module head_outer() ellipsoid(head_r);
module head_inner() ellipsoid([head_r[0]-head_wall, head_r[1]-head_wall, head_r[2]-head_wall]);

mag_pts = [[16.5, 16.5], [-16.5, 16.5], [16.5, -16.5], [-16.5, -16.5]];   // (x,z) on the split plane

// boss hulled outward so it fuses with the wall
module wall_boss(x, z, r, y0, y1) {
    intersection() {
        head_outer();
        hull() {
            translate([x, y0, z]) rotate([-90,0,0]) cylinder(r=r, h=y1-y0, $fn=28);
            translate([x*1.9, y0, z*1.9]) rotate([-90,0,0]) cylinder(r=r, h=y1-y0, $fn=28);
        }
    }
}

module head_front() {
    difference() {
        union() {
            difference() { head_outer(); head_inner(); }
            translate(neck_c) socket_pos(NR);
            for (s = [-1, 1]) intersection() {                      // eye seat tubes
                head_outer();
                translate([s*eye_x, -14, eye_z]) rotate([90,0,0]) cylinder(d=eye_seat_d+5, h=40, $fn=40);
            }
            for (p = mag_pts) wall_boss(p[0], p[1], 5, y_cut-7, y_cut);
            for (s = [-1, 1]) wall_boss(s*21.5, 0, 3.6, y_cut-6, y_cut);   // pin bosses
            // elastic hook bar (head cord anchor) on two posts
            for (s = [-1, 1]) cyl_ab([s*7, neck_c[1], neck_c[2]+2], [s*7, neck_c[1], -19], 3.6);
            cyl_ab([-8, neck_c[1], -19], [8, neck_c[1], -19], 3.4);
        }
        translate([-100, y_cut, -100]) cube([200, 100, 200]);                 // remove rear cap
        translate(neck_c) socket_neg(NR, NRS, [0,0,40,40], [0,0,1], 28);     // neck ball socket, nod +-40
        for (s = [-1, 1]) {                                                    // eye sockets
            translate([s*eye_x, -14, eye_z]) rotate([90,0,0]) cylinder(d=eye_win_d, h=60, $fn=40);
            translate([s*eye_x, -28.5, eye_z]) rotate([90,0,0]) cylinder(d=eye_seat_d+2*clr, h=20, $fn=48);
        }
        for (p = mag_pts)                                                       // magnet pockets
            translate([p[0], y_cut-mag_h-0.2, p[1]]) rotate([-90,0,0]) cylinder(d=mag_d+2*clr, h=mag_h+1, $fn=28);
        for (s = [-1, 1])                                                       // dowel holes
            translate([s*21.5, y_cut-5, 0]) rotate([-90,0,0]) cylinder(d=3+2*clr, h=6, $fn=20);
    }
}

module head_back() {
    difference() {
        union() {
            intersection() {
                difference() { head_outer(); head_inner(); }
                translate([-100, y_cut, -100]) cube([200, 100, 200]);
            }
            for (p = mag_pts) wall_boss(p[0], p[1], 5, y_cut, y_cut+7);
            for (s = [-1, 1]) wall_boss(s*21.5, 0, 3.6, y_cut, y_cut+6);
        }
        for (s = [-1, 1])                                                       // dowel holes (3 mm dowels / filament)
            translate([s*21.5, y_cut-1, 0]) rotate([-90,0,0]) cylinder(d=3+2*clr, h=6, $fn=20);
        for (p = mag_pts)
            translate([p[0], y_cut-0.2, p[1]]) rotate([-90,0,0]) cylinder(d=mag_d+2*clr, h=mag_h+0.2+0.2, $fn=28);
    }
}

module dowel() cylinder(d=3, h=10.5, $fn=20);   // locating dowel (or cut 3 mm filament to 10 mm)

// ====================================================================== NECK
module neck() {
    difference() {
        union() {
            ball_pos(NR, NRS, 0);
            translate([0,0,neck_len]) ball_pos(NR, NRS, 0);
            cylinder(r=NRS, h=neck_len, $fn=32);
        }
        translate([0,0,-NR-1]) cylinder(d=bore_d, h=neck_len+2*NR+2, $fn=20);
    }
}

// ====================================================================== CHEST  (origin = waist cup centre)
module chest_body() {
    hull() {
        translate([0,0,8])  ellipsoid([16,12,12]);
        translate([0,0,26]) ellipsoid([20,14,12]);
    }
}
module chest() {
    difference() {
        union() {
            chest_body();
            // waist cup (opening down)
            socket_pos(WR);
            // neck cup (opening up)
            translate([0,0,chest_neck_z]) rotate([180,0,0]) socket_pos(NR);
            // shoulder yokes + cups
            for (s = [-1, 1]) {
                hull() {
                    translate([s*12, 0, sh_z]) rotate([0,90,0]) cylinder(r=6.5, h=1, center=true, $fn=28);
                    translate([s*sh_x, 0, sh_z]) sphere(SR + cup_wall, $fn=36);
                }
                translate([s*sh_x, 0, sh_z]) socket_pos(SR);
            }
        }
        // waist socket: lean +-35 front/back, +-25 side
        socket_neg(WR, 4.8, [25,25,35,35], [0,0,1], 30);
        translate([0,0,chest_neck_z]) rotate([180,0,0]) socket_neg(NR, NRS, [0,0,35,35], [0,0,1], 28);
        // shoulders: x+ = outward, -x = toward body
        for (s = [-1, 1]) translate([s*sh_x, 0, sh_z])
            mirror([s < 0 ? 1 : 0, 0, 0])
                socket_neg(SR, 4.0, [100, 10, 70, 100], [-0.7, 0, 0.7], 48);
        // head cord: neck centre -> waist centre
        translate([0, 0, -2]) cylinder(d=bore_d, h=chest_neck_z + 4, $fn=20);
        // arm cord: shoulder to shoulder cross tunnel
        translate([-sh_x, 0, sh_z]) rotate([0,90,0]) cylinder(d=bore_d+0.2, h=2*sh_x, $fn=20);
    }
}

// ====================================================================== PELVIS (origin = waist ball centre)
module pelvis() {
    difference() {
        union() {
            translate([0,0,-24]) ellipsoid([24, 15, 13]);
            translate([0,0,-14]) cylinder(r=5, h=14, $fn=32);             // waist stem
            sphere(WR, $fn=48);                                              // waist ball
            for (s = [-1, 1]) {
                hull() {
                    translate([s*hip_x, 0, hip_z]) sphere(HR + cup_wall, $fn=36);
                    translate([s*6, 0, hip_z + 2]) sphere(8, $fn=24);
                }
                translate([s*hip_x, 0, hip_z]) socket_pos(HR);
            }
        }
        // head cord: ball top -> hook cavity
        translate([0,0,-WR-1]) cylinder(d=bore_d, h=WR*2, $fn=20);
        translate([0,0,-19]) cylinder(d=bore_d, h=19, $fn=20);
        for (s = [-1, 1]) translate([s*hip_x, 0, hip_z])
            mirror([s < 0 ? 1 : 0, 0, 0])
                socket_neg(HR, 5.0, [60, 5, 35, 110], [-0.7, 0, 0.7], 48);
        // leg cord: hip to hip cross tunnel
        translate([-hip_x, 0, hip_z]) rotate([0,90,0]) cylinder(d=bore_d+0.2, h=2*hip_x, $fn=20);
        // hook cavity (pelvis) – access from the waist bore
        translate([0,0,-19]) sphere(5, $fn=28);
    }
    // hook bar spanning the cavity (elastic loops around it)
    cyl_ab([0, -7.5, -19], [0, 7.5, -19], 3.4);
}

// ====================================================================== HINGES
// Removes the half-space (n.p < 0.6) around the pivot except the shared knuckle circle,
// so two limbs can fold to 'bend' degrees without their flesh colliding.
module relief_cut(n, rr, g=0.6, B=70) {
    difference() {
        rotate([atan2(-n[1], n[2]), 0, 0]) translate([-B, -B, g - 2*B]) cube(2*B);
        rotate([0,90,0]) cylinder(r=rr+0.3, h=4*rr, center=true, $fn=48);
    }
}

// Clevis half (limb extends toward +Z, pivot at origin). fold = -1 forearm folds to front(-Y), +1 to back
module hinge_clevis(rr, pin_d, hd, fold, bend, hl=16) {
    at = (rr > 9) ? 4.5 : 3.5;
    S  = 2*rr - 2*at;
    ls = clr + hd - clr;                       // stub length = hole depth (0.2 end gap stays as clearance)
    u = [0, 0, 1];
    d = [0, fold*sin(bend), -cos(bend)];       // forearm axis at max fold (limb frame, pivot at origin)
    n = (u - d)/norm(u - d);
    difference() {
        union() {
            rotate([0,90,0]) cylinder(r=rr, h=2*rr, center=true, $fn=48);
            cylinder(r=rr, h=hl, $fn=48);
        }
        // slot for tongue
        translate([-S/2, -rr-1, -rr-1]) cube([S, 2*rr+2, rr + 1 + rr + 2.5]);
        relief_cut(n, rr);
        // elastic bore
        cylinder(d=bore_d, h=hl+1, $fn=20);
    }
    for (s = [-1, 1]) translate([s*S/2, 0, 0]) rotate([0, -s*90, 0]) {   // pin stubs
        cylinder(d=pin_d, h=ls - 0.6, $fn=28);
        translate([0,0,ls-0.6]) cylinder(d1=pin_d, d2=pin_d-1.0, h=0.6, $fn=28);
    }
}

// Tongue half (limb extends toward -Z, pivot at origin)
module hinge_tongue(rr, pin_d, hd, fold, bend, hl=0) {
    at = (rr > 9) ? 4.5 : 3.5;
    S  = 2*rr - 2*at;
    tt = S - 2*clr;
    difference() {
        union() {
            rotate([0,90,0]) cylinder(r=rr-0.8, h=tt, center=true, $fn=48);
            translate([-tt/2, -(rr-1.1), -rr-1.5]) cube([tt, 2*(rr-1.1), rr+1.5]);
        }
        // pin holes (dia + 2*clr)
        for (s = [-1, 1]) translate([s*tt/2, 0, 0]) rotate([0, -s*90, 0]) cylinder(d=pin_d+2*clr, h=hd+clr, $fn=28);
        // elastic fan slit: from forearm bore up through every angle reached while folding
        for (a = [0 : 4 : bend + 8]) rotate([-fold*a, 0, 0]) cylinder(d=bore_d+0.3, h=rr+1, $fn=20);
        translate([-bore_d/2, -bore_d, -bore_d]) cube([bore_d, 2*bore_d, 2*bore_d]);
    }
}

// ====================================================================== ARMS
module upper_arm() {
    rs = 4.0;
    difference() {
        union() {
            ball_pos(SR, rs, 0);
            translate([0,0,-16]) cylinder(r=rs, h=16, $fn=32);
            // limb body
            translate([0,0,-(L_ua-14)]) cylinder(r1=rr_a, r2=6, h=(L_ua-14)-16, $fn=40);
            translate([0,0,-L_ua]) hinge_clevis(rr_a, 4, 2.0, -1, 120);
        }
        translate([0,0,-L_ua+1]) cylinder(d=bore_d, h=L_ua + SR, $fn=20);
        translate([0,0,-SR-1]) cylinder(d=bore_d, h=2*SR, $fn=20);
    }
}

module forearm() {
    sd = peg_w_d + 2*clr;
    depth = peg_w_l + 3;
    difference() {
        union() {
            hinge_tongue(rr_a, 4, 2.0, -1, 120);
            translate([0,0,-L_fa]) cylinder(r1=5.8, r2=rr_a-1, h=L_fa-rr_a-1.5, $fn=40);
            translate([0,0,-L_fa]) cylinder(r=5.8, h=5, $fn=40);
        }
        relief_cut(([0,0,-1] - [0, -sin(120), cos(120)])/norm([0,0,-1] - [0, -sin(120), cos(120)]), rr_a);
        translate([0,0,-L_fa-1]) cylinder(d=bore_d, h=L_fa+1, $fn=20);
        // wrist peg socket + detent groove (knot seats on the socket floor)
        translate([0,0,-L_fa-1]) cylinder(d=sd, h=depth+1, $fn=36);
        translate([0,0,-L_fa+peg_w_l-3]) cylinder(d=sd+2*ridge+2*clr, h=1.2, $fn=36);
    }
}

module hand() {   // origin = wrist face plane; peg goes up (+Z) into forearm
    difference() {
        union() {
            cylinder(d=peg_w_d, h=peg_w_l, $fn=36);
            translate([0,0,peg_w_l-3]) cylinder(d=peg_w_d+2*ridge, h=1, $fn=36);   // detent ridge
            // palm + fingers
            hull() {
                translate([0,0,-3]) scale([1,0.5,0.8]) sphere(7.5, $fn=28);
                translate([0,0.5,-14]) scale([1,0.45,0.6]) sphere(7, $fn=28);
            }
            for (i = [0:3]) hull() {
                translate([-5.4+i*3.6, -0.5, -13]) sphere(1.9, $fn=14);
                translate([-5.4+i*3.6, -1.7, -26 - [2,5,5,0][i]]) sphere(1.6, $fn=14);
            }
            hull() {   // thumb
                translate([6.2, -2, -7]) sphere(2.2, $fn=14);
                translate([9.5, -5, -14]) sphere(1.7, $fn=14);
            }
        }
        translate([0,0,-1]) cylinder(d=bore_d, h=peg_w_l+2, $fn=16);   // optional tie-off hole
    }
}

// ====================================================================== LEGS
module thigh() {
    rs = 5.0;
    difference() {
        union() {
            ball_pos(HR, rs, 0);
            translate([0,0,-22]) cylinder(r=rs, h=22, $fn=32);
            translate([0,0,-(L_th-16)]) cylinder(r1=rr_l, r2=7.5, h=(L_th-16)-22, $fn=44);
            translate([0,0,-L_th]) hinge_clevis(rr_l, 5, 2.6, 1, 125);
        }
        translate([0,0,-L_th+1]) cylinder(d=bore_d, h=L_th + HR, $fn=20);
        translate([0,0,-HR-1]) cylinder(d=bore_d, h=2*HR, $fn=20);
    }
}

module shin() {
    sd = peg_a_d + 2*clr;
    difference() {
        union() {
            hinge_tongue(rr_l, 5, 2.6, 1, 125);
            translate([0,0,-L_sh]) cylinder(r1=7.5, r2=rr_l-0.5, h=L_sh-rr_l-1.5, $fn=44);
            translate([0,0,-L_sh]) cylinder(r=7.5, h=6, $fn=40);
        }
        relief_cut(([0,0,-1] - [0, sin(125), cos(125)])/norm([0,0,-1] - [0, sin(125), cos(125)]), rr_l, 0.9);
        translate([0,0,-L_sh-1]) cylinder(d=bore_d, h=L_sh+1, $fn=20);
        translate([0,0,-L_sh-1]) cylinder(d=sd, h=peg_a_l+4, $fn=36);
        translate([0,0,-L_sh+peg_a_l-3]) cylinder(d=sd+2*ridge+2*clr, h=1.2, $fn=36);
    }
}

module foot() {   // origin = ankle face plane, peg up, toes toward -Y
    difference() {
        union() {
            cylinder(d=peg_a_d, h=peg_a_l, $fn=36);
            translate([0,0,peg_a_l-3]) cylinder(d=peg_a_d+2*ridge, h=1, $fn=36);
            intersection() {
                translate([-30,-40,-foot_h]) cube([60, 80, foot_h]);
                union() {
                    hull() {
                        translate([0,0,-1]) cylinder(r=7.5, h=1, $fn=36);
                        translate([0,5,-8]) sphere(8, $fn=28);
                    }
                    hull() {
                        translate([0,5,-8]) sphere(8, $fn=28);
                        translate([0,-11,-9]) scale([1,1,0.8]) sphere(8.5, $fn=28);
                    }
                    hull() {
                        translate([0,-11,-9]) scale([1,1,0.8]) sphere(8.5, $fn=28);
                        translate([0,-20,-11]) scale([1,1,0.6]) sphere(6.5, $fn=28);
                    }
                }
            }
        }
        translate([0,0,-1]) cylinder(d=bore_d, h=peg_a_l+2, $fn=16);
    }
}

// ====================================================================== ASSEMBLY
Zk = foot_h + L_sh;            // knee pivot height
Zh = Zk + L_th;                // hip centre height
Zw = Zh - hip_z;               // waist ball centre height
Zn = Zw + chest_neck_z;        // lower neck ball centre

module leg(s) {                // s=+1 right leg (+X)
    translate([s*hip_x, 0, Zh]) rotate([-pose_hip_y, 0, 0]) {
        color("Ivory") thigh();
        translate([0,0,-L_th]) rotate([pose_knee, 0, 0]) {
            color("Linen") shin();
            translate([0,0,-L_sh]) color("Ivory") foot();
        }
    }
}
module arm(s) {
    translate([s*sh_x, 0, Zw + sh_z]) rotate([-pose_arm_raise_y, -s*pose_arm_raise_x, 0]) {
        color("Ivory") upper_arm();
        translate([0,0,-L_ua]) rotate([-pose_elbow, 0, 0]) {
            color("Linen") forearm();
            translate([0,0,-L_fa]) color("Ivory") mirror([s < 0 ? 1 : 0, 0, 0]) hand();
        }
    }
}

module assembly() {
    translate([0, 0, Zw]) color("Linen") pelvis();
    translate([0, 0, Zw]) rotate([-pose_waist_lean, 0, pose_waist_turn]) {
        color("Ivory") chest();
        translate([0, 0, chest_neck_z]) {
            color("Linen") neck();
            translate([0, 0, neck_len]) rotate([-pose_head_tilt, 0, pose_head_turn])
                translate([-neck_c[0], -neck_c[1], -neck_c[2]]) {
                    color("Ivory") head_front();
                    color("Linen") head_back();
                }
        }
    }
    for (s = [-1, 1]) { arm(s); leg(s); }
}

// ====================================================================== PRINT PLATE
// Reference layout (spans several beds - print part by part with `part=`).
// Limbs lie on their side so hinge arms / pin stubs are vertical walls (strong across layers).
module lay() rotate([90,0,0]) children();
module plate() {
    translate([-110, 45, 0]) rotate([-90,0,0]) translate([0,-y_cut,0]) head_front();   // cut face on the bed
    translate([ -25, 45, 0]) rotate([ 90,0,0]) translate([0,-y_cut,0]) head_back();
    translate([  70, 45, 9]) chest();
    translate([ 155, 45, 37]) pelvis();
    for (i = [0,1]) translate([-110+i*25, 110, 8])   lay() upper_arm();
    for (i = [0,1]) translate([ -55+i*22, 110, 7.2]) lay() forearm();
    for (i = [0,1]) translate([  -8+i*25, 110, 6.7]) lay() hand();
    for (i = [0,1]) translate([  40+i*28, 110, 10])  lay() thigh();
    for (i = [0,1]) translate([ 100+i*26, 110, 9.5]) lay() shin();
    for (i = [0,1]) translate([ 155+i*28, 150, 14]) foot();
    translate([ 210, 110, 6.5]) lay() neck();
    for (i = [0,1]) translate([ 225+i*8, 60, 0]) dowel();
}

// ====================================================================== dispatch
if      (part == "assembly")   assembly();
else if (part == "plate")      plate();
else if (part == "head_front") head_front();
else if (part == "head_back")  head_back();
else if (part == "neck")       neck();
else if (part == "chest")      chest();
else if (part == "pelvis")     pelvis();
else if (part == "upper_arm")  upper_arm();
else if (part == "forearm")    forearm();
else if (part == "hand")       hand();
else if (part == "thigh")      thigh();
else if (part == "shin")       shin();
else if (part == "foot")       foot();
else if (part == "dowel")      dowel();
