# Level layouts and the two fortresses

The arena's scenery is **data**: a layout file per place, written by a generator from measured piece sizes, checked by an audit, and loaded in the game. The technique is Everdawn's level-layout pipeline (its forest battle stage); what is ported here is the layout format, the mesh-measuring helpers, the generate-audit-load loop and the loader. Everdawn's Blender build and capture tools are not ported: places are looked at with `./dev.sh screenshot`.

## The map

The ground is 70 wide (east-west) and 80 long (north-south). North is -Z, up the screen in the default camera.

| Place | Where | Gate | Role |
|---|---|---|---|
| Allied town | South: courtyard x -10..10, z 14..30; rooms x -18..-10 and 10..18, z 18..26 | Middle of its north wall | Players start and get back up here; safe from skeletons |
| Enemy fortress | North: courtyard x -10..10, z -30..-14; rooms x -18..-10 and 10..18, z -26..-18 | Middle of its south wall | Every wave rises here and comes out across the field |

Both are a walled courtyard on the dungeon kit's 4 m grid, 5 segments across and 4 deep, with a tower at each corner and one each side of the gate, which leaves a 3 m opening. 28 of open field lies between the two gates. Off each side wall is a room 2 segments by 2, with three walls of its own, a tower at each outer corner and no roof; it is entered from the courtyard through a doorway that takes the place of one side-wall segment (the kit's `wall_doorway`: an opening 1.9 wide under an arch). Each of the town's rooms has a storey over it, reached by stairs inside the room; under the enemy's ossuary and the left of its courtyard lies a crypt, reached by stairs down from the ossuary ("Floors", below).

- **Allied town:** stone walls, tiled floor, a statue facing the gate, a camp fire with benches, a long table, a forge corner and a training dummy, stores stacked in the corners, lantern posts, blue banners inside and out. Its rooms have wooden floors: an armoury (a chest, a cabinet and arms on the walls below, barrels in the nook under the landing; crates, a grindstone, barrels and shelves upstairs) and a barracks (a table and chair, trunks and a rug below; two beds upstairs).
- **Enemy fortress:** the same kit in its night texture, with cracked and broken wall segments, a dirt floor, a crypt monument filling the back, graves, dead trees, skull posts, bones underfoot, red banners. Its rooms have tiled floors: an ossuary (candles in skulls, bones, the stairs down) and a throne room (a throne facing the doorway across a rug, a shrine either side). The crypt under the ossuary holds three coffins, two pillars, a large chest with candles either side, bones, torches and banners, and its guards.

## In play

- **Solid:** walls, towers and the larger props block players, skeletons and dashes (a box each on the World collision layer, from the piece's own bounds). A doorway blocks as its mesh does instead, so it is walked through under its arch.
- **Players start and get back up in the town**, on its `player-spawn` spots.
- **Waves rise in the enemy fortress**, one skeleton to a spawn spot in a random order (a wave bigger than the 21 spots shares them), and march on the players wherever they are: aggro reaches further than the arena is long.
- **The town is safe:** a skeleton never goes for a player inside the town's walls, its rooms included, nothing of theirs (swing or arrow) hurts a player standing there, and a barrier across the town gate stops skeletons walking in (a collision layer only they run into). With every player in the town the skeletons stand where they are. The gate's threshold is outside the safe ground.
- **Ways round the walls:** every machine bakes a navigation mesh from the solids as the arena loads (`ArenaMap`). The host's skeletons follow it when chasing; the self-test bots follow it too. A walker's next corner is worked out again every quarter second.
- **Floors above the player are hidden, and stairs lead between them:** "Floors", below.
- **Walls in the camera's way fade:** a tall solid on the line between the camera and the local player goes mostly transparent and comes back when it is clear (`GeometryInstance3D.Transparency`). What hangs on it fades with it: a piece that is not solid, has its middle at least 1 m off the ground and sits within 0.5 m of a tall solid (the banners and wall torches; one on the join of two wall segments hangs on both). The game's log lists what each layout hangs (`[level] ... hung on walls: ...`), so a floor tile or a prop caught by the rule shows.

## Floors

The map has three floors: the ground (level 0), the town's two storeys (level 1, 4 m up) and the crypt (level -1, 4 m down), all loaded at once. `Core/Level/Floors` has the rules: a storey is 4 m (`Floors.Storey`); a body's level is the floor it stands nearest (`LevelOf`: the boundary is 1.5 below a floor, so a body halfway up a flight still counts as below); a piece's or a light's level is the floor under it (`FloorUnder`); two bodies are on one floor when they are less than 2 m apart in height (`SameLevel`).

- **Stairs:** the kit's `stairs_wood`, a solid with `"shape": "ramp"`, collides as a wedge (`LevelLayoutNode.Wedge`): a slope across its whole width from the top of its -Z end down to the bottom of its +Z end, solid under it, so it is walked up and never under. Each flight has a landing a tile deep straight on from its top and room to step on before its bottom, so it is walked on and off along its length. A room is too short for the kit's 6 m flight with both, so the flights up to the storeys are drawn out to 4.5 m only (42 degrees); bodies take a floor up to 50 degrees (`Gravity.SteepestFloor`; at Godot's 45 a body now and then took the steps for a wall halfway up). Both flights are drawn in to 0.8 of the kit's width along the room's outer wall, leaving a gap beside their open side: without it a body on the upper steps found the floor beside them an edge too high to step onto, though the ways led onto it.
- **Gravity:** players and skeletons fall at 20 m/s² up to 30 m/s, and are kept to a floor up to 0.25 below their feet from one step to the next, so walking down a flight is walking (`Util/Gravity`).
- **Holes in the ground:** the ground is laid by `ArenaMap` (no longer part of the scene): the field's rectangle cut along the edges of every `hole` area, each piece outside the holes a slab of its own. The crypt's stairs come up through one.
- **What is above is hidden:** each `cover` area hides every floor from its own level up, over its ground, while the camera's subject is below that level and on that ground. A storey's cover is its room, so a player in the armoury sees no storey over it (and sunlight through the stairwell); the crypt's cover is the whole ground at level 0, so a player in the crypt sees only the crypt, and past the crypt's walls, where the ground would be, is dark rather than sky (`WorldLook.Underground`: the background goes from the sky to a near-black colour and metal mirrors that). Hidden pieces still cast their shadows (`ShadowsOnly`), so a floor overhead still shades what is under it; lights, players, skeletons and what lies or stands on a floor (gold, orbs, weapons on the ground, sellers: the `on_a_floor` group) are not drawn at all. Bars over heads and floating numbers are drawn over everything, so they show only for bodies on the subject's own floor (`ArenaMap.OnSubjectsFloor`): the bars ask it, and each body asks it before a number floats over it (`FloatingText` itself knows nothing of floors).
- **One floor at a time:** blows, bolts, blasts, arrows and a skeleton's swing land only on bodies on their own floor; loot and weapons are picked up, and sellers traded with, only from their own floor; a skeleton only goes for a player on its own floor (so the waves keep to the ground and the crypt's guards to the crypt), and its way to that player is sought on that floor.
- **Ways:** the navigation mesh is baked with layer partitioning: by watershed (Godot's default) the floor over a flight and the floor under it came out as one region, with polygons from one to the other through the air (`./dev.sh ways-dump` lists any). `ArenaMap.HasWay` holds only for a way that ends on the spot's own floor.
- **The crypt** under the enemy fortress (x -18..-2, z -30..-14, 4 m down) has six guards, who rise once as the session starts (`Core/Combat/Crypt`, raised by `EnemyDirector` on the `crypt-guard` spots) and are no part of any wave. When the last of them falls, a treasure of 40 gold and 2 orbs is left at `crypt-treasure` ([combat.md](combat.md)).

## Pieces

| Piece | Role |
|---|---|
| `./dev.sh level-fortresses [--seed N] [--out-dir D]` | [Tools/level_fortresses.py](../../Tools/level_fortresses.py): writes `GodotClient/config/levels/allied-town.layout.json` and `enemy-fortress.layout.json`. Fixed positions for what matters, a seeded choice for worn wall segments and scattered bones. A solid that would run into another, or a marker with no room, stops the run, naming the spot in the room's `(u, v)` when a room put it. |
| `./dev.sh level-audit [<layout.json> ...]` | [Tools/level_audit.py](../../Tools/level_audit.py): re-checks layout files without Godot and without trusting the generator; exit 1 on a missing asset or texture, solids run into each other (walls and pillars may: they are built to join), a marker inside a solid or closer than a body to one, a solid in a `gate` area or (other than the doorway itself) in a `door` area, a flight of stairs with no floor at its own height or no room for a body straight on past its top or bottom (a copy with the armoury's flight put back as first built, its top against the end wall, fails on both ends), or a piece off the ground. On the `./dev.sh health` dashboard. |
| `./dev.sh level-list <town\|fortress> [x0,z0,x1,z1] [--room -1\|1]` | [Tools/level_list.py](../../Tools/level_list.py): what a layout has in an area, each placement with its box and height, markers and areas; with `--room`, all in that room's `(u, v)` as the generator takes them, so a piece can be moved by the numbers shown. |
| `./dev.sh ways-dump [x0,z0,x1,z1]` | Headless: bakes the navigation mesh as the arena does and prints its polygons in the area, or with none only those that go through the air (an edge rising more than 1 m at over 60 degrees); exit 1 when any does. Shown to find the two that joined the barracks' storey to the ground when the stairs were wider and the mesh partitioned by watershed. |
| [Tools/level_common.py](../../Tools/level_common.py) | Stdlib helpers both use: GLB mesh loading with node transforms, a piece's bounds, turned-rectangle overlap and containment. |
| [Core/Level/LevelLayout.cs](../../Core/Level/LevelLayout.cs) | The parser, engine-free, with its tests (which also read the two generated files). |
| [LevelLayoutNode.cs](../../GodotClient/scripts/Level/LevelLayoutNode.cs) | Builds a layout in the scene: placements, textures, solids, the list of tall solids to fade. |
| [ArenaMap.cs](../../GodotClient/scripts/Main/ArenaMap.cs) | Loads both layouts and holds what they mean in play: spawn spots, the safe area, the ward, wall fading. The floors (the ground with its holes, what is hidden above the camera's subject, the dark underground) are [ArenaMap.Floors.cs](../../GodotClient/scripts/Main/ArenaMap.Floors.cs), the navigation mesh and the walkers' steps [ArenaMap.Ways.cs](../../GodotClient/scripts/Main/ArenaMap.Ways.cs). |

## Layout format (version 1)

Everdawn's format with five additions (`solid`, `shape`, `textures`, `markers`, `areas`). Godot space: Y up, metres, yaw about +Y. Positions carry their height: a piece upstairs stands at 4, one in the crypt at -4.

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

- `solid` placements get a collision box, at their scale along each axis (the stairs are drawn out and drawn in). With `"shape": "mesh"` a solid collides as its triangles do instead of as its box: the doorways. With `"shape": "ramp"` it collides as a wedge sloping down toward its own +Z: the stairs. The tools still treat its whole box as taken, so nothing else is put in it.
- `textures` maps an asset path prefix to the texture every piece under it wears; the longest matching prefix wins. The dungeon kit's pieces carry no texture of their own, which is what lets the fortress wear the night texture while its banners keep their red.
- `markers` in use: `player-spawn`, `enemy-spawn`, `gate`, `outside-gate`, `room` (a spot to stand on in each room), `seller-<id>` (where that seller stands, with a `yaw` for the way they face; [trade.md](trade.md)), `upstairs` (a spot on each of the town's storeys), `crypt` (a spot in the crypt), `crypt-guard` (where each guard rises) and `crypt-treasure` (where the treasure is left).
- `areas` carry a `level` (0 when left out). In use: `courtyard`, `room` (two per fortress) and `door` (each doorway with 1.5 of ground either side, kept clear of props), which together are the ground inside a fortress; `safe` (the same three in the town); `gate` (each opening; the town's also carries the ward); `cover` (hides its level and those above over its ground from a player below it: each storey's room, and the whole ground for the crypt); `hole` (the ground cut away: over the crypt's stairs); `crypt` (the crypt's floor, level -1).
- `lights` are point lights without shadows, colours scene-linear: `level_fortresses.py` writes one at every piece that burns (its `BURNING` table: wall torches, post lanterns, the campfire, candles), at the flame, since the models give no light of their own. A new piece with a flame goes in that table. `meshes` is Everdawn's, unused so far.

## Workflow

1. Edit `Tools/level_fortresses.py`. Courtyard positions are `(x, d)`: d metres from the courtyard's middle toward its gate, x to the left as one walks out of the gate (the town is the fortress turned half round, not mirrored). Room positions are `(u, v)`: u metres out from the side wall, v from the room's middle toward the gate, and a yaw of 0 faces the courtyard, in the room on either side.
2. `./dev.sh level-fortresses`, then `./dev.sh level-audit`.
3. `./dev.sh screenshot` (add `--camera 2` for the top-down view, `--start-at x,z --no-enemies` to stand somewhere, such as in a room, `--start-at x,y,z` on another floor, or `--start-at crypt` / `--start-at upstairs:1` on a marker) to look at it; `./dev.sh net-test` for the `[map-check]` gate and `./dev.sh floors-test` for the floors.

## Verified by `./dev.sh net-test`

`[map-check]` on every machine: the local player starts inside the town, gets out of it and as far as the enemy fortress, and takes no hit while it stands in the town; no skeleton is ever inside the town; every skeleton of a wave rises inside the enemy fortress and skeletons get out of it (the crypt's guards are floors-test's); walls between the camera and the player fade (2-4 at once in a run), all 29 banners and torches are found hanging on walls, and on some machine of the session they fade with one; the ways lead from where players start into each of the four rooms; and after the first second nobody had to walk straight for want of a way round the walls. The audit was shown to catch faults by moving a barrel into the gate and a large prop onto the spawn spots in a copy.

## Verified by `./dev.sh floors-test`

Headless, skeletons on (`FloorsSelfTest`). No polygon of the navigation mesh goes through the air (`ways-dump`'s check). The player walks the ways as a bot would: from the town's spawn up to each storey's `upstairs` spot and back down, then out to the enemy fortress, down to the `crypt` spot, to the treasure and back up to the fortress gate. Each walk must find its way on the navigation mesh, end on its floor and get there in time (5-6 s up each flight in a run, 11 s from the town into the crypt). Each storey is hidden while the player is in the room under it and nothing is hidden upstairs or back in the town; from the crypt the ground is hidden (310 pieces in a run) and the crypt's guards are shown. The six guards stand on their spots on the crypt's floor until the player comes down; all but one warrior are cut down before the player goes down, that one must leave its spot for the player on the way, and when it falls the treasure lies at its spot (40 gold, 2 orbs) and the player who walks to it takes it all. The waves are cut down as they rise. A failed walk prints where the player stands, the ways under it, the way it was given, and what it touches.

Found by it before it passed: both flights first ended against a room's end wall, so a body got off them sideways onto a floor a step too high (the navigation mesh, with its 0.25 climb, said it could); the ways' check compared ground positions only, so a spot under the storey passed for the storey; watershed partitioning joined floors through the air; the 42-degree steps were a wall now and then at Godot's 45; and a skeleton's way to a player was sought at height 0, under the crypt's floor.

The playtest starts its players inside a room of the town (`--start-at`, the spot read from the layout's `room` marker), so its `left_town` gate has every one of them walk out through a doorway. A barrel moved into a doorway's approach in a copy was shown to fail the audit.

## Open questions

- What else the town should do: the weaponsmith is in the armoury, the blacksmith at the courtyard's anvil and the merchant at a stall across the courtyard from it ([trade.md](trade.md)); and the enchanter on a rug by a shrine at the front of the courtyard.
- Camping the enemy gate: four players standing in the fortress kill each wave as it rises. Whether waves should rise somewhere players cannot stand, or the fortress should defend itself.
- A player in the town can strike skeletons outside the gate without being struck back.
- Whether skeletons should do something when every player is in the town, instead of standing still.
- Battlements and a real gate: the dungeon kit has neither, so towers are scaled pillars and the gate is an opening.
- What the rooms are for: they are furnished and walked into, and nothing happens in them yet, upstairs included. Waves still rise only in the fortress's courtyard.
- Railings: the kit has none for its wooden stairs, so the open edge of a stairwell can be walked off (a fall onto the floor below).
- Whether more of the map should have floors: the enemy's throne room could have a storey, the town a cellar.
