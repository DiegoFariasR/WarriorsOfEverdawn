"""What a fortress layout has in an area: placements with their boxes, markers and areas (Docs/Design/level-layouts.md).

  python Tools/level_list.py <town|fortress> [x0,z0,x1,z1] [--room -1|1]

The area is in world coordinates (x, z), the whole layout when left out. With --room, the room off that side wall
(-1 or +1, as Tools/level_fortresses.py builds it): everything is given in the room's own (u, v), the area is read
in (u, v) and defaults to the whole room, so a placement can be moved in the generator by the numbers shown.
"""
import argparse
import json
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import level_common as lc  # noqa: E402
import level_fortresses as lf  # noqa: E402

# Each fortress's file, the middle of its courtyard and the way its gate faces (Fort's centre_z and front).
FORTS = {
    "town": ("allied-town", lf.TOWN_Z, -1),
    "fortress": ("enemy-fortress", lf.FORTRESS_Z, 1),
}
HALF_X = lf.ACROSS * lf.SEGMENT / 2
ROOM_OUT, ROOM_HALF_ALONG = lf.ROOM_OUT * lf.SEGMENT, lf.ROOM_ALONG * lf.SEGMENT / 2


class Frame:
    """World (x, z) to the frame things are listed in: the world's own, or a room's (u, v)."""

    def __init__(self, cz, front, side):
        self.cz, self.front, self.side = cz, front, side

    def of(self, wx, wz):
        if self.side is None:
            return wx, wz
        # Fort.world and Room.at, the other way round.
        x, d = self.front * wx, self.front * (wz - self.cz)
        return self.side * x - HALF_X, d

    def yaw(self, world_yaw):
        if self.side is None:
            return world_yaw
        fort_yaw = world_yaw - (0.0 if self.front > 0 else math.pi)
        return fort_yaw / self.side + math.pi / 2


def parse_area(text):
    parts = [float(p) for p in text.split(",")]
    if len(parts) != 4:
        raise SystemExit(f"[level-list] an area is x0,z0,x1,z1, got {text!r}")
    return min(parts[0], parts[2]), min(parts[1], parts[3]), max(parts[0], parts[2]), max(parts[1], parts[3])


def extent(points):
    xs, zs = [p[0] for p in points], [p[1] for p in points]
    return min(xs), min(zs), max(xs), max(zs)


def overlaps(a, b):
    return a[0] <= b[2] and b[0] <= a[2] and a[1] <= b[3] and b[1] <= a[3]


def degrees(yaw):
    return round(math.degrees(yaw)) % 360


def main():
    # An area can start with a minus, which argparse would take for an option: it is picked out first.
    areas = [a for a in sys.argv[1:] if re.fullmatch(r"-?[\d.]+(,-?[\d.]+){3}", a)]
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("fort", choices=sorted(FORTS))
    parser.add_argument("--room", type=int, choices=(-1, 1))
    args = parser.parse_args([a for a in sys.argv[1:] if a not in areas])
    args.area = areas[0] if areas else None

    name, cz, front = FORTS[args.fort]
    frame = Frame(cz, front, args.room)
    if args.area:
        area = parse_area(args.area)
    elif args.room is not None:
        area = (0.0, -ROOM_HALF_ALONG, ROOM_OUT, ROOM_HALF_ALONG)
    else:
        area = (-math.inf, -math.inf, math.inf, math.inf)
    with open(os.path.join(lc.LEVELS_DIR, f"{name}.layout.json"), encoding="utf-8") as f:
        layout = json.load(f)
    a, b = ("u", "v") if args.room is not None else ("x", "z")

    print(f"[level-list] {name}: {'room ' + format(args.room, '+d') if args.room is not None else 'world'} "
          f"{a} {area[0]}..{area[2]}, {b} {area[1]}..{area[3]}")
    for p in layout["placements"]:
        res = layout["assets"][p["asset"]]
        yaw = lc.yaw_of(p["rotation"])
        box = extent([frame.of(x, z) for x, z in lc.rect_corners(lc.footprint_rect(res, p["position"], yaw, p["scale"]))])
        if not overlaps(box, area):
            continue
        low, high = lc.height_span(res, p["position"][1], p["scale"])
        u, v = frame.of(p["position"][0], p["position"][2])
        kind = ("solid " + p.get("shape", "box")) if p.get("solid") else "loose"
        scale = p["scale"][0] if len(set(p["scale"])) == 1 else p["scale"]
        print(f"  {p['asset']:<28} at {a}={u:.2f} {b}={v:.2f} y={p['position'][1]:.2f}  yaw {degrees(frame.yaw(yaw)):>3}  "
              f"scale {scale}  {kind:<11} box {a} {box[0]:.2f}..{box[2]:.2f} {b} {box[1]:.2f}..{box[3]:.2f} "
              f"y {low:.2f}..{high:.2f}")
    for m in layout.get("markers", []):
        u, v = frame.of(m["position"][0], m["position"][2])
        if area[0] <= u <= area[2] and area[1] <= v <= area[3]:
            print(f"  marker {m['name']:<21} at {a}={u:.2f} {b}={v:.2f} y={m['position'][1]:.2f}")
    for ar in layout.get("areas", []):
        box = extent([frame.of(ar["min"][0], ar["min"][1]), frame.of(ar["max"][0], ar["max"][1])])
        if overlaps(box, area):
            print(f"  area {ar['name']:<23} {a} {box[0]:.2f}..{box[2]:.2f} {b} {box[1]:.2f}..{box[3]:.2f} level {ar.get('level', 0)}")


if __name__ == "__main__":
    main()
