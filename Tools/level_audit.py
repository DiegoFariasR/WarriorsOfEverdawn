"""Audit level layouts without launching Godot (Docs/Design/level-layouts.md).

  python Tools/level_audit.py [<layout.json> ...] [--verbose]

With no layout named, audits every *.layout.json under GodotClient/config/levels/. Exit 1 when:
  - an asset or texture file is missing, or a placement names an asset the layout does not list;
  - two solid pieces on the same floor run into each other (walls, pillars, floors and stairs may: they are built
    to join; a storey's floor over a room is no overlap);
  - a marker stands inside a solid, or closer to one than a character's body, at its own height;
  - a solid stands in an area named "gate", or one other than the doorway itself in an area named "door", on the
    area's floor;
  - a flight of stairs (shape "ramp") has no floor at its own height, or no room for a body, straight on past its top
    or its bottom: a flight is walked on and off along its length;
  - a piece lies off the ground;
  - a piece is breakable but not a solid that blocks as its box does.
Adapted from Everdawn's Tools/level_audit.py, which checks its side-view battle stages.
"""
import argparse
import glob
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import level_common as lc  # noqa: E402

# Pieces that make up the walls, floors and stairs may run into each other.
STRUCTURE = ("wall", "pillar", "floor", "stairs")
BODY_RADIUS = 0.5
BODY_HEIGHT = 2.2
SHAPES = (None, "mesh", "ramp")
VIEWS = (None, "overhead", "around")
# Half the ground's size (ArenaMap.Floors.cs, GroundHalf): east-west and north-south.
GROUND_HALF = (35.0, 40.0)
# Touching boxes are not an overlap.
TOUCH = 0.02
# Past each end of a flight of stairs, this far on, a body steps off: across its width at these shares of it. A floor
# meets the end when its top is this close to the end's height.
STEP_OFF = (0.3, 0.8)
ACROSS_FLIGHT = (0.2, 0.5, 0.8)
FLOOR_MEETS = 0.1


def stairs_errors(layout, assets, solids):
    """Each flight's two ends: floor at the end's height straight on past it, and nothing solid where a body stands
    there. The floor is a solid floor piece with its top at that height, or the ground at 0 outside the holes."""
    errors = []
    holes = [a for a in layout.get("areas", []) if a["name"] == "hole"]
    floors = [(rect, span) for name, rect, span in solids if name.startswith("floor")]
    others = [(name, rect, span) for name, rect, span in solids if not name.startswith(("floor", "stairs"))]
    for p in layout["placements"]:
        if p.get("shape") != "ramp":
            continue
        lo, hi = lc.bounds(assets[p["asset"]])
        sx, sy, sz = lc.scales(p["scale"])
        yaw, (px, py, pz) = lc.yaw_of(p["rotation"]), p["position"]
        # The kit's stairs rise toward their own -Z: the top end is at the box's -Z face, the bottom at its +Z face.
        for end, z_end, height, outward in (("top", lo[2] * sz, py + hi[1] * sy, -1), ("bottom", hi[2] * sz, py + lo[1] * sy, 1)):
            for along in STEP_OFF:
                for share in ACROSS_FLIGHT:
                    ox, oz = lc.turned((lo[0] + (hi[0] - lo[0]) * share) * sx, z_end + outward * along, yaw)
                    x, z = px + ox, pz + oz
                    on_floor = any(abs(span[1] - height) <= FLOOR_MEETS and lc.point_in_rect(x, z, rect) for rect, span in floors)
                    on_ground = abs(height) <= FLOOR_MEETS and not any(
                        h["min"][0] <= x <= h["max"][0] and h["min"][1] <= z <= h["max"][1] for h in holes)
                    in_the_way = [name for name, rect, span in others
                                  if lc.spans_overlap((height + 0.1, height + BODY_HEIGHT), span) and lc.point_in_rect(x, z, rect)]
                    where = f"{p['asset']} at ({px}, {pz}): {along} past its {end} ({x:.2f}, {z:.2f})"
                    if not (on_floor or on_ground):
                        errors.append(f"{where} has no floor at {height:.2f} to step off onto")
                    if in_the_way:
                        errors.append(f"{where} is taken by {', '.join(sorted(set(in_the_way)))}")
    return errors


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

    # Every solid, and those among them that block as their whole box does (a doorway blocks as its mesh does).
    solids, boxes = [], []
    for i, p in enumerate(layout["placements"]):
        if p["asset"] not in assets:
            errors.append(f"placement {i}: asset {p['asset']!r} is not in the layout's assets")
            continue
        x, _, z = p["position"]
        if abs(x) > GROUND_HALF[0] or abs(z) > GROUND_HALF[1]:
            errors.append(f"{p['asset']} at ({x}, {z}) lies off the ground")
        if p.get("view") not in VIEWS:
            errors.append(f"{p['asset']} at ({x}, {z}): view {p['view']!r}, expected \"overhead\", \"around\" or none")
        if p.get("solid"):
            rect = lc.footprint_rect(assets[p["asset"]], p["position"], lc.yaw_of(p["rotation"]), p["scale"])
            span = lc.height_span(assets[p["asset"]], p["position"][1], p["scale"])
            solids.append((p["asset"], rect, span))
            if p.get("shape") not in SHAPES:
                errors.append(f"{p['asset']} at ({x}, {z}): shape {p['shape']!r}, expected \"mesh\", \"ramp\" or none")
            if p.get("shape") is None:
                boxes.append((p["asset"], rect, span))
        elif "shape" in p:
            errors.append(f"{p['asset']} at ({x}, {z}): a shape on a piece that is not solid")
        if p.get("breakable") and (not p.get("solid") or p.get("shape") is not None):
            errors.append(f"{p['asset']} at ({x}, {z}): breakable, but not a solid that blocks as its box does")

    for i, (name_a, rect_a, span_a) in enumerate(solids):
        for name_b, rect_b, span_b in solids[i + 1:]:
            if name_a.startswith(STRUCTURE) and name_b.startswith(STRUCTURE):
                continue
            if lc.spans_overlap(span_a, span_b, slack=TOUCH) and lc.rects_overlap(rect_a, rect_b, slack=TOUCH):
                errors.append(f"{name_a} at ({rect_a[0]:.1f}, {rect_a[1]:.1f}) runs into {name_b} at ({rect_b[0]:.1f}, {rect_b[1]:.1f})")

    for marker in layout.get("markers", []):
        x, y, z = marker["position"]
        body = (y + 0.1, y + BODY_HEIGHT)
        for name, rect, span in solids:
            if lc.spans_overlap(body, span) and lc.point_in_rect(x, z, rect, margin=BODY_RADIUS):
                errors.append(f"marker {marker['name']} at ({x}, {z}) has no room: {name} is in the way")

    for area in layout.get("areas", []):
        if area["name"] not in ("gate", "door"):
            continue
        (x0, z0), (x1, z1) = area["min"], area["max"]
        opening = ((x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2, (z1 - z0) / 2, 0.0)
        floor = lc.STOREY * area.get("level", 0)
        for name, rect, span in solids if area["name"] == "gate" else boxes:
            if lc.spans_overlap((floor, floor + BODY_HEIGHT), span, slack=TOUCH) and lc.rects_overlap(opening, rect, slack=TOUCH):
                errors.append(f"{name} at ({rect[0]:.1f}, {rect[1]:.1f}) stands in the {'gate' if area['name'] == 'gate' else 'doorway'}")

    errors.extend(stairs_errors(layout, assets, solids))

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
