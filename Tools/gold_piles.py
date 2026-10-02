"""Build the piles gold falls in from the one coin (Docs/Design/combat.md, "Gold, souls and orbs").

  blender --background --factory-startup --python Tools/gold_piles.py      (./dev.sh gold-piles)

A pile shows its gold coin for coin, from one to ten (Core LootRules.MostCoinsShown): one coin, two in a row, three
in a triangle; from the fourth on, each goes on top of those three in turn, so nine are three stacks of three; and
the tenth lies on the middle of them. Each coin above the ground lies a little off the one under it, so that from
straight above, where a neat stack is one disc whatever its height, the coins can still be counted. Each pile is the
one coin (Money_Coins_Stack_Single.glb) copied into place and joined into one mesh with its one material, written
beside it as Money_Coins_Pile_<coins>.glb. A pile of one is the coin itself.

Run inside Blender (its Python has bpy). Afterwards: ./dev.sh import.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROPS = os.path.join(ROOT, "GodotClient", "assets", "props")
SOURCE = os.path.join(PROPS, "Money_Coins_Stack_Single.glb")

# Between two coins lying side by side, so they read as two from above and not as one blob.
GAP = 0.015

# The most coins a pile shows: Core's LootRules.MostCoinsShown.
MOST = 10

# Each coin is turned about its own axis by a different amount, so the copies do not show as copies.
TURNS = (0.0, 137.0, 251.0, 41.0, 190.0, 97.0, 313.0, 222.0, 68.0, 164.0)

# How far the coins of each layer lie off the layer under them, in coin radii (x, z): a whole layer moves together,
# so its coins never run into each other.
LAYER_OFF = ((0.0, 0.0), (0.16, 0.10), (-0.12, 0.14), (0.02, -0.06))


def spots(coins, radius, height):
    """Where each coin of a pile lies: (x, z, y) in the game's space (Y up, -Z up the screen from above)."""
    apart = 2 * radius + GAP
    half = apart / 2
    if not 2 <= coins <= MOST:
        raise SystemExit(f"[gold-piles] no pile of {coins} coins")
    if coins == 2:
        return [(-half, 0.0, 0.0), (half, 0.0, 0.0)]

    tall = apart * math.sqrt(3) / 2
    triangle = [(-half, tall / 3), (half, tall / 3), (0.0, -2 * tall / 3)]

    def lying(x, z, layer):
        off_x, off_z = LAYER_OFF[layer]
        return (x + off_x * radius, z + off_z * radius, layer * height)

    stacked = min(coins, MOST - 1)
    placed = [lying(*triangle[coin % 3], coin // 3) for coin in range(stacked)]
    if coins == MOST:
        placed.append(lying(0.0, 0.0, stacked // 3))
    return placed


def import_stack():
    """The one coin as one mesh object at the origin, its transforms applied, lying flat on Blender's ground."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SOURCE)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if len(meshes) != 1:
        raise SystemExit(f"[gold-piles] {SOURCE} should hold one mesh, and holds {len(meshes)}")
    stack = meshes[0]
    world = stack.matrix_world.copy()
    stack.parent = None
    stack.matrix_world = world
    for other in [o for o in bpy.context.scene.objects if o is not stack]:
        bpy.data.objects.remove(other, do_unlink=True)
    stack.data.transform(stack.matrix_world)
    stack.matrix_world = Matrix.Identity(4)
    return stack


def build(stacks):
    stack = import_stack()
    corners = [Vector(c) for c in stack.bound_box]
    radius = max(max(abs(c.x), abs(c.y)) for c in corners)
    height = max(c.z for c in corners)
    pieces = []
    for index, (x, z, y) in enumerate(spots(stacks, radius, height)):
        piece = stack if index == 0 else stack.copy()
        if index > 0:
            piece.data = stack.data.copy()
            bpy.context.scene.collection.objects.link(piece)
        # The game's (x, y up, z) is Blender's (x, -z, y up).
        piece.matrix_world = Matrix.Translation((x, -z, y)) @ Matrix.Rotation(math.radians(TURNS[index]), 4, "Z")
        pieces.append(piece)

    bpy.ops.object.select_all(action="DESELECT")
    for piece in pieces:
        piece.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    pile = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    name = f"Money_Coins_Pile_{stacks}"
    pile.name = name
    pile.data.name = name
    if len(pile.material_slots) != 1:
        raise SystemExit(f"[gold-piles] {name} came out with {len(pile.material_slots)} materials, not the coin's one")

    path = os.path.join(PROPS, f"{name}.glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_yup=True, export_apply=True)
    corners = [Vector(c) for c in pile.bound_box]
    across = max(c.x for c in corners) - min(c.x for c in corners)
    deep = max(c.y for c in corners) - min(c.y for c in corners)
    print(f"[gold-piles] {name}: {stacks} coins of radius {radius:.3f}, {across:.2f} across, {deep:.2f} deep, "
          f"{max(c.z for c in corners):.2f} high, {len(pile.data.vertices)} vertices -> {path}")


def main():
    if not os.path.isfile(SOURCE):
        raise SystemExit(f"[gold-piles] the coin the piles are made of is missing: {SOURCE}")
    for coins in range(2, MOST + 1):
        build(coins)


if __name__ == "__main__":
    try:
        main()
    except SystemExit as stop:
        print(stop, file=sys.stderr)
        raise
