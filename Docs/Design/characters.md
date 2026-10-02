# Characters

How a figure is made: Everdawn's body part system, shared with it. Every figure in the game (players, skeletons, sellers) is the rig's skeleton with a part in each slot, so any of them can be put together from any character's parts, and figures can be drawn at random.

The parts, the catalogue and the code that reads it and puts a figure together are not this game's: they are **the kit** (`EverdawnKit`), a repository of its own that both games mount as a git submodule at `GodotClient/kit` (below, The kit).

## Parts and slots

- **A part** is one model made for the medium rig: a head, a body, an arm, a leg, something worn. All are skinned to the same skeleton, so any part fits any figure whichever character it was made for.
- **Eight slots** (the kit's `CharacterSlot`): `Head`, `Face`, `Body`, `ArmLeft`, `ArmRight`, `LegLeft`, `LegRight`, one part each, and `Accessory`, any number. (The enum also has Everdawn's numbered accessory slots; a look here keeps what is worn in a list.)
- **The catalogue** (`GodotClient/kit/config/parts_catalog.json`, read by the kit's `PartsCatalog`) lists every part by its stem (`Knight_Helmet`) with its slot, the character it was made for (`origin`), where its model is under `kit/character_parts/` (`path`), its rig, and the bone that carries it (`bone`). 449 parts of 53 characters, 83 of them for the large rig, which no figure here uses.
- **Heads come two ways.** A character's whole head (`Knight_Head`) has its face modelled in. Most also have a faceless one (`Knight_HeadShell`) for a face to be put on (`Knight_Face`). A skeleton has neither: its face is its eyes and its jaw, two models, named as one in a look (`SkeletonMinion_Face`; `PartsCatalog.Pieces`).
- **What the head carries is told by its bone, not by its name.** `bone` is the bone most of the part's weight is on, or the one it hangs from, read off the model when the catalogue is written. A hat, a helmet, a mask and a pair of goggles are on `head`; a cape, a quiver and a medal are not. Going by names had the blacksmith's goggles down as no headgear, and takes a hip pouch for a hat.

## A look

The kit's `CharacterLook` is what a figure is made of: a part's stem in each slot, the accessories, and optionally a palette.

- `CharacterLook.Of("Knight", "Cape", "Helmet")` is a character as it was made, with what it wears.
- `Wearing(body, arms, legs)` swaps those for another character's; this is all armour is (below).
- `Key` writes the whole look in one line, and two looks are equal when their keys are. It is what a machine would send another to say what a figure wears; nothing sends one yet, since every figure's look is still fixed by what it is.
- `CheckAgainst(catalogue)` throws for a part that is not in the catalogue or is in a slot it was not made for.

Who looks like what:

| Figure | Look | Where |
|---|---|---|
| Player | The Knight with its cape, bare-headed, in the armour its tier gives it | `PlayerCharacter.Look`, `Character/ArmourLook` |
| Skeleton minion, warrior, archer | KayKit's minion, warrior and rogue skeletons with what they wear | `CombatVisuals.LookFor` |
| Weaponsmith, blacksmith, merchant, enchanter | Barbarian, Engineer, hooded Rogue, Mage | `SellerNpc` |

## Putting a figure together

The kit's `CharacterBody` (`kit/godot/`). Its `Mount` is what both games put parts on a skeleton with; `Build` and `Dress` are this game's way in, a medium-rig figure in a `CharacterLook` with the toon look on it:

- `Build(look)` is the skeleton alone (`kit/rig/Rig_Medium.glb`) with the look's parts on it. `Dress(skeleton, look)` puts another look on a figure that is already standing, in place of what it wore; what it holds stays in its hands.
- A skinned part's mesh goes under the skeleton as it is. One that is not skinned (a skeleton's helmet and hood, a strapped-on pack) hangs on the bone it hangs from in its own model, or on the catalogue's bone when its model has no rig.
- A part's meshes are read off its model once and kept; after that a figure is made of what was read, with no model to load. A dash's ghosts are eight more figures, rebuilt whenever a weapon changes hands.
- The parts were exported with their rig's control bones (IK targets, foot rolls), which the skeleton here does not have and no vertex is weighted to. Their binds are pointed at the root bone; without that Godot reports each as a missing bone.
- Every part gets the toon look (`ToonLook`). The characters whose textures have something other than metal where the others have their metals (a beak, leaves, bandages) get it without the metal.

## Head size

**Everdawn's, by Everdawn's rule, from the same two constants** (the kit's `HeadSizing`): the head and the face are drawn at 0.75 of their modelled size, and what the head carries at 0.825, a little bigger so it still fits over the head. Both shrink towards the head bone's resting place, so the head stays on the neck. It is the one thing Everdawn changes about a KayKit figure's proportions.

- Which parts are which goes by the slot and the catalogue's bone, as above. Before the parts, whole models were sorted by mesh name, which left the blacksmith's goggles at full size.
- Both games size and place parts by the one rule (`CharacterBody.Mount`). It replaced Everdawn's name rule, under which a skeleton's eyes and jaw were sized as headgear (0.825) and what is worn on the body (a medal, a breastplate, a pouch, a tail) was shrunk towards the head.
- **Players are bare-headed for the head's sake.** With the same numbers, figures here read bigger-headed in play than Everdawn's; side by side from the front, bare-headed, they are the same (`./dev.sh look-lineup Barbarian:bare,Knight:bare,Knight`). Two things differ. Everdawn's heroes are mostly bare-headed, where the player here wore the Knight's great helm, which is drawn bigger than the head under it and is the widest thing on the figure; so the helm came off (the sellers keep their hats and hood, the skeleton warrior its helmet). And the angled and top-down cameras look down from 56 and 90 degrees, where Everdawn's battle camera stands beside the fighters about 21 up (the camera behind the player, at 22, is the same): seen from above the body is foreshortened and the head, on top, is not. The numbers were 0.55 and 0.605 for a while for that reason, and were put back to Everdawn's. Open: whether heads are sized by camera.
- `[head-check]` (net-test) and `parts-test` only read the scale back off the meshes: whether a size looks right is a screenshot question (`./dev.sh look-lineup`).

## Armour

A tier of armour is the figure's own look with the body, arms and legs of other characters ([trade.md](trade.md)): `ArmourLook.Dressed(own, tier)`. The head and what is on it are the figure's own, so a player is still told by its head and cape. The trade test checks that every player's figure wears the parts of the tier the host holds for it, on every machine (`ArmourLook.IsDressedFor`).

## Palettes

A look may name a palette: one of the textures of `kit/textures/characters/`, by its file name without the extension (`knight_texture_alt_B`). Every textured surface of every part is then coloured by it in place of its own texture.

- This works because KayKit's character textures all keep their colours in the same places: an 8 by 4 grid with the skin, the hair and the two metals in the first row.
- With no palette each part keeps the colours of the character it was made for, which is right for a character as it was made and patchwork for a mix: one figure's bare arms in another's skin.
- A skeleton's eyes glow by their own material and are never recoloured; neither is glass.
- Not brought over: Everdawn's palette mixer, which recolours single cells of the grid (this skin with that hair).

## Random figures

The kit's `LookRandomizer.Roll(catalogue, pool, seed)` puts a figure together the way Everdawn's character creator randomizes one.

- A head, a body, a pair of arms and a pair of legs, each from any character of the pool; arms and legs always as pairs.
- The face is the head's own: a faceless head with its character's face, a skull with its eyes and jaw. A character's whole head is never drawn when it has a faceless one, which is the same head.
- By chance (a half each, set per pool) one thing on the head and one thing carried.
- One palette of the pool's for the whole figure.
- **The same seed gives the same figure on every machine**, so a host need only send the seed.

| Pool (`Core/Characters/LookPools`) | Drawn from | Palettes |
|---|---|---|
| `townsfolk` | Knight, Barbarian, Mage, Rogue, hooded Rogue, Ranger, Druid, Engineer, Cleric, Lorekeeper, the farmers | The adventurers' textures and their three alternates each |
| `skeletons` | Skeleton minion, warrior, rogue, mage | The two skeleton textures |

A few parts are never drawn on their own (`PartsCatalog.IsHidden`, the list Everdawn's character creator hides too): they belong to another part or do not sit right on a figure they were not made for. Parts of the large rig are never drawn either.

**Nothing in the game is random yet.** Sellers and skeletons look as they did. The pools are there to be used: a skeleton's look from its wave and number, a townsperson's from where it stands.

## The kit

`EverdawnKit` is a repository of its own, mounted in both games as a git submodule at `GodotClient/kit`, so both read the same files at the same engine paths (`res://kit/...`). Its `README.md` says what is in it and how a game uses it.

- **From it:** the part models with their import files, the palette textures, the catalogue and the tool that writes it, the bare skeleton, the outline shader, the engine-free code (`kit/core`: catalogue, look, randomizer, head sizes) and the Godot-side code (`kit/godot`: `CharacterBody`, `ToonLook`).
- **This game's:** who looks like what, the pools, armour as a change of parts, and everything a figure holds and does.
- `Core` references `kit/core` as a project. The Godot project compiles `kit/godot` in as source: Godot does not find node scripts in another assembly. `kit/core` and `kit/tests` are left out of it (`WarriorsOfEverdawn.csproj`).
- A clone needs `git submodule update --init`; `./dev.sh build` says so when the kit is missing.
- A change to the kit is made in the kit, checked in both games, and reaches this one when the submodule is moved to the new commit. Never edit it for this game's sake alone.

Not used here, though the kit has them:

- **The large rig** (the Black Knight, the golems, the orc brute, 83 parts): there are no clips for it here.
- **Part aliases** (the Knight's visor closed, the Engineer's goggles down): the same model turned and moved, which `CharacterBody` does not do.

Everdawn's alone: the character creator screen, face expressions, portraits, palettes mixed cell by cell, and looks written in YAML per unit.

## Verified by

- The kit's tests (`kit/tests/`, run by `./dev.sh test` after Core's): the catalogue's reading and its refusals, looks, and the randomizer (same seed same figure, pairs, faces, nothing hidden or of the large rig).
- `Core.Tests/Characters/`: this game's pools against the kit's files.
- `./dev.sh parts-test`, headless: every part of the catalogue is put on a figure in its slot and must come out as a mesh whose skin resolves, at the size its slot and bone say; then 200 figures are drawn from each pool and dressed.
- `./dev.sh look-lineup [cast | <pool>[@seed] | <Character>[:bare],...]`: figures in a row from the front, saved to `_staging/look-lineup.png`. `cast` is the player, the enemies and the sellers; `:bare` shows a character with nothing on.
- `./dev.sh net-test` (`[head-check]`) and `./dev.sh trade-test` (armour on the figure) read live figures.

## Open questions

- **The toon look in Everdawn.** Everdawn mounts its parts with the kit's `CharacterBody.Mount`, but still has its own materials (palettes mixed cell by cell, per-origin textures, its own toon and metal code), not the kit's `ToonLook`.
- Which figures are drawn at random, and from what seed.
- Players choosing their own figure.
