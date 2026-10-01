"""glTF 2.0 binary header parser, stdlib-only.

Shared by `Tools/inspect_glb.py`, `Tools/collapse_uvs_to_cell_center.py`,
`Tools/normalize_head_anchors.py`, `Tools/inspect_uv_islands.py`,
`Tools/uv_cell_overlay.py`, `Tools/level_common.py`. Use `_glb_helpers.py` for bpy-driven scripts;
this helper is the stdlib counterpart that does not depend on Blender.

Underscore prefix keeps the module out of `./dev.sh help` listings.

A glTF 2.0 GLB binary has the shape:
    12-byte header (magic "glTF", version, length)
    JSON chunk: 4-byte length, 4-byte type "JSON", JSON payload (padded to 4)
    optional BIN chunk: 4-byte length, 4-byte type "BIN\\x00", BIN payload

`parse_glb_header(data)` returns:
    gltf            -- parsed JSON payload (dict)
    bin_payload_off -- offset of the BIN payload within `data` (i.e. *after*
                       the 8-byte BIN chunk header). 0 if no BIN chunk.
    bin_data        -- bytes view of the BIN payload, or None if no BIN chunk.

The "offset after the 8-byte BIN chunk header" choice matches what every
in-place mutator in this repo needs: writing into the BIN payload via
`struct.pack_into(..., buf, bin_payload_off + view_off, ...)` is the natural
call shape. Callers that want the BIN chunk *header* offset can compute
`bin_payload_off - 8`.
"""

from __future__ import annotations

import json
import struct
from typing import Optional


def parse_glb_header(data: bytes) -> tuple[dict, int, Optional[bytes]]:
    if data[:4] != b"glTF":
        raise ValueError(f"not a GLB (magic={data[:4]!r})")
    version = struct.unpack("<I", data[4:8])[0]
    if version != 2:
        raise ValueError(f"unsupported glTF version {version}")

    json_len = struct.unpack("<I", data[12:16])[0]
    if data[16:20] != b"JSON":
        raise ValueError("first chunk is not JSON")
    gltf = json.loads(bytes(data[20:20 + json_len]))

    pad = (4 - (json_len % 4)) % 4
    bin_chunk_off = 12 + 8 + json_len + pad
    if bin_chunk_off + 8 > len(data):
        return gltf, 0, None

    bin_chunk_len = struct.unpack("<I", data[bin_chunk_off:bin_chunk_off + 4])[0]
    bin_chunk_type = bytes(data[bin_chunk_off + 4:bin_chunk_off + 8])
    if bin_chunk_type != b"BIN\x00":
        return gltf, 0, None

    bin_payload_off = bin_chunk_off + 8
    bin_data = bytes(data[bin_payload_off:bin_payload_off + bin_chunk_len])
    return gltf, bin_payload_off, bin_data


def accessor_floats(data, gltf, bin_offset, idx, comps):
    """Read `count` vec`comps` floats from accessor `idx`."""
    acc  = gltf["accessors"][idx]
    view = gltf["bufferViews"][acc["bufferView"]]
    o = bin_offset + view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    n = acc["count"]
    stride = view.get("byteStride", comps * 4)
    return [struct.unpack_from(f"<{comps}f", data, o + i * stride) for i in range(n)]


def accessor_ints(data, gltf, bin_offset, idx):
    """Read all indices from accessor `idx`."""
    acc  = gltf["accessors"][idx]
    view = gltf["bufferViews"][acc["bufferView"]]
    o = bin_offset + view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    n = acc["count"]
    fmt = {5121: "B", 5123: "H", 5125: "I"}[acc["componentType"]]
    return struct.unpack_from(f"<{n}{fmt}", data, o)
