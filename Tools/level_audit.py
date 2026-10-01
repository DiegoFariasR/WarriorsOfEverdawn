"""Audit level layouts without launching Godot (Docs/Design/level-layouts.md).

  python Tools/level_audit.py [<layout.json> ...] [--verbose]

With no layout named, audits every *.layout.json under GodotClient/config/levels/. Exit 1 when:
  - an asset or texture file is missing, or a placement names an asset the layout does not list;
  - two solid pieces run into each other (walls and pillars may: they are built to join);
  - a marker stands inside a solid, or closer to one than a character's body;
  - a solid stands in an area named "gate";
  - a piece lies off the ground.
Adapted from Everdawn's Tools/level_audit.py, which checks its side-view battle stages.
"""
import argparse
import glob
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import level_common as lc  # noqa: E402

# Pieces that make up the walls may run into each other.
STRUCTURE = ("wall", "pillar")
BODY_RADIUS = 0.5
# Half the ground's size (GodotClient/scenes/Arena.tscn, Ground): east-west and north-south.
GROUND_HALF = (35.0, 40.0)
# Touching boxes are not an overlap.
TOUCH = 0.02


def audit(path, verbose):
    errors = []
    with open(path, encoding="utf-8") as f:
        layout = json.load(f)
    if layout.get("version") != lc.LAYOUT_VERSION:
        return [f"version {layout.get('version')}, expected {lc.LAYOUT_VERSION}"]

    assets = layout["assets"]
    for name, res in assets.items():
        if not os.path.isfile(lc.res_to_path(res)):
            errors.append(f"{name}: missing file {res}")
    for prefix, texture in layout.get("textures", {}).items():
        if not os.path.isfile(lc.res_to_path(texture)):
            errors.append(f"texture for {prefix}: missing file {texture}")
    if errors:
        return errors

    solids = []
    for i, p in enumerate(layout["placements"]):
        if p["asset"] not in assets:
            errors.append(f"placement {i}: asset {p['asset']!r} is not in the layout's assets")
            continue
        x, _, z = p["position"]
        if abs(x) > GROUND_HALF[0] or abs(z) > GROUND_HALF[1]:
            errors.append(f"{p['asset']} at ({x}, {z}) lies off the ground")
        if p.get("solid"):
            if len(set(p["scale"])) != 1:
                errors.append(f"{p['asset']} at ({x}, {z}): solids need a uniform scale, got {p['scale']}")
                continue
            rect = lc.footprint_rect(assets[p["asset"]], p["position"], lc.yaw_of(p["rotation"]), p["scale"][0])
            solids.append((p["asset"], rect))

    for i, (name_a, rect_a) in enumerate(solids):
        for name_b, rect_b in solids[i + 1:]:
            if name_a.startswith(STRUCTURE) and name_b.startswith(STRUCTURE):
                continue
            if lc.rects_overlap(rect_a, rect_b, slack=TOUCH):
                errors.append(f"{name_a} at ({rect_a[0]:.1f}, {rect_a[1]:.1f}) runs into {name_b} at ({rect_b[0]:.1f}, {rect_b[1]:.1f})")

    for marker in layout.get("markers", []):
        x, _, z = marker["position"]
        for name, rect in solids:
            if lc.point_in_rect(x, z, rect, margin=BODY_RADIUS):
                errors.append(f"marker {marker['name']} at ({x}, {z}) has no room: {name} is in the way")

    for area in layout.get("areas", []):
        if area["name"] != "gate":
            continue
        (x0, z0), (x1, z1) = area["min"], area["max"]
        opening = ((x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2, (z1 - z0) / 2, 0.0)
        for name, rect in solids:
            if lc.rects_overlap(opening, rect, slack=TOUCH):
                errors.append(f"{name} at ({rect[0]:.1f}, {rect[1]:.1f}) stands in the gate")

    if verbose:
        print(f"  {len(layout['placements'])} placements, {len(solids)} solid, {len(layout.get('markers', []))} markers, "
              f"{len(layout.get('areas', []))} areas")
    return errors


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("layouts", nargs="*")
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    paths = args.layouts or sorted(glob.glob(os.path.join(lc.LEVELS_DIR, "*.layout.json")))
    if not paths:
        print(f"[level-audit] no layouts under {lc.LEVELS_DIR}")
        return 1

    failed = 0
    for path in paths:
        errors = audit(path, args.verbose)
        print(f"[level-audit] {os.path.basename(path)}: {'clean' if not errors else f'{len(errors)} problem(s)'}")
        for e in errors:
            print(f"  FAIL {e}")
        failed += bool(errors)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
