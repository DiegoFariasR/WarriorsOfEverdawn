"""Generate the two fortresses of the arena as level layouts (Docs/Design/level-layouts.md).

  python Tools/level_fortresses.py [--out-dir <dir>] [--seed N]

Writes allied-town.layout.json and enemy-fortress.layout.json (default: GodotClient/config/levels/). Each is a
walled courtyard on the dungeon kit's 4 m grid with corner towers, one gate facing the other across the field, and a
room off each side wall, entered through a doorway: the town to the south, where players start and are safe; the
fortress to the north, where the waves rise. Pieces are placed by their measured boxes; a solid that would overlap
another stops the run. Check the result with `./dev.sh level-audit`.
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
# Rooms reach this many segments out from the side wall and run this many along it, about the courtyard's middle.
ROOM_OUT, ROOM_ALONG = 2, 2
ROOM_TOWER_SCALE = 1.2
# The side-wall segment that is each room's doorway (its middle, toward the gate), and half the kit doorway's opening.
DOOR_D = 2.0
DOOR_HALF = 0.9
# Ground either side of a doorway that stays clear, so the way through is not walled off by a prop.
DOOR_APPROACH = 1.5
# A character's body, for keeping spawn points and the gate clear.
BODY_RADIUS = 0.5
# Pieces that make up the walls may run into each other; everything else solid must stand clear.
STRUCTURE = ("wall", "pillar")
# Banners are modelled from the wall's middle line; these have their backs at their own origin, so they are moved
# out to the wall's face (or, for the shelves, a quarter of the way).
ON_FACE = WALL_THICK / 2
SHELVES_OUT = 0.25
# Flames: scene-linear colours, as the layout format has them.
FIRE = [1.0, 0.342, 0.084]
CANDLE = [1.0, 0.57, 0.21]
# Pieces that burn, and the light each gives: where in the piece's box the flame is (0 to 1 across x, up y and along
# z, so a wall torch's is at the end that stands out from the wall), then colour, energy and range in metres. The
# models carry no light of their own, and a flame that lights nothing reads as painted on.
BURNING = {
    "torch_mounted": ((0.5, 0.95, 1.0), FIRE, 1.6, 5.0),
    "post_lantern": ((0.5, 0.75, 0.85), FIRE, 1.4, 5.0),
    "Campfire_Logs": ((0.5, 1.0, 0.5), FIRE, 2.2, 7.0),
    "shrine_candles": ((0.5, 1.05, 0.5), CANDLE, 1.0, 3.5),
    "skull_candle": ((0.5, 1.05, 0.5), CANDLE, 1.0, 3.5),
}


class Fort:
    """One courtyard with its rooms. `front` is +1 when its gate faces +Z (south) and -1 when it faces -Z (north).

    Coordinates are (x, d): d from the courtyard's middle toward its gate, x to the left as one walks out of the
    gate. A fort facing north is the same fort turned half round, not mirrored, so a yaw means the same in both.
    """

    def __init__(self, centre_z, front, texture, seed, wall, worn, safe=False):
        self.cz, self.front, self.rng = centre_z, front, random.Random(seed)
        self.wall, self.worn, self.safe = wall, worn, safe
        self.half_x, self.half_z = ACROSS * SEGMENT / 2, DEEP * SEGMENT / 2
        self.assets, self.placements, self.solids = {}, [], []
        self.markers, self.areas, self.lights = [], [], []
        # The longest prefix wins: banners keep their own colours whatever the walls wear.
        self.textures = {DUNGEON_PREFIX: texture, DUNGEON_PREFIX + "banner_": STONE}

    def world(self, x, d):
        return (self.front * x, self.cz + self.front * d)

    def facing(self, yaw_toward_gate):
        """Yaw for a piece whose own +Z should point toward the gate (0) or turned from that."""
        return (0.0 if self.front > 0 else math.pi) + yaw_toward_gate

    def put(self, res_format, name, x, d, yaw=0.0, scale=1.0, solid=False, y=0.0, centred=True, shape=None):
        """Places a piece with the middle of its box at (x, d), or its own origin there when not centred.

        shape="mesh" makes a solid block as its mesh does, not as its box: a doorway is walked through.
        """
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
            if shape is None:
                for area in self.areas:
                    if area["name"] == "door" and lc.rects_overlap(rect, area_rect(area), slack=0.02):
                        raise SystemExit(f"[level-fortresses] {name} at ({x}, {d}) stands in a doorway")
            self.solids.append((name, rect))
        self.assets.setdefault(name, res)
        placement = {"asset": name, "position": position, "rotation": lc.yaw_quat(yaw), "scale": [round(scale, 3)] * 3}
        if solid:
            placement["solid"] = True
        if shape is not None:
            placement["shape"] = shape
        self.placements.append(placement)
        if name in BURNING:
            at, colour, energy, reach = BURNING[name]
            lo, hi = lc.bounds(res)
            fx, fy, fz = (lo[i] + (hi[i] - lo[i]) * at[i] for i in range(3))
            ox, oz = lc.turned(fx * scale, fz * scale, yaw)
            self.lights.append({"position": [round(position[0] + ox, 3), round(position[1] + fy * scale, 3), round(position[2] + oz, 3)],
                                "color": colour, "energy": energy, "range": reach})

    def marker(self, name, x, d, yaw=None):
        """A named spot; with a yaw, the way whoever stands on it faces (0 toward the gate)."""
        wx, wz = self.world(x, d)
        if any(lc.point_in_rect(wx, wz, rect, margin=BODY_RADIUS) for _, rect in self.solids):
            raise SystemExit(f"[level-fortresses] marker {name} at ({x}, {d}) is inside a solid")
        marker = {"name": name, "position": [round(wx, 3), 0.0, round(wz, 3)]}
        if yaw is not None:
            marker["yaw"] = round(self.facing(yaw), 4)
        self.markers.append(marker)

    def area(self, name, x0, d0, x1, d1):
        (ax, az), (bx, bz) = self.world(x0, d0), self.world(x1, d1)
        self.areas.append({"name": name, "min": [round(min(ax, bx), 3), round(min(az, bz), 3)],
                           "max": [round(max(ax, bx), 3), round(max(az, bz), 3)]})

    def inside(self, name, x0, d0, x1, d1):
        """Ground inside the walls: named, and in the town safe as well."""
        self.area(name, x0, d0, x1, d1)
        if self.safe:
            self.area("safe", x0, d0, x1, d1)

    def pick(self):
        return self.rng.choice(self.worn) if self.worn and self.rng.random() < 0.3 else self.wall

    # --- structure ---

    def walls(self):
        """The four walls with a gate in the middle of the front one, a doorway in each side one, towers at the
        corners and at the gate."""
        for i in range(ACROSS):
            x = (i - (ACROSS - 1) / 2) * SEGMENT
            self.put(DUNGEON, self.pick(), x, -self.half_z, solid=True)
            if i != ACROSS // 2:
                self.put(DUNGEON, self.pick(), x, self.half_z, solid=True)
        for i in range(DEEP):
            d = (i - (DEEP - 1) / 2) * SEGMENT
            for side in (-1, 1):
                if d == DOOR_D:
                    self.put(DUNGEON, "wall_doorway", side * self.half_x, d, yaw=math.pi / 2, solid=True, shape="mesh")
                else:
                    self.put(DUNGEON, self.pick(), side * self.half_x, d, yaw=math.pi / 2, solid=True)
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
        self.inside("courtyard", -self.half_x + WALL_THICK / 2, -self.half_z + WALL_THICK / 2,
                    self.half_x - WALL_THICK / 2, self.half_z - WALL_THICK / 2)

        # Each doorway with the ground either side of it: declared before anything is furnished, so a prop put in the
        # way stops the run.
        for side in (-1, 1):
            near, far = side * (self.half_x - DOOR_APPROACH), side * (self.half_x + DOOR_APPROACH)
            self.inside("door", min(near, far), DOOR_D - DOOR_HALF, max(near, far), DOOR_D + DOOR_HALF)

    def floor(self, tile):
        for i in range(ACROSS):
            for j in range(DEEP):
                self.put(DUNGEON, tile, (i - (ACROSS - 1) / 2) * SEGMENT, (j - (DEEP - 1) / 2) * SEGMENT,
                         yaw=self.rng.randrange(4) * math.pi / 2)

    def room(self, side, floor):
        """The room off one side wall (-1 or +1): its floor, three walls of its own and towers at its outer corners."""
        room = Room(self, side)
        out, along = ROOM_OUT * SEGMENT, ROOM_ALONG * SEGMENT
        for i in range(ROOM_OUT):
            for j in range(ROOM_ALONG):
                room.put(DUNGEON, floor, (i + 0.5) * SEGMENT, (j - (ROOM_ALONG - 1) / 2) * SEGMENT,
                         yaw=self.rng.randrange(4) * math.pi / 2)
        for j in range(ROOM_ALONG):
            room.put(DUNGEON, self.pick(), out, (j - (ROOM_ALONG - 1) / 2) * SEGMENT, solid=True)
        for i in range(ROOM_OUT):
            for end in (-1, 1):
                room.put(DUNGEON, self.pick(), (i + 0.5) * SEGMENT, end * along / 2, yaw=math.pi / 2, solid=True)
        for end in (-1, 1):
            room.put(DUNGEON, "pillar", out, end * along / 2, scale=ROOM_TOWER_SCALE, solid=True)

        inner, outer = side * (self.half_x + WALL_THICK / 2), side * (self.half_x + out - WALL_THICK / 2)
        self.inside("room", min(inner, outer), -along / 2 + WALL_THICK / 2, max(inner, outer), along / 2 - WALL_THICK / 2)
        return room

    def hang(self, name, x, d, yaw, height=0.0, out=0.0):
        """Hangs a piece on a wall whose middle line passes through (x, d), facing `yaw`, `out` metres off that line."""
        self.put(DUNGEON, name, x + out * math.sin(yaw), d + out * math.cos(yaw), yaw=yaw, y=height, centred=False)

    def on_wall(self, name, side, along, height=0.0, inside=True, out=0.0):
        """Hangs a piece on one of the courtyard's four walls.

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
        self.hang(name, x, d, yaw, height, out)

    def scatter(self, res_format, names, spots, scale=(1.0, 1.0)):
        """Small pieces lying about, turned any way: walked over, never solid."""
        for x, d in spots:
            self.put(res_format, self.rng.choice(names), x + self.rng.uniform(-0.3, 0.3), d + self.rng.uniform(-0.3, 0.3),
                     yaw=self.rng.uniform(0, 2 * math.pi), scale=self.rng.uniform(*scale))

    def layout(self):
        return {"version": lc.LAYOUT_VERSION, "assets": self.assets, "textures": self.textures,
                "placements": self.placements, "markers": self.markers, "areas": self.areas, "meshes": [],
                "lights": self.lights}


class Room:
    """A room off a courtyard's side wall, furnished in its own coordinates.

    (u, v): u metres out from the side wall's middle line, v from the room's middle toward the gate. A yaw of 0 faces
    a piece's +Z at the courtyard and a quarter turn faces it at the gate, in the room on either side.
    """

    def __init__(self, fort, side):
        self.fort, self.side = fort, side

    def at(self, u, v):
        return (self.side * (self.fort.half_x + u), v)

    def yaw(self, yaw):
        return self.side * (yaw - math.pi / 2)

    def put(self, res_format, name, u, v, yaw=0.0, **kw):
        self.fort.put(res_format, name, *self.at(u, v), yaw=self.yaw(yaw), **kw)

    def marker(self, name, u, v, yaw=None):
        self.fort.marker(name, *self.at(u, v), yaw=None if yaw is None else self.yaw(yaw))

    def hang(self, name, wall, along, height=0.0, out=0.0):
        """Hangs a piece on one of the room's own walls.

        wall: 'outer' (opposite the doorway), 'gate' or 'back' (the cross walls nearer to and further from the
        gate); along: v on the outer wall, u on a cross wall.
        """
        reach, half = ROOM_OUT * SEGMENT, ROOM_ALONG * SEGMENT / 2
        if wall == "outer":
            (x, d), yaw = self.at(reach, along), self.yaw(0.0)
        elif wall == "gate":
            (x, d), yaw = self.at(along, half), self.yaw(-math.pi / 2)
        else:
            (x, d), yaw = self.at(along, -half), self.yaw(math.pi / 2)
        self.fort.hang(name, x, d, yaw, height, out)

    def scatter(self, res_format, names, spots, scale=(1.0, 1.0)):
        self.fort.scatter(res_format, names, [self.at(u, v) for u, v in spots], scale)


def area_rect(area):
    (x0, z0), (x1, z1) = area["min"], area["max"]
    return ((x0 + x1) / 2, (z0 + z1) / 2, (x1 - x0) / 2, (z1 - z0) / 2, 0.0)


def allied_town(seed):
    f = Fort(TOWN_Z, front=-1, texture=STONE, seed=seed, wall="wall", worn=("wall_window_open", "wall_archedwindow_open"),
             safe=True)
    f.floor("floor_tile_large")
    f.walls()

    # A statue at the back, facing the gate.
    f.put(PROPS, "paladin_statue", 0.0, -5.6, solid=True)

    # A camp fire with benches on the left, a long table on the right.
    f.put(PROPS, "Campfire_Base", -5.4, 0.6)
    f.put(PROPS, "Campfire_Logs", -5.4, 0.6)
    f.put(DUNGEON, "bench", -5.4, 2.4, solid=True)
    f.put(DUNGEON, "bench", -3.5, 0.6, yaw=math.pi / 2, solid=True)
    f.put(DUNGEON, "table_long", 6.0, 0.2, solid=True)
    f.put(DUNGEON, "bench", 4.5, 0.2, yaw=math.pi / 2, solid=True)
    f.put(DUNGEON, "bench", 7.5, 0.2, yaw=math.pi / 2, solid=True)

    # Work corners: a forge on the left wall, a training dummy on the right.
    f.put(PROPS, "anvil", -8.0, -1.6, yaw=math.radians(90), solid=True)
    f.put(PROPS, "Trainingdummy", 7.9, -2.4, yaw=math.radians(-60), solid=True)

    # The blacksmith works at the anvil, facing the courtyard.
    f.marker("seller-blacksmith", -6.4, -1.8, yaw=math.pi / 2)

    # The merchant keeps a stall at the back on the right: a trunk of what it has bought, its takings beside it.
    f.put(DUNGEON, "trunk_medium_A", 8.7, -4.0, yaw=-math.pi / 2, solid=True)
    f.put(PROPS, "Money_Coins_Stack_Large", 7.7, -4.6)
    f.put(PROPS, "Money_Coins_Stack_Medium", 7.6, -3.4)
    f.put(PROPS, "Money_Coins_Stack_Small", 7.2, -4.9)
    f.marker("seller-merchant", 6.8, -4.0, yaw=-math.pi / 2)

    # The enchanter keeps to the front of the courtyard on the left, by a shrine of candles.
    f.put(PROPS, "shrine_candles", -8.4, 3.9, yaw=math.pi / 2, solid=True)
    f.put(PROPS, "rug_rectangle_stripes_A", -6.4, 3.9)
    f.marker("seller-enchanter", -6.6, 3.9, yaw=math.pi / 2)

    # Stores stacked in the corners, clear of the gate and the doorways.
    f.put(DUNGEON, "barrel_large", -7.9, 6.0, solid=True)
    f.put(DUNGEON, "crates_stacked", -5.6, 6.1, yaw=math.radians(8), solid=True)
    f.put(DUNGEON, "keg", 7.8, 5.9, yaw=math.pi / 2, solid=True)
    f.put(DUNGEON, "box_large", 5.6, 6.3, yaw=math.radians(-12), solid=True)
    f.put(DUNGEON, "barrel_small_stack", 8.4, -5.6, yaw=math.pi / 2, solid=True)
    f.put(DUNGEON, "trunk_large_A", -8.6, -5.4, yaw=math.pi / 2, solid=True)

    # Lanterns inside the gate; colours on every wall, and outside either side of the gate.
    for sx in (-1, 1):
        side = "left" if sx < 0 else "right"
        f.put(PROPS, "post_lantern", sx * 3.9, 6.2, yaw=math.pi, solid=True)
        f.on_wall("banner_shield_blue", "front", sx * 6.0, inside=False)
        f.on_wall("banner_blue", "front", sx * 6.0)
        f.on_wall("torch_mounted", "front", sx * 4.0, height=2.3, inside=False, out=ON_FACE)
        f.on_wall("banner_patternA_blue", side, -4.0)
        f.on_wall("torch_mounted", side, 0.0, height=2.3, out=ON_FACE)
        f.on_wall("banner_blue", side, 4.0)
        f.on_wall("banner_patternA_blue", "back", sx * 6.0)
        f.on_wall("torch_mounted", "back", sx * 3.0, height=2.3, out=ON_FACE)
    f.on_wall("banner_shield_blue", "back", 0.0)

    # The armoury: stores along the far wall, a grindstone, arms on the walls.
    armoury = f.room(-1, "floor_wood_large")
    armoury.put(DUNGEON, "chest_large", 6.5, 0.0, solid=True)
    armoury.put(DUNGEON, "crate_large", 6.3, -2.4, solid=True)
    armoury.put(DUNGEON, "barrel_small", 6.8, 2.0, solid=True)
    armoury.put(DUNGEON, "barrel_small", 5.6, 2.9, solid=True)
    armoury.put(PROPS, "grindstone", 4.2, -2.2, solid=True)
    armoury.put(PROPS, "cabinet_medium", 1.8, -3.0, yaw=math.pi / 2, solid=True)
    armoury.hang("shelves", "outer", -2.2, out=SHELVES_OUT)
    armoury.hang("shelves", "outer", 2.2, out=SHELVES_OUT)
    armoury.hang("torch_mounted", "outer", 0.0, height=2.6, out=ON_FACE)
    armoury.hang("sword_shield", "gate", 4.5, height=2.3, out=ON_FACE)
    armoury.hang("sword_shield", "back", 5.0, height=2.3, out=ON_FACE)
    armoury.marker("room", 2.6, 1.0)

    # The weaponsmith stands before the stores, facing whoever comes in.
    armoury.marker("seller-weaponsmith", 4.6, 0.3, yaw=0.0)

    # The barracks: three beds with a trunk at their feet, a table by the door wall.
    barracks = f.room(1, "floor_wood_large")
    for v in (-2.4, 0.0, 2.4):
        barracks.put(DUNGEON, "bed_A_single", 5.5, v, solid=True)
    barracks.put(DUNGEON, "trunk_medium_A", 3.3, -2.4, yaw=math.pi, solid=True)
    barracks.put(DUNGEON, "trunk_medium_A", 3.3, 0.0, yaw=math.pi, solid=True)
    barracks.put(DUNGEON, "table_small", 1.4, -2.6, solid=True)
    barracks.put(DUNGEON, "chair", 2.3, -2.6, solid=True)
    barracks.put(DUNGEON, "stool", 1.4, -1.5, solid=True)
    barracks.put(PROPS, "rug_rectangle_A", 2.6, 2.0)
    barracks.hang("banner_blue", "back", 4.0)
    barracks.hang("torch_mounted", "gate", 3.0, height=2.3, out=ON_FACE)
    barracks.marker("room", 2.0, 1.0)

    # Players start, and get back up, in a ring in front of the statue.
    for i in range(8):
        a = 2 * math.pi * i / 8
        f.marker("player-spawn", 2.6 * math.sin(a), 0.4 + 2.6 * math.cos(a))
    return f.layout()


def enemy_fortress(seed):
    f = Fort(FORTRESS_Z, front=1, texture=NIGHT, seed=seed, wall="wall", worn=("wall_cracked", "wall_broken"))
    f.floor("floor_dirt_large")
    f.walls()

    # The crypt the dead come from fills the back; graves line the sides.
    f.put(PROPS, "crypt", 0.0, -3.2, solid=True)
    for sx in (-1, 1):
        f.put(PROPS, "grave_A" if sx < 0 else "grave_A_destroyed", sx * 6.4, -6.0, solid=True)
        f.put(PROPS, "gravestone", sx * 8.4, -3.6, yaw=sx * math.radians(12), solid=True)
        f.put(PROPS, "grave_A_destroyed" if sx < 0 else "grave_A", sx * 6.2, -2.2, yaw=sx * math.radians(-8), solid=True)
    f.put(PROPS, "tree_dead_large", -8.0, 5.9, solid=True)
    f.put(PROPS, "tree_dead_medium", 8.2, 5.8, yaw=math.radians(140), solid=True)

    # Skull posts inside the gate; bones underfoot.
    for sx in (-1, 1):
        side = "left" if sx < 0 else "right"
        f.put(PROPS, "post_skull", sx * 3.9, 6.2, yaw=math.pi, solid=True)
        f.on_wall("banner_shield_red", "front", sx * 6.0, inside=False)
        f.on_wall("banner_red", "front", sx * 6.0)
        f.on_wall("torch_mounted", "front", sx * 4.0, height=2.3, inside=False, out=ON_FACE)
        f.on_wall("banner_patternB_red", side, -4.0)
        f.on_wall("torch_mounted", side, 0.0, height=2.3, out=ON_FACE)
        f.on_wall("banner_red", side, 4.0)
    f.scatter(PROPS, ("bone_A", "bone_B", "bone_C"), [(-3.2, 3.6), (2.6, 2.2), (-1.0, 5.4), (4.8, 4.6), (-5.2, 1.8), (1.4, 6.6)], scale=(1.2, 1.8))
    f.scatter(PROPS, ("skull", "ribcage"), [(-4.4, 5.2), (3.8, 6.4), (5.6, 2.4)], scale=(0.8, 1.1))

    # The ossuary: coffins along the far wall, candles in skulls, bones on the floor.
    ossuary = f.room(-1, "floor_tile_large")
    ossuary.put(PROPS, "coffin", 5.5, -2.2, solid=True)
    ossuary.put(PROPS, "coffin_decorated", 5.5, 0.0, solid=True)
    ossuary.put(PROPS, "coffin", 5.5, 2.2, solid=True)
    ossuary.put(PROPS, "skull_candle", 3.4, -2.9, yaw=math.radians(160), solid=True)
    ossuary.put(PROPS, "skull_candle", 1.3, -2.9, yaw=math.radians(200), solid=True)
    ossuary.scatter(PROPS, ("bone_A", "bone_B", "bone_C", "ribcage"), [(2.4, -1.2), (3.6, 1.4), (1.6, 0.2), (4.0, -0.6)], scale=(1.1, 1.6))
    ossuary.hang("banner_patternB_red", "back", 4.6)
    ossuary.hang("torch_mounted", "gate", 3.0, height=2.3, out=ON_FACE)
    ossuary.marker("room", 2.6, 0.8)

    # The throne room: the throne faces the doorway across a rug, between two shrines.
    throne = f.room(1, "floor_tile_large")
    throne.put(PROPS, "Vampire_Throne", 6.7, 0.0, solid=True)
    for v in (-2.3, 2.3):
        throne.put(PROPS, "shrine_candles", 6.6, v, solid=True)
        throne.hang("banner_patternB_red", "outer", v)
    throne.put(PROPS, "rug_rectangle_stripes_A", 4.0, 0.0, yaw=math.pi / 2)
    throne.hang("torch_mounted", "back", 3.0, height=2.3, out=ON_FACE)
    throne.hang("torch_mounted", "gate", 5.0, height=2.3, out=ON_FACE)
    throne.marker("room", 2.6, 0.8)

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
              f"{len(layout['markers'])} markers, {len(layout['areas'])} areas, {len(layout['lights'])} lights -> {path}")


if __name__ == "__main__":
    main()
