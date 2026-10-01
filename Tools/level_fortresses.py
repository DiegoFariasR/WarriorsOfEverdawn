"""Generate the two fortresses of the arena as level layouts (Docs/Design/level-layouts.md).

  python Tools/level_fortresses.py [--out-dir <dir>] [--seed N]

Writes allied-town.layout.json and enemy-fortress.layout.json (default: GodotClient/config/levels/). Each is a
walled courtyard on the dungeon kit's 4 m grid with corner towers and one gate facing the other across the field:
the town to the south, where players start and are safe; the fortress to the north, where the waves rise. Pieces are
placed by their measured boxes; a solid that would overlap another stops the run. Check the result with
`./dev.sh level-audit`.
"""
import argparse
import json
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import level_common as lc  # noqa: E402

DUNGEON = "res://assets/dungeon/{}.glb"
PROPS = "res://assets/props/{}.glb"
DUNGEON_PREFIX = "res://assets/dungeon/"
STONE = "res://assets/textures/dungeon/dungeon_texture.png"
NIGHT = "res://assets/textures/dungeon/alt_texture_5_NightA.png"

# The kit's wall segment is this wide and this thick.
SEGMENT = 4.0
WALL_THICK = 1.0
# Courtyards are this many segments across (east-west) and deep (north-south).
ACROSS, DEEP = 5, 4
# Centre of each courtyard on the north-south axis; -Z is north, up the screen in the default camera.
TOWN_Z, FORTRESS_Z = 22.0, -22.0
# Gate towers stand this far either side of the gate's middle, leaving a 3 m opening.
GATE_TOWER_OFFSET = 2.4
GATE_TOWER_SCALE = 1.2
CORNER_TOWER_SCALE = 1.5
# A character's body, for keeping spawn points and the gate clear.
BODY_RADIUS = 0.5
# Pieces that make up the walls may run into each other; everything else solid must stand clear.
STRUCTURE = ("wall", "pillar")


class Fort:
    """One courtyard. `front` is +1 when its gate faces +Z (south) and -1 when it faces -Z (north)."""

    def __init__(self, centre_z, front, texture, seed):
        self.cz, self.front, self.rng = centre_z, front, random.Random(seed)
        self.half_x, self.half_z = ACROSS * SEGMENT / 2, DEEP * SEGMENT / 2
        self.assets, self.placements, self.solids = {}, [], []
        self.markers, self.areas = [], []
        # The longest prefix wins: banners keep their own colours whatever the walls wear.
        self.textures = {DUNGEON_PREFIX: texture, DUNGEON_PREFIX + "banner_": STONE}

    # --- coordinates: (x, d) with d measured from the courtyard's centre toward its gate ---

    def world(self, x, d):
        return (x, self.cz + self.front * d)

    def facing(self, yaw_toward_gate):
        """Yaw for a piece whose own +Z should point toward the gate (0) or turned from that."""
        return (0.0 if self.front > 0 else math.pi) + yaw_toward_gate

    def put(self, res_format, name, x, d, yaw=0.0, scale=1.0, solid=False, y=0.0, centred=True):
        """Places a piece with the middle of its box at (x, d), or its own origin there when not centred."""
        res = res_format.format(name)
        yaw = self.facing(yaw)
        wx, wz = self.world(x, d)
        if centred:
            lo, hi = lc.bounds(res)
            ox, oz = lc.turned((lo[0] + hi[0]) / 2 * scale, (lo[2] + hi[2]) / 2 * scale, yaw)
            wx, wz = wx - ox, wz - oz
        position = [round(wx, 3), round(y, 3), round(wz, 3)]
        if solid:
            rect = lc.footprint_rect(res, position, yaw, scale)
            structure = name.startswith(STRUCTURE)
            for other_name, other_rect in self.solids:
                if structure and other_name.startswith(STRUCTURE):
                    continue
                if lc.rects_overlap(rect, other_rect, slack=0.02):
                    raise SystemExit(f"[level-fortresses] {name} at ({x}, {d}) runs into {other_name}")
            self.solids.append((name, rect))
        self.assets.setdefault(name, res)
        placement = {"asset": name, "position": position, "rotation": lc.yaw_quat(yaw), "scale": [round(scale, 3)] * 3}
        if solid:
            placement["solid"] = True
        self.placements.append(placement)

    def marker(self, name, x, d):
        wx, wz = self.world(x, d)
        if any(lc.point_in_rect(wx, wz, rect, margin=BODY_RADIUS) for _, rect in self.solids):
            raise SystemExit(f"[level-fortresses] marker {name} at ({x}, {d}) is inside a solid")
        self.markers.append({"name": name, "position": [round(wx, 3), 0.0, round(wz, 3)]})

    def area(self, name, x0, d0, x1, d1):
        (ax, az), (bx, bz) = self.world(x0, d0), self.world(x1, d1)
        self.areas.append({"name": name, "min": [round(min(ax, bx), 3), round(min(az, bz), 3)],
                           "max": [round(max(ax, bx), 3), round(max(az, bz), 3)]})

    # --- structure ---

    def walls(self, plain, worn=()):
        """The four walls with a gate in the middle of the front one, towers at the corners and at the gate."""
        def pick():
            return self.rng.choice(worn) if worn and self.rng.random() < 0.3 else plain

        for i in range(ACROSS):
            x = (i - (ACROSS - 1) / 2) * SEGMENT
            self.put(DUNGEON, pick(), x, -self.half_z, solid=True)
            if i != ACROSS // 2:
                self.put(DUNGEON, pick(), x, self.half_z, solid=True)
        for i in range(DEEP):
            d = (i - (DEEP - 1) / 2) * SEGMENT
            for side in (-1, 1):
                self.put(DUNGEON, pick(), side * self.half_x, d, yaw=math.pi / 2, solid=True)
        for sx in (-1, 1):
            for sd in (-1, 1):
                self.put(DUNGEON, "pillar", sx * self.half_x, sd * self.half_z, scale=CORNER_TOWER_SCALE, solid=True)
            self.put(DUNGEON, "pillar", sx * GATE_TOWER_OFFSET, self.half_z, scale=GATE_TOWER_SCALE, solid=True)

        # The opening between the gate towers, and the strip of ground just outside it.
        tower_half = (lc.bounds(DUNGEON.format("pillar"))[1][0]) * GATE_TOWER_SCALE
        opening = GATE_TOWER_OFFSET - tower_half
        self.area("gate", -opening, self.half_z - WALL_THICK / 2, opening, self.half_z + WALL_THICK / 2)
        self.marker("gate", 0.0, self.half_z)
        self.marker("outside-gate", 0.0, self.half_z + 3.0)

        # The ground inside the walls; the gate's threshold is not part of it.
        self.area("courtyard", -self.half_x + WALL_THICK / 2, -self.half_z + WALL_THICK / 2,
                  self.half_x - WALL_THICK / 2, self.half_z - WALL_THICK / 2)

    def floor(self, tile):
        for i in range(ACROSS):
            for j in range(DEEP):
                self.put(DUNGEON, tile, (i - (ACROSS - 1) / 2) * SEGMENT, (j - (DEEP - 1) / 2) * SEGMENT,
                         yaw=self.rng.randrange(4) * math.pi / 2)

    def on_wall(self, name, side, along, height=0.0, inside=True):
        """Hangs a piece modelled against a wall's +Z face (banners, torches) on one of the four walls.

        side: 'back', 'front', 'left', 'right'; along: metres from the wall's middle.
        """
        face = 1.0 if inside else -1.0
        if side == "back":
            x, d, yaw = along, -self.half_z, 0.0 if inside else math.pi
        elif side == "front":
            x, d, yaw = along, self.half_z, math.pi if inside else 0.0
        elif side == "left":
            x, d, yaw = -self.half_x, along, math.pi / 2 * face
        else:
            x, d, yaw = self.half_x, along, -math.pi / 2 * face
        self.put(DUNGEON, name, x, d, yaw=yaw, y=height, centred=False)

    def scatter(self, res_format, names, spots, scale=(1.0, 1.0)):
        """Small pieces lying about, turned any way: walked over, never solid."""
        for x, d in spots:
            self.put(res_format, self.rng.choice(names), x + self.rng.uniform(-0.3, 0.3), d + self.rng.uniform(-0.3, 0.3),
                     yaw=self.rng.uniform(0, 2 * math.pi), scale=self.rng.uniform(*scale))

    def layout(self):
        return {"version": lc.LAYOUT_VERSION, "assets": self.assets, "textures": self.textures,
                "placements": self.placements, "markers": self.markers, "areas": self.areas, "meshes": []}


def allied_town(seed):
    f = Fort(TOWN_Z, front=-1, texture=STONE, seed=seed)
    f.floor("floor_tile_large")
    f.walls("wall", worn=("wall_window_open", "wall_archedwindow_open"))

    # A statue at the back, facing the gate.
    f.put(PROPS, "paladin_statue", 0.0, -5.6, solid=True)

    # A camp fire with benches on the left, a long table on the right.
    f.put(PROPS, "Campfire_Base", -5.4, 0.6)
    f.put(PROPS, "Campfire_Logs", -5.4, 0.6)
    f.put(DUNGEON, "bench", -5.4, 2.4, solid=True)
    f.put(DUNGEON, "bench", -7.3, 0.6, yaw=math.pi / 2, solid=True)
    f.put(DUNGEON, "table_long", 6.0, 0.2, solid=True)
    f.put(DUNGEON, "bench", 4.5, 0.2, yaw=math.pi / 2, solid=True)
    f.put(DUNGEON, "bench", 7.5, 0.2, yaw=math.pi / 2, solid=True)

    # Work corners: a forge on the left wall, a training dummy on the right.
    f.put(PROPS, "anvil", -8.0, -1.6, yaw=math.radians(90), solid=True)
    f.put(PROPS, "Trainingdummy", 7.9, -2.4, yaw=math.radians(-60), solid=True)

    # Stores stacked in the front corners, clear of the gate.
    f.put(DUNGEON, "barrel_large", -7.9, 6.0, solid=True)
    f.put(DUNGEON, "crates_stacked", -5.6, 6.1, yaw=math.radians(8), solid=True)
    f.put(DUNGEON, "keg", 7.8, 5.9, yaw=math.pi / 2, solid=True)
    f.put(DUNGEON, "box_large", 5.6, 6.3, yaw=math.radians(-12), solid=True)
    f.put(DUNGEON, "barrel_small_stack", 8.5, 3.5, yaw=math.pi / 2, solid=True)
    f.put(DUNGEON, "trunk_large_A", -8.6, 3.8, yaw=math.pi / 2, solid=True)

    # Lanterns inside the gate; colours on every wall, and outside either side of the gate.
    for sx in (-1, 1):
        f.put(PROPS, "post_lantern", sx * 3.9, 6.2, yaw=math.pi, solid=True)
        f.on_wall("banner_shield_blue", "front", sx * 6.0, inside=False)
        f.on_wall("banner_blue", "front", sx * 6.0)
        f.on_wall("torch_mounted", "front", sx * 4.0, height=2.3, inside=False)
        f.on_wall("banner_patternA_blue", "left" if sx < 0 else "right", -4.0)
        f.on_wall("torch_mounted", "left" if sx < 0 else "right", 0.0, height=2.3)
        f.on_wall("banner_blue", "left" if sx < 0 else "right", 4.0)
        f.on_wall("banner_patternA_blue", "back", sx * 6.0)
        f.on_wall("torch_mounted", "back", sx * 3.0, height=2.3)
    f.on_wall("banner_shield_blue", "back", 0.0)

    # Players start, and get back up, in a ring in front of the statue.
    for i in range(8):
        a = 2 * math.pi * i / 8
        f.marker("player-spawn", 2.6 * math.sin(a), 0.4 + 2.6 * math.cos(a))
    f.area("safe", -f.half_x + WALL_THICK / 2, -f.half_z + WALL_THICK / 2, f.half_x - WALL_THICK / 2, f.half_z - WALL_THICK / 2)
    return f.layout()


def enemy_fortress(seed):
    f = Fort(FORTRESS_Z, front=1, texture=NIGHT, seed=seed)
    f.floor("floor_dirt_large")
    f.walls("wall", worn=("wall_cracked", "wall_broken"))

    # The crypt the dead come from fills the back; graves and coffins line the sides.
    f.put(PROPS, "crypt", 0.0, -3.2, solid=True)
    for sx in (-1, 1):
        f.put(PROPS, "grave_A" if sx < 0 else "grave_A_destroyed", sx * 6.4, -6.0, solid=True)
        f.put(PROPS, "gravestone", sx * 8.4, -3.6, yaw=sx * math.radians(12), solid=True)
        f.put(PROPS, "grave_A_destroyed" if sx < 0 else "grave_A", sx * 6.2, -2.2, yaw=sx * math.radians(-8), solid=True)
    f.put(PROPS, "coffin", -8.0, 0.8, yaw=math.radians(15), solid=True)
    f.put(PROPS, "coffin_decorated", 8.0, 0.4, yaw=math.radians(-20), solid=True)
    f.put(PROPS, "tree_dead_large", -8.0, 5.9, solid=True)
    f.put(PROPS, "tree_dead_medium", 8.2, 5.8, yaw=math.radians(140), solid=True)

    # Skull posts inside the gate; bones underfoot.
    for sx in (-1, 1):
        f.put(PROPS, "post_skull", sx * 3.9, 6.2, yaw=math.pi, solid=True)
        f.on_wall("banner_shield_red", "front", sx * 6.0, inside=False)
        f.on_wall("banner_red", "front", sx * 6.0)
        f.on_wall("torch_mounted", "front", sx * 4.0, height=2.3, inside=False)
        f.on_wall("banner_patternB_red", "left" if sx < 0 else "right", -4.0)
        f.on_wall("torch_mounted", "left" if sx < 0 else "right", 0.0, height=2.3)
        f.on_wall("banner_red", "left" if sx < 0 else "right", 4.0)
    f.scatter(PROPS, ("bone_A", "bone_B", "bone_C"), [(-3.2, 3.6), (2.6, 2.2), (-1.0, 5.4), (4.8, 4.6), (-5.2, 1.8), (1.4, 6.6)], scale=(1.2, 1.8))
    f.scatter(PROPS, ("skull", "ribcage"), [(-4.4, 5.2), (3.8, 6.4), (5.6, 2.4)], scale=(0.8, 1.1))

    # The waves rise in the open ground between the crypt and the gate: a spot each for a tripled wave.
    for d in (1.6, 2.9, 4.2):
        for x in (-5.4, -3.6, -1.8, 0.0, 1.8, 3.6, 5.4):
            f.marker("enemy-spawn", x, d)
    return f.layout()


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--out-dir", default=lc.LEVELS_DIR)
    ap.add_argument("--seed", type=int, default=11)
    args = ap.parse_args()

    os.makedirs(args.out_dir, exist_ok=True)
    for name, build in (("allied-town", allied_town), ("enemy-fortress", enemy_fortress)):
        layout = build(args.seed)
        path = os.path.join(args.out_dir, f"{name}.layout.json")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(layout, f, indent=1)
            f.write("\n")
        solids = sum(1 for p in layout["placements"] if p.get("solid"))
        print(f"[level-fortresses] seed {args.seed}: {name}: {len(layout['placements'])} placements ({solids} solid), "
              f"{len(layout['markers'])} markers, {len(layout['areas'])} areas -> {path}")


if __name__ == "__main__":
    main()
