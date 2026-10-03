"""Shared helpers for the level-layout tools, stdlib-only. Adapted from Everdawn's Tools/level_common.py.

Used by level_fortresses.py and level_audit.py; format and workflow in Docs/Design/level-layouts.md.
Coordinates are Godot space (Y up, metres); yaw turns about +Y as Godot's does.
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from _glb_stdlib import accessor_floats, accessor_ints, parse_glb_header  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GODOT_ROOT = os.path.join(REPO, "GodotClient")
LEVELS_DIR = os.path.join(GODOT_ROOT, "config", "levels")
# Core/Level/LevelLayout.cs SupportedVersion
LAYOUT_VERSION = 1


def res_to_path(res):
    if not res.startswith("res://"):
        raise ValueError(f"asset path must be res://, got {res!r}")
    return os.path.join(GODOT_ROOT, res[len("res://"):])


# --- Mesh geometry (glTF / GLB positions + indices, with the default scene's node transforms) ---

def _load_gltf(path):
    """(gltf json, bytes holding the binary buffer, offset of the buffer within those bytes)."""
    with open(path, "rb") as f:
        data = f.read()
    if path.endswith(".glb"):
        gltf, bin_offset, _ = parse_glb_header(data)
        return gltf, data, bin_offset
    gltf = json.loads(data)
    with open(os.path.join(os.path.dirname(path), gltf["buffers"][0]["uri"]), "rb") as f:
        return gltf, f.read(), 0


def _node_matrix(node):
    """Row-major 3x4 affine matrix of a glTF node (matrix, or translation * rotation * scale)."""
    if "matrix" in node:
        m = node["matrix"]  # column-major 4x4
        return [[m[c * 4 + r] for c in range(4)] for r in range(3)]
    tx, ty, tz = node.get("translation", (0.0, 0.0, 0.0))
    qx, qy, qz, qw = node.get("rotation", (0.0, 0.0, 0.0, 1.0))
    sx, sy, sz = node.get("scale", (1.0, 1.0, 1.0))
    rot = [[1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - qz * qw), 2 * (qx * qz + qy * qw)],
           [2 * (qx * qy + qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - qx * qw)],
           [2 * (qx * qz - qy * qw), 2 * (qy * qz + qx * qw), 1 - 2 * (qx * qx + qy * qy)]]
    return [[rot[r][0] * sx, rot[r][1] * sy, rot[r][2] * sz, t] for r, t in zip(range(3), (tx, ty, tz))]


def _compose(a, b):
    return [[sum(a[r][k] * b[k][c] for k in range(3)) + (a[r][3] if c == 3 else 0.0) for c in range(4)]
            for r in range(3)]


_IDENTITY = [[1.0, 0.0, 0.0, 0.0], [0.0, 1.0, 0.0, 0.0], [0.0, 0.0, 1.0, 0.0]]
_mesh_cache = {}


def load_mesh(res):
    """(vertices [(x, y, z)], triangles [(i, j, k)]) of an asset's default scene, in asset space."""
    if res not in _mesh_cache:
        gltf, data, off = _load_gltf(res_to_path(res))
        if "scenes" not in gltf:
            raise ValueError(f"{res}: glTF has no scenes")
        verts, tris = [], []
        stack = [(n, _IDENTITY) for n in gltf["scenes"][gltf.get("scene", 0)]["nodes"]]
        while stack:
            idx, parent = stack.pop()
            node = gltf["nodes"][idx]
            m = _compose(parent, _node_matrix(node))
            if "mesh" in node:
                for prim in gltf["meshes"][node["mesh"]]["primitives"]:
                    base = len(verts)
                    for x, y, z in accessor_floats(data, gltf, off, prim["attributes"]["POSITION"], 3):
                        verts.append(tuple(m[r][0] * x + m[r][1] * y + m[r][2] * z + m[r][3] for r in range(3)))
                    if "indices" in prim:
                        ix = accessor_ints(data, gltf, off, prim["indices"])
                        tris += [(base + ix[k], base + ix[k + 1], base + ix[k + 2]) for k in range(0, len(ix), 3)]
            stack += [(c, m) for c in node.get("children", [])]
        _mesh_cache[res] = (verts, tris)
    return _mesh_cache[res]


def bounds(res):
    """((min x, y, z), (max x, y, z)) of an asset in its own space."""
    verts, _ = load_mesh(res)
    return (tuple(min(v[i] for v in verts) for i in range(3)), tuple(max(v[i] for v in verts) for i in range(3)))


def yaw_of(rotation):
    """Yaw in radians of a layout quaternion [x, y, z, w]; raises on any tilt."""
    if abs(rotation[0]) > 1e-4 or abs(rotation[2]) > 1e-4:
        raise ValueError(f"only yaw rotations are supported, got {rotation}")
    return 2 * math.atan2(rotation[1], rotation[3])


def yaw_quat(yaw):
    return [0.0, round(math.sin(yaw / 2), 6), 0.0, round(math.cos(yaw / 2), 6)]


def turned(x, z, yaw):
    """A point in a piece's own space, turned by yaw as Godot's Y rotation turns it."""
    c, s = math.cos(yaw), math.sin(yaw)
    return (x * c + z * s, -x * s + z * c)


def scales(scale):
    """A piece's scale along its own x, y and z, from one number or three."""
    return (scale,) * 3 if isinstance(scale, (int, float)) else tuple(scale)


def footprint_rect(res, position, yaw, scale):
    """The piece's box on the ground as (centre x, centre z, half extent along its own x, along its own z, yaw)."""
    lo, hi = bounds(res)
    sx, _, sz = scales(scale)
    cx, cz = (lo[0] + hi[0]) / 2 * sx, (lo[2] + hi[2]) / 2 * sz
    dx, dz = turned(cx, cz, yaw)
    return (position[0] + dx, position[2] + dz, (hi[0] - lo[0]) / 2 * sx, (hi[2] - lo[2]) / 2 * sz, yaw)


def height_span(res, y, scale):
    """(bottom, top) of a piece standing at height y."""
    lo, hi = bounds(res)
    sy = scales(scale)[1]
    return (y + lo[1] * sy, y + hi[1] * sy)


def spans_overlap(a, b, slack=0.0):
    return a[0] + slack < b[1] and b[0] + slack < a[1]


# Core/Level/Floors.cs: one storey, and how far below its own floor a level begins.
STOREY = 4.0
LEVEL_BELOW = STOREY * 0.375


def level_of(y):
    return math.floor((y + LEVEL_BELOW) / STOREY)


def rect_corners(rect):
    cx, cz, hx, hz, yaw = rect
    return [(cx + dx, cz + dz) for dx, dz in (turned(sx * hx, sz * hz, yaw) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1)))]


def rects_overlap(a, b, slack=0.0):
    """Separating-axis test for two turned rectangles; slack shrinks both, so touching edges do not count."""
    for rect in (a, b):
        yaw = rect[4]
        for axis in (turned(1.0, 0.0, yaw), turned(0.0, 1.0, yaw)):
            spans = []
            for other in (a, b):
                dots = [x * axis[0] + z * axis[1] for x, z in rect_corners(other)]
                spans.append((min(dots), max(dots)))
            if spans[0][1] - slack <= spans[1][0] or spans[1][1] - slack <= spans[0][0]:
                return False
    return True


def point_in_rect(x, z, rect, margin=0.0):
    cx, cz, hx, hz, yaw = rect
    lx, lz = turned(x - cx, z - cz, -yaw)
    return abs(lx) <= hx + margin and abs(lz) <= hz + margin
