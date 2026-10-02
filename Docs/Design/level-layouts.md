# Level layouts and the two fortresses

The arena's scenery is **data**: a layout file per place, written by a generator from measured piece sizes, checked by an audit, and loaded in the game. The technique is Everdawn's level-layout pipeline (its forest battle stage); what is ported here is the layout format, the mesh-measuring helpers, the generate-audit-load loop and the loader. Everdawn's Blender build and capture tools are not ported: places are looked at with `./dev.sh screenshot`.

## The map

The ground is 70 wide (east-west) and 80 long (north-south). North is -Z, up the screen in the default camera.

| Place | Where | Gate | Role |
|---|---|---|---|
| Allied town | South: courtyard x -10..10, z 14..30; rooms x -18..-10 and 10..18, z 18..26 | Middle of its north wall | Players start and get back up here; safe from skeletons |
| Enemy fortress | North: courtyard x -10..10, z -30..-14; rooms x -18..-10 and 10..18, z -26..-18 | Middle of its south wall | Every wave rises here and comes out across the field |

Both are a walled courtyard on the dungeon kit's 4 m grid, 5 segments across and 4 deep, with a tower at each corner and one each side of the gate, which leaves a 3 m opening. 28 of open field lies between the two gates. Off each side wall is a room 2 segments by 2, with three walls of its own, a tower at each outer corner and no roof; it is entered from the courtyard through a doorway that takes the place of one side-wall segment (the kit's `wall_doorway`: an opening 1.9 wide under an arch).

- **Allied town:** stone walls, tiled floor, a statue facing the gate, a camp fire with benches, a long table, a forge corner and a training dummy, stores stacked in the corners, lantern posts, blue banners inside and out. Its rooms have wooden floors: an armoury (chest, crate, barrels, a grindstone, a cabinet, shelves and arms on the walls) and a barracks (three beds with trunks at their feet, a table and chair, a rug).
- **Enemy fortress:** the same kit in its night texture, with cracked and broken wall segments, a dirt floor, a crypt filling the back, graves, dead trees, skull posts, bones underfoot, red banners. Its rooms have tiled floors: an ossuary (three coffins, candles in skulls, bones) and a throne room (a throne facing the doorway across a rug, a shrine either side).

## In play

- **Solid:** walls, towers and the larger props block players, skeletons and dashes (a box each on the World collision layer, from the piece's own bounds). A doorway blocks as its mesh does instead, so it is walked through under its arch.
- **Players start and get back up in the town**, on its `player-spawn` spots.
- **Waves rise in the enemy fortress**, one skeleton to a spawn spot in a random order (a wave bigger than the 21 spots shares them), and march on the players wherever they are: aggro reaches further than the arena is long.
- **The town is safe:** a skeleton never goes for a player inside the town's walls, its rooms included, nothing of theirs (swing or arrow) hurts a player standing there, and a barrier across the town gate stops skeletons walking in (a collision layer only they run into). With every player in the town the skeletons stand where they are. The gate's threshold is outside the safe ground.
- **Ways round the walls:** every machine bakes a navigation mesh from the solids as the arena loads (`ArenaMap`). The host's skeletons follow it when chasing; the self-test bots follow it too. A walker's next corner is worked out again every quarter second.
- **Walls in the camera's way fade:** a tall solid on the line between the camera and the local player goes mostly transparent and comes back when it is clear (`GeometryInstance3D.Transparency`). What hangs on it fades with it: a piece that is not solid, has its middle at least 1 m off the ground and sits within 0.5 m of a tall solid (the banners and wall torches; one on the join of two wall segments hangs on both). The game's log lists what each layout hangs (`[level] ... hung on walls: ...`), so a floor tile or a prop caught by the rule shows.

## Pieces

| Piece | Role |
|---|---|
| `./dev.sh level-fortresses [--seed N] [--out-dir D]` | [Tools/level_fortresses.py](../../Tools/level_fortresses.py): writes `GodotClient/config/levels/allied-town.layout.json` and `enemy-fortress.layout.json`. Fixed positions for what matters, a seeded choice for worn wall segments and scattered bones. A solid that would run into another, or a marker with no room, stops the run. |
| `./dev.sh level-audit [<layout.json> ...]` | [Tools/level_audit.py](../../Tools/level_audit.py): re-checks layout files without Godot and without trusting the generator; exit 1 on a missing asset or texture, solids run into each other (walls and pillars may: they are built to join), a marker inside a solid or closer than a body to one, a solid in a `gate` area or (other than the doorway itself) in a `door` area, or a piece off the ground. On the `./dev.sh health` dashboard. |
| [Tools/level_common.py](../../Tools/level_common.py) | Stdlib helpers both use: GLB mesh loading with node transforms, a piece's bounds, turned-rectangle overlap and containment. |
| [Core/Level/LevelLayout.cs](../../Core/Level/LevelLayout.cs) | The parser, engine-free, with its tests (which also read the two generated files). |
| [LevelLayoutNode.cs](../../GodotClient/scripts/Level/LevelLayoutNode.cs) | Builds a layout in the scene: placements, textures, solids, the list of tall solids to fade. |
| [ArenaMap.cs](../../GodotClient/scripts/Main/ArenaMap.cs) | Loads both layouts and holds what they mean in play: spawn spots, the safe area, the ward, the navigation mesh, wall fading. |

## Layout format (version 1)

Everdawn's format with five additions (`solid`, `shape`, `textures`, `markers`, `areas`). Godot space: Y up, metres, yaw about +Y.

```json
{
 "version": 1,
 "assets": {"wall": "res://assets/dungeon/wall.glb"},
 "textures": {"res://assets/dungeon/": "res://assets/textures/dungeon/dungeon_texture.png"},
 "placements": [{"asset": "wall", "position": [x, y, z], "rotation": [qx, qy, qz, qw], "scale": [s, s, s], "solid": true}],
 "markers": [{"name": "player-spawn", "position": [x, y, z]}, {"name": "seller-weaponsmith", "position": [x, y, z], "yaw": radians}],
 "areas": [{"name": "safe", "min": [x, z], "max": [x, z]}],
 "meshes": [],
 "lights": [{"position": [x, y, z], "color": [r, g, b], "energy": 1.6, "range": 5.0}]
}
```

- `solid` placements get a collision box and must be scaled evenly. With `"shape": "mesh"` a solid collides as its triangles do instead of as its box: the doorways. The tools still treat its whole box as taken, so nothing else is put in it.
- `textures` maps an asset path prefix to the texture every piece under it wears; the longest matching prefix wins. The dungeon kit's pieces carry no texture of their own, which is what lets the fortress wear the night texture while its banners keep their red.
- `markers` in use: `player-spawn`, `enemy-spawn`, `gate`, `outside-gate`, `room` (a spot to stand on in each room), `seller-<id>` (where that seller stands, with a `yaw` for the way they face; [trade.md](trade.md)).
- `areas` in use: `courtyard`, `room` (two per fortress) and `door` (each doorway with 1.5 of ground either side, kept clear of props), which together are the ground inside a fortress; `safe` (the same three in the town); `gate` (each opening; the town's also carries the ward).
- `lights` are point lights without shadows, colours scene-linear: `level_fortresses.py` writes one at every piece that burns (its `BURNING` table: wall torches, post lanterns, the campfire, candles), at the flame, since the models give no light of their own. A new piece with a flame goes in that table. `meshes` is Everdawn's, unused so far.

## Workflow

1. Edit `Tools/level_fortresses.py`. Courtyard positions are `(x, d)`: d metres from the courtyard's middle toward its gate, x to the left as one walks out of the gate (the town is the fortress turned half round, not mirrored). Room positions are `(u, v)`: u metres out from the side wall, v from the room's middle toward the gate, and a yaw of 0 faces the courtyard, in the room on either side.
2. `./dev.sh level-fortresses`, then `./dev.sh level-audit`.
3. `./dev.sh screenshot` (add `--camera 2` for the top-down view, `--start-at x,z --no-enemies` to stand somewhere, such as in a room) to look at it; `./dev.sh net-test` for the `[map-check]` gate.

## Verified by `./dev.sh net-test`

`[map-check]` on every machine: the local player starts inside the town, gets out of it and as far as the enemy fortress, and takes no hit while it stands in the town; no skeleton is ever inside the town; every skeleton rises inside the enemy fortress and skeletons get out of it; walls between the camera and the player fade (2-4 at once in a run), all 29 banners and torches are found hanging on walls, and on some machine of the session they fade with one; the ways lead from where players start into each of the four rooms; and after the first second nobody had to walk straight for want of a way round the walls. The audit was shown to catch faults by moving a barrel into the gate and a large prop onto the spawn spots in a copy.

The playtest starts its players inside a room of the town (`--start-at`, the spot read from the layout's `room` marker), so its `left_town` gate has every one of them walk out through a doorway. A barrel moved into a doorway's approach in a copy was shown to fail the audit.

## Open questions

- What else the town should do: the weaponsmith is in the armoury, the blacksmith at the courtyard's anvil and the merchant at a stall across the courtyard from it ([trade.md](trade.md)); and the enchanter on a rug by a shrine at the front of the courtyard.
- Camping the enemy gate: four players standing in the fortress kill each wave as it rises. Whether waves should rise somewhere players cannot stand, or the fortress should defend itself.
- A player in the town can strike skeletons outside the gate without being struck back.
- Whether skeletons should do something when every player is in the town, instead of standing still.
- Battlements and a real gate: the dungeon kit has neither, so towers are scaled pillars and the gate is an opening.
- What the rooms are for: they are furnished and walked into, and nothing happens in them yet. Waves still rise only in the fortress's courtyard.
