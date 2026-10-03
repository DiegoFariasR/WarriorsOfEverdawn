"""Generate the two fortresses of the arena as level layouts (Docs/Design/level-layouts.md).

  python Tools/level_fortresses.py [--out-dir <dir>] [--seed N]

Writes allied-town.layout.json and enemy-fortress.layout.json (default: GodotClient/config/levels/). Each is a
walled courtyard on the dungeon kit's 4 m grid with corner towers, one gate facing the other across the field, and a
room off each side wall, entered through a doorway: the town to the south, where players start and are safe, with a
storey over each room reached by stairs inside it; the fortress to the north, where the waves rise, with a crypt
under it reached by stairs down from one of its rooms. Pieces are placed by their measured boxes; a solid that would
overlap another (on the same floor: a storey's floor over a room is no overlap) stops the run. Check the result with
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
# The town's lantern posts stand this far out from the middle line of its front wall, either side of the gate.
GATE_LANTERNS_OUT = 1.8
CORNER_TOWER_SCALE = 1.5
# Rooms reach this many segments out from the side wall and run this many along it, about the courtyard's middle.
ROOM_OUT, ROOM_ALONG = 2, 2
ROOM_TOWER_SCALE = 1.2
# The side-wall segment that is each room's doorway (its middle, toward the gate), and half the kit doorway's opening.
DOOR_D = 2.0
DOOR_HALF = 0.9
# A wall's end, one at either end of a doorway's segment: as long as half of what the opening leaves of it.
WALL_END = "wall_endcap"
# Ground either side of a doorway that stays clear, so the way through is not walled off by a prop.
DOOR_APPROACH = 1.5
# A character's body, for keeping spawn points and the gate clear.
BODY_RADIUS = 0.5
# Pieces that make up the walls, floors and stairs may run into each other; everything else solid must stand clear.
STRUCTURE = ("wall", "pillar", "floor", "stairs")
# One storey (Core/Level/Floors.cs), and where on a floor tile what stands on it stands: the tile's top.
STOREY = lc.STOREY
FLOOR_TOP = 0.05
# A storey's floor is small tiles, so the hole the stairs come up through can be cut to fit.
SMALL_TILE = 2.0
# A storey is topped with stone tiles on its walls, out of reach: the town's buildings are roofed seen from outside.
STOREY_TOP = "floor_tile_large"
# A flight of the kit's wooden stairs is this wide and this long, and rises one storey (to a floor tile's top),
# toward its own -Z: its origin is at the top of the flight.
STAIRS = "stairs_wood"
STAIRS_HALF_WIDE, STAIRS_LONG = 1.65, 6.0
# A room is too short for a whole flight with a landing above it and room to step on below, so the stairs up to a
# storey are drawn out this long only: steeper (42 degrees), under the slope a body walks up (Gravity.SteepestFloor).
STAIRS_SHORT = 4.5
# Both flights are drawn in to this share of the kit's width, along the room's outer wall: so a gap is left between
# their open side and the floor's edge beside them (the floor upstairs, or the ground over the crypt). Without one a
# body on the flight's upper steps finds that floor an edge too high to step onto, though the ways lead onto it.
STAIRS_NARROWED = 0.8
# A body's height: a marker needs this much room above it.
BODY_HEIGHT = 2.2
# The crypt under the fortress: (x, d) corners in the fortress's own coordinates, under its left room and the left of
# its courtyard.
CRYPT_X, CRYPT_D = (-18.0, -2.0), (-8.0, 8.0)
# The whole ground, for what is hidden of it while a player is under it.
GROUND_HALF = (35.0, 40.0)
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

    def put(self, res_format, name, x, d, yaw=0.0, scale=1.0, solid=False, y=0.0, centred=True, shape=None, view=None):
        """Places a piece with the middle of its box at (x, d), or its own origin there when not centred, standing at
        height y. Its scale is one number, or three along its own x, y and z.

        shape="mesh" makes a solid block as its mesh does, not as its box: a doorway is walked through. shape="ramp"
        makes it a slope from the top of its box at its -Z end down to the bottom at its +Z end, as the kit's stairs
        rise: stairs are walked up. view="overhead" draws it only for the cameras looking straight down, view="around"
        only for the others; it blocks the same whichever draws it.
        """
        res = res_format.format(name)
        yaw = self.facing(yaw)
        wx, wz = self.world(x, d)
        sx, sy, sz = lc.scales(scale)
        if centred:
            lo, hi = lc.bounds(res)
            ox, oz = lc.turned((lo[0] + hi[0]) / 2 * sx, (lo[2] + hi[2]) / 2 * sz, yaw)
            wx, wz = wx - ox, wz - oz
        position = [round(wx, 3), round(y, 3), round(wz, 3)]
        if solid:
            rect = lc.footprint_rect(res, position, yaw, scale)
            span = lc.height_span(res, y, scale)
            structure = name.startswith(STRUCTURE)
            for other_name, other_rect, other_span in self.solids:
                if structure and other_name.startswith(STRUCTURE):
                    continue
                if lc.spans_overlap(span, other_span, slack=0.02) and lc.rects_overlap(rect, other_rect, slack=0.02):
                    raise SystemExit(f"[level-fortresses] {name} at ({x}, {d}) runs into {other_name}")
            if shape is None:
                for area in self.areas:
                    if (area["name"] == "door" and area.get("level", 0) == lc.level_of(y)
                            and lc.rects_overlap(rect, area_rect(area), slack=0.02)):
                        raise SystemExit(f"[level-fortresses] {name} at ({x}, {d}) stands in a doorway")
            self.solids.append((name, rect, span))
        self.assets.setdefault(name, res)
        placement = {"asset": name, "position": position, "rotation": lc.yaw_quat(yaw), "scale": [round(s, 3) for s in (sx, sy, sz)]}
        if solid:
            placement["solid"] = True
        if shape is not None:
            placement["shape"] = shape
        if view is not None:
            placement["view"] = view
        self.placements.append(placement)
        if name in BURNING:
            at, colour, energy, reach = BURNING[name]
            lo, hi = lc.bounds(res)
            fx, fy, fz = (lo[i] + (hi[i] - lo[i]) * at[i] for i in range(3))
            ox, oz = lc.turned(fx * sx, fz * sz, yaw)
            self.lights.append({"position": [round(position[0] + ox, 3), round(position[1] + fy * sy, 3), round(position[2] + oz, 3)],
                                "color": colour, "energy": energy, "range": reach})

    def marker(self, name, x, d, yaw=None, y=0.0):
        """A named spot at height y; with a yaw, the way whoever stands on it faces (0 toward the gate)."""
        wx, wz = self.world(x, d)
        body = (y + 0.1, y + BODY_HEIGHT)
        if any(lc.spans_overlap(body, span) and lc.point_in_rect(wx, wz, rect, margin=BODY_RADIUS) for _, rect, span in self.solids):
            raise SystemExit(f"[level-fortresses] marker {name} at ({x}, {d}) is inside a solid")
        marker = {"name": name, "position": [round(wx, 3), round(y, 3), round(wz, 3)]}
        if yaw is not None:
            marker["yaw"] = round(self.facing(yaw), 4)
        self.markers.append(marker)

    def area(self, name, x0, d0, x1, d1, level=None):
        """A named rectangle of ground; with a level, of that floor (a "cover" over a storey, a "hole" in the ground)."""
        (ax, az), (bx, bz) = self.world(x0, d0), self.world(x1, d1)
        area = {"name": name, "min": [round(min(ax, bx), 3), round(min(az, bz), 3)],
                "max": [round(max(ax, bx), 3), round(max(az, bz), 3)]}
        if level is not None:
            area["level"] = level
        self.areas.append(area)

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
                    self.put(DUNGEON, "wall_doorway", side * self.half_x, d, yaw=math.pi / 2, solid=True, shape="mesh", view="around")
                    # Seen from straight above, the arch hides the way through under it: those cameras see two wall
                    # ends instead, with the opening between them. Each faces the opening with its rounded end.
                    end_long = lc.bounds(DUNGEON.format(WALL_END))[1][0]
                    for along, yaw in ((-1, -math.pi / 2), (1, math.pi / 2)):
                        self.put(DUNGEON, WALL_END, side * self.half_x, d + along * (SEGMENT - end_long) / 2, yaw=yaw, view="overhead")
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

    def room(self, side, floor, stairwell=False):
        """The room off one side wall (-1 or +1): its floor, three walls of its own and towers at its outer corners.
        With a stairwell, its floor is left out over the outer half, for stairs down."""
        room = Room(self, side)
        out, along = ROOM_OUT * SEGMENT, ROOM_ALONG * SEGMENT
        for i in range(ROOM_OUT):
            for j in range(ROOM_ALONG):
                if stairwell and i == ROOM_OUT - 1:
                    continue
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

    def storey(self, room, floor, outer_wall):
        """A storey over a room: a floor of small tiles with a hole over the stairs, which run up along the room's outer
        wall away from the gate, from room to step on before them up to a landing a tile deep; three walls and the
        courtyard side, towers at the corners, and a top of stone tiles on its walls. A level-1 "cover" over the room
        hides the storey from a player in the room under it, and a level-2 one the top from a player in the storey."""
        out, along = ROOM_OUT * SEGMENT, ROOM_ALONG * SEGMENT
        up = STOREY
        stairs_u = out - WALL_THICK / 2 - STAIRS_HALF_WIDE * STAIRS_NARROWED
        top = -along / 2 + SMALL_TILE
        room.put(DUNGEON, STAIRS, stairs_u, top, yaw=math.pi / 2, scale=(STAIRS_NARROWED, 1.0, STAIRS_SHORT / STAIRS_LONG),
                 solid=True, shape="ramp", centred=False)
        # The hole: the outer half of the floor, two tiles on from the landing. Past them the steps are low enough for
        # a body on them to walk under the floor.
        hole_u, hole_v = out / 2, (top, top + 2 * SMALL_TILE)
        tiles = int(out / SMALL_TILE), int(along / SMALL_TILE)
        for i in range(tiles[0]):
            for j in range(tiles[1]):
                u, v = (i + 0.5) * SMALL_TILE, (j + 0.5) * SMALL_TILE - along / 2
                if u > hole_u and hole_v[0] < v < hole_v[1]:
                    continue
                room.put(DUNGEON, floor, u, v, yaw=self.rng.randrange(4) * math.pi / 2, solid=True, y=up)
        for j in range(ROOM_ALONG):
            v = (j - (ROOM_ALONG - 1) / 2) * SEGMENT
            room.put(DUNGEON, outer_wall, out, v, solid=True, y=up)
            room.put(DUNGEON, "wall", 0.0, v, solid=True, y=up)
        for i in range(ROOM_OUT):
            for end in (-1, 1):
                room.put(DUNGEON, self.pick(), (i + 0.5) * SEGMENT, end * along / 2, yaw=math.pi / 2, solid=True, y=up)
        for u in (0.0, out):
            for end in (-1, 1):
                room.put(DUNGEON, "pillar", u, end * along / 2, scale=ROOM_TOWER_SCALE, solid=True, y=up)
        for i in range(ROOM_OUT):
            for j in range(ROOM_ALONG):
                room.put(DUNGEON, STOREY_TOP, (i + 0.5) * SEGMENT, (j - (ROOM_ALONG - 1) / 2) * SEGMENT,
                         yaw=self.rng.randrange(4) * math.pi / 2, y=2 * up)
        inner, outer = room.side * (self.half_x - WALL_THICK / 2), room.side * (self.half_x + out + WALL_THICK / 2)
        for level in (1, 2):
            self.area("cover", min(inner, outer), -along / 2 - WALL_THICK / 2, max(inner, outer), along / 2 + WALL_THICK / 2, level=level)

    def crypt(self):
        """The crypt under the fortress's left room and the left of its courtyard, one storey down: its floor, walls and
        towers, and the stairs down from the room above, through a hole in the ground. A level-0 "cover" over the whole
        ground hides everything above from a player down here."""
        down = -STOREY
        (x0, x1), (d0, d1) = CRYPT_X, CRYPT_D
        across, deep = int((x1 - x0) / SEGMENT), int((d1 - d0) / SEGMENT)
        for i in range(across):
            for j in range(deep):
                self.put(DUNGEON, "floor_tile_large", x0 + (i + 0.5) * SEGMENT, d0 + (j + 0.5) * SEGMENT,
                         yaw=self.rng.randrange(4) * math.pi / 2, solid=True, y=down)
        for i in range(across):
            for d in (d0, d1):
                self.put(DUNGEON, self.pick(), x0 + (i + 0.5) * SEGMENT, d, solid=True, y=down)
        for j in range(deep):
            for x in (x0, x1):
                self.put(DUNGEON, self.pick(), x, d0 + (j + 0.5) * SEGMENT, yaw=math.pi / 2, solid=True, y=down)
        # Towers of the kit's own height: any taller would stand out of the ground above.
        for x in (x0, x1):
            for d in (d0, d1):
                self.put(DUNGEON, "pillar", x, d, solid=True, y=down)
        self.area("crypt", x0 + WALL_THICK / 2, d0 + WALL_THICK / 2, x1 - WALL_THICK / 2, d1 - WALL_THICK / 2, level=-1)
        self.area("cover", -GROUND_HALF[0], -GROUND_HALF[1] - self.cz, GROUND_HALF[0], GROUND_HALF[1] - self.cz, level=0)

    def stairs_down(self, room, landing):
        """Stairs from a room's floor down into the crypt, along its outer wall: their top toward the gate, a landing
        of small tiles before it, and a hole in the ground over them."""
        out, along = ROOM_OUT * SEGMENT, ROOM_ALONG * SEGMENT
        stairs_u = out - WALL_THICK / 2 - STAIRS_HALF_WIDE * STAIRS_NARROWED
        top = along / 2 - SMALL_TILE
        room.put(DUNGEON, STAIRS, stairs_u, top, yaw=-math.pi / 2, scale=(STAIRS_NARROWED, 1.0, 1.0), solid=True,
                 shape="ramp", centred=False, y=-STOREY)
        for u in (out / 2 + SMALL_TILE / 2, out - SMALL_TILE / 2):
            room.put(DUNGEON, landing, u, top + SMALL_TILE / 2, yaw=self.rng.randrange(4) * math.pi / 2)
        (x0, d0), (x1, d1) = room.at(out / 2, -along / 2 + WALL_THICK / 2), room.at(out - WALL_THICK / 2, top)
        self.area("hole", min(x0, x1), min(d0, d1), max(x0, x1), max(d0, d1), level=0)

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
        try:
            self.fort.put(res_format, name, *self.at(u, v), yaw=self.yaw(yaw), **kw)
        except SystemExit as stop:
            # The fortress names the spot in its own (x, d); the room's call gave (u, v).
            raise SystemExit(f"{stop} (room {self.side:+d}: u={u}, v={v})") from None

    def marker(self, name, u, v, yaw=None, y=0.0):
        self.fort.marker(name, *self.at(u, v), yaw=None if yaw is None else self.yaw(yaw), y=y)

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

    # A camp fire with a bench on the left, a long table on the right. The fire's side toward the gate is left open:
    # the way from the gate to the blacksmith passes it.
    f.put(PROPS, "Campfire_Base", -5.4, 0.6)
    f.put(PROPS, "Campfire_Logs", -5.4, 0.6)
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
    f.put(PROPS, "Money_Coins_Pile_10", 7.8, -4.8, yaw=math.radians(140), scale=0.9)
    f.put(PROPS, "Money_Coins_Pile_6", 7.7, -3.7, yaw=math.radians(265), scale=0.9)
    f.put(PROPS, "Money_Coins_Pile_3", 7.1, -5.1, yaw=math.radians(25), scale=0.9)
    f.put(PROPS, "Money_Coins_Stack_Single", 7.1, -3.0, yaw=math.radians(70), scale=0.9)
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

    # Lanterns outside the gate, torches inside it; colours on every wall, and outside either side of the gate.
    for sx in (-1, 1):
        side = "left" if sx < 0 else "right"
        f.put(PROPS, "post_lantern", sx * 3.9, f.half_z + GATE_LANTERNS_OUT, solid=True)
        f.on_wall("banner_shield_blue", "front", sx * 6.0, inside=False)
        f.on_wall("banner_blue", "front", sx * 6.0)
        f.on_wall("torch_mounted", "front", sx * 4.0, height=2.3, out=ON_FACE)
        f.on_wall("banner_patternA_blue", side, -4.0)
        f.on_wall("torch_mounted", side, 0.0, height=2.3, out=ON_FACE)
        f.on_wall("banner_blue", side, 4.0)
        f.on_wall("banner_patternA_blue", "back", sx * 6.0)
        f.on_wall("torch_mounted", "back", sx * 3.0, height=2.3, out=ON_FACE)
    f.on_wall("banner_shield_blue", "back", 0.0)

    upstairs = STOREY + FLOOR_TOP

    # The armoury: its stairs up along the far wall, a chest and a cabinet by the back wall, barrels in the nook under
    # the landing, arms on the walls. The stores are upstairs, with the grindstone, along the courtyard side: the way
    # round the hole is left open.
    armoury = f.room(-1, "floor_wood_large")
    f.storey(armoury, "floor_wood_small", "wall_window_open")
    armoury.put(DUNGEON, "chest_large", 3.1, -2.6, yaw=math.pi / 2, solid=True)
    armoury.put(PROPS, "cabinet_medium", 1.0, -2.5, solid=True)
    armoury.put(DUNGEON, "barrel_small", 5.0, -3.0, solid=True)
    armoury.put(DUNGEON, "barrel_small", 6.2, -3.0, solid=True)
    armoury.hang("torch_mounted", "back", 2.0, height=2.3, out=ON_FACE)
    armoury.hang("sword_shield", "gate", 2.5, height=2.3, out=ON_FACE)
    armoury.marker("room", 2.6, 1.0)

    # The weaponsmith stands before the chest, facing whoever comes in.
    armoury.marker("seller-weaponsmith", 2.8, -0.4, yaw=0.0)

    armoury.put(DUNGEON, "crates_stacked", 1.7, 2.0, solid=True, y=upstairs)
    armoury.put(PROPS, "grindstone", 1.7, -1.0, solid=True, y=upstairs)
    armoury.put(DUNGEON, "barrel_small", 5.0, 3.0, solid=True, y=upstairs)
    armoury.put(DUNGEON, "barrel_small", 6.2, 3.0, solid=True, y=upstairs)
    armoury.hang("shelves", "outer", 3.0, height=upstairs, out=SHELVES_OUT)
    armoury.hang("torch_mounted", "back", 2.0, height=STOREY + 2.3, out=ON_FACE)
    armoury.marker("upstairs", 2.5, -2.75, y=upstairs)

    # The barracks: its stairs up along the far wall, a table and trunks below, the beds upstairs.
    barracks = f.room(1, "floor_wood_large")
    f.storey(barracks, "floor_wood_small", "wall_window_open")
    barracks.put(DUNGEON, "trunk_medium_A", 3.3, -2.4, yaw=math.pi, solid=True)
    barracks.put(DUNGEON, "trunk_medium_A", 3.3, 0.0, yaw=math.pi, solid=True)
    barracks.put(DUNGEON, "table_small", 1.4, -2.6, solid=True)
    barracks.put(DUNGEON, "chair", 2.3, -2.6, solid=True)
    barracks.put(DUNGEON, "stool", 1.4, -1.5, solid=True)
    barracks.put(PROPS, "rug_rectangle_A", 2.4, 2.0)
    barracks.hang("banner_blue", "back", 2.0)
    barracks.hang("torch_mounted", "gate", 3.0, height=2.3, out=ON_FACE)
    barracks.marker("room", 2.0, 1.0)

    for v in (-1.55, 1.55):
        barracks.put(DUNGEON, "bed_A_single", 1.55, v, yaw=math.pi / 2, solid=True, y=upstairs)
    barracks.hang("torch_mounted", "gate", 3.0, height=STOREY + 2.3, out=ON_FACE)
    barracks.marker("upstairs", 5.4, 2.7, y=upstairs)

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

    # The ossuary: stairs down to the crypt along the far wall, candles in skulls, bones on the floor.
    ossuary = f.room(-1, "floor_tile_large", stairwell=True)
    f.stairs_down(ossuary, "floor_tile_small")
    ossuary.put(PROPS, "skull_candle", 3.4, -2.9, yaw=math.radians(160), solid=True)
    ossuary.put(PROPS, "skull_candle", 1.3, -2.9, yaw=math.radians(200), solid=True)
    ossuary.scatter(PROPS, ("bone_A", "bone_B", "bone_C", "ribcage"), [(2.4, -1.2), (3.2, 1.4), (1.6, 0.2), (3.4, -0.6)], scale=(1.1, 1.6))
    ossuary.hang("banner_patternB_red", "back", 4.6)
    ossuary.hang("torch_mounted", "gate", 3.0, height=2.3, out=ON_FACE)
    ossuary.marker("room", 2.6, 0.8)

    # The crypt under the ossuary: coffins along the north wall, two pillars, the treasure at the east end with
    # candles, bones, torches, and the dead that guard it.
    f.crypt()
    down = -STOREY + FLOOR_TOP
    for x, coffin in ((-12.0, "coffin"), (-8.0, "coffin_decorated"), (-4.4, "coffin")):
        f.put(PROPS, coffin, x, -5.8, solid=True, y=down)
    for d in (-2.0, 4.0):
        f.put(DUNGEON, "pillar", -10.0, d, solid=True, y=-STOREY)
    f.put(DUNGEON, "chest_large", -3.6, 2.0, yaw=-math.pi / 2, solid=True, y=down)
    for d in (-0.2, 4.2):
        f.put(PROPS, "skull_candle", -3.3, d, yaw=-math.pi / 2, solid=True, y=down)
    f.scatter(PROPS, ("bone_A", "bone_B", "bone_C", "skull", "ribcage"),
              [(-12.5, 6.2), (-7.0, 6.4), (-6.0, -2.8), (-12.0, -2.6), (-4.8, 6.4)], scale=(1.1, 1.6))
    for x in (-14.0, -6.0):
        f.hang("torch_mounted", x, CRYPT_D[0], 0.0, height=-STOREY + 2.3, out=ON_FACE)
    for x in (-10.0, -6.0):
        f.hang("torch_mounted", x, CRYPT_D[1], math.pi, height=-STOREY + 2.3, out=ON_FACE)
    f.hang("torch_mounted", CRYPT_X[1], -4.0, -math.pi / 2, height=-STOREY + 2.3, out=ON_FACE)
    f.hang("banner_red", -10.0, CRYPT_D[0], 0.0, height=-STOREY)
    f.hang("banner_patternB_red", CRYPT_X[1], 5.6, -math.pi / 2, height=-STOREY)
    for x, d in ((-12.5, -0.6), (-12.5, 5.0), (-7.0, -3.0), (-7.0, 1.4), (-6.2, 5.6), (-5.4, -1.4)):
        f.marker("crypt-guard", x, d, y=down)
    f.marker("crypt-treasure", -5.4, 2.0, y=down)
    f.marker("crypt", -11.2, 1.8, y=down)

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
