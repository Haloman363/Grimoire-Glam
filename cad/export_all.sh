#!/bin/sh
# Export every printable part to ./stl (run in parallel; needs openscad)
set -e
cd "$(dirname "$0")"
mkdir -p stl
for p in head_front head_back neck chest pelvis upper_arm forearm hand thigh shin foot dowel; do
    openscad -o "stl/$p.stl" -D "part=\"$p\"" blythe_doll.scad &
done
wait
