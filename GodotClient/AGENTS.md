# GodotClient — Instructions

Godot 4.7.2 .NET client: real-time play, networking, presentation and the headless self-tests. See the root [AGENTS.md](../AGENTS.md) for project-wide rules; the headless rule there applies to every launch.

Key reminders:
- Build with `./dev.sh build` or `dotnet build GodotClient/WarriorsOfEverdawn.csproj`.
- After copying assets in, run `./dev.sh import`.
- After editing `.tscn` or `.cs` files with the editor open, Godot prompts "Reload from disk" — always Reload.

## Script layout

`GodotClient/scripts/`:

| Folder | Owns |
|---|---|
| `Main/` | Arena and session start, launch flags (`LaunchOptions`), camera and camera modes, HUD, overhead bars, weapons on the ground and their labels (`GroundWeapons`, `GroundWeaponLabels`), gold, souls and magic orbs (`Loot`, with `MagicOrb` for how an orb looks), sellers and trade (`Market`, `SellerNpc`, and `ShopPanel`, the one window every seller uses), the two fortresses and the ways round them (`ArenaMap`), session rules (PvP) |
| `Level/` | Builds a level layout in the scene (`LevelLayoutNode`): pieces, their textures, solids; format and tools in [Docs/Design/level-layouts.md](../Docs/Design/level-layouts.md) |
| `Theme/` | Everdawn-style colours, fonts, panels and bars (`UiTheme`) |
| `Player/` | Networked character (`PlayerCharacter`), host-owned vitals (`PlayerVitals`), human and bot controls (`PlayerControls`) |
| `Enemy/` | Skeletons (`EnemyCharacter`, host-run AI), wave director, arrows in flight (`Arrows`) |
| `Character/` | Rig clips (`RigAnimations`), animator layers, torso twist, rig pieces and head sizing (`CharacterRig`), clip-per-skill and enemy looks (`CombatVisuals`), weapon trails, hit flash, dash ghosts |
| `Dev/` | Headless self-tests: `NetSelfTest` (net-test, pvp-test, trade-test; one part per area, `NetSelfTest.<Area>.cs`), `CameraSelfTest` (camera-test); window captures (`ScreenshotCapture`) |
| `Util/` | Small shared helpers |

## Headless self-tests

Headless runs draw nothing, so the self-tests measure instead: each check prints one `[<name>-check] key=value ...` line, and `dev.sh` gates on it (the `gate` helper reads the fields as `v["key"]` in an awk condition).

- The game prints its own limits and expectations next to what it measured (`limit_deg_s`, `head_expected`, `spin_speed_limit`, ...), so a retune in `Core` never leaves a stale number in `dev.sh`.
- A missing line fails its gate. A new check is not done until its gate exists; see [Docs/ops/registering-tools.md](../Docs/ops/registering-tools.md).
- `NetSelfTest` is a partial class split by area: `NetSelfTest.cs` runs the frame loop, tracks players and prints `[net-check]`; `Combat`, `Body`, `Spin`, `Weapon`, `Hud`, `Dash`, `Lunge`, `Pickup`, `Loot`, `Map`, `Trade`, `Bot`, `Ranged`, `Guard`, `Session` (waves, removal of the dead and of damage numbers, node count per wave) and `Down` (going down and back up) each hold their checks' fields, measurements and printed lines. A new check goes into the part for its area (or a new part), wired through `Track`, the frame loop and `PrintSummary`.
- Bots (`--bot`) drive the characters: they seek the nearest hostile, spin while hostiles are in reach, swing (at the air too while closing in, so every skill's reach gets samples), find their way round the walls and into the enemy fortress, run at their target and circle it once close, dash in bursts with a lunge in every third, spin, swing and lunge at the air on a schedule when nothing is in reach, swap their sets, let go of their weapon and take it back, and defend in phases (parrying, then bracing), so every feature gets exercised without a person. Each behaviour takes session time from the others: when a gate comes up short of samples, look at how often the bots get to do that thing before touching the gate: `[bot-check]` prints where each bot spent the session and on what (which kind of skeleton it chased or fought, time in reach of one, time down or unarmed), and `[combat-check]` its hits by skill.
- `./dev.sh playtest` runs the net-test session for 2.5 minutes and adds the `Session` and `Down` gates, which need several waves and a player going down. `--down-at` makes sure one does; the leak tolerances next to the gates in `dev.sh` record what was measured.
- Logs land in `_staging/net-test/`, `_staging/pvp-test/`, `_staging/trade-test/`, `_staging/playtest/` and `_staging/camera-test.log`.
- `KNOWN_DISCONNECT_ERROR` in `dev.sh` whitelists one engine error on disconnect (godot#86814); every other `ERROR` in a log fails the run.

## Launch flags

Parsed by `scripts/Main/LaunchOptions.cs`, after the `--` separator. An unknown flag or bad value fails the launch with the reason.

| Flag | Effect |
|---|---|
| `--host`, `--join <address>`, `--port N` | Session mode; solo when neither is given |
| `--pvp` | Host only: players can hit each other |
| `--no-enemies` | Host only: no skeleton waves |
| `--bot` | A bot drives the local player; also attaches `NetSelfTest` |
| `--quit-after <seconds>` | Quits after that long, printing the self-test summary first when `--bot` is on |
| `--camera-check` | Runs `CameraSelfTest` |
| `--camera 1-4` | Starts in that camera mode (numbered as C cycles them) |
| `--ai-playtest` | Minimizes the window without taking focus, so an AI-launched window never covers the user's work |
| `--screenshot`, `--shot-at S`, `--shots N`, `--shot-interval S` | Captures the window (`Dev/ScreenshotCapture`) at S seconds, N frames apart by the interval, then quits; any `--shot-*` implies `--screenshot`. Defaults: 6 s, 1 frame, 0.5 s |
| `--no-ui` | Hides the HUD and overhead bars |
| `--weapon <id>`, `--back-weapon <id>` | This machine's player starts with those weapons in hand and on the back (`greatsword`, `quarterstaff`, `spear`, `scythe`, each also at a level, as `spear+4`; they must differ); the self-tests give each bot a different pair |
| `--swing-survey` | Runs `Dev/SwingSurvey` and quits (`./dev.sh swing-survey`) |
| `--armour-lineup`, `--outfits A,B,...` | Stands the player's figure in a row in the field between the fortresses, once per tier of armour or once per outfit named, with a camera of its own in front of them (`Dev/ArmourLineup`); `./dev.sh armour-lineup` captures it |
| `--zoom F` | The camera starts that much nearer or further (below 1 is nearer; the camera keeps it within its limits), for captures |
| `--down-at S` | Host only: S seconds in, takes one player (a client's when there is one) down through the normal damage path; the playtest uses it |
| `--wave-scale N` | Host only: each wave brings N times its skeletons; net-test and playtest pass 3, since waves are not scaled by player count yet |
| `--orb-chance P` | Host only: every monster leaves a magic orb P of the time (0 to 1) in place of its own few in a hundred; net-test and playtest pass 0.25 so every machine sees orbs fall and be picked up, and `./dev.sh screenshot --orb-chance 1` shows them |
| `--start-gold N`, `--start-orbs N`, `--start-armour TIER` | Host only: every player starts with that much gold, that many orbs and that tier of armour (0 to 5), for trying the sellers and the armour; the trade test passes 150 gold at the weaponsmith and 280, 3 and tier 2 at the blacksmith |
| `--trade-drill` | With `--bot`: the bot trades with the seller it starts beside, through the shop window (`NetSelfTest.Trade`); the trade test passes it, and `./dev.sh screenshot --trade-drill` shows the window |
| `--start-at x,z` | Host only: players start on that spot of the ground instead of in the town's courtyard. `./dev.sh screenshot --start-at` uses it to look at a place; the playtest starts its players inside a town room, so each has to walk out through a doorway |

The self-tests are these flags plus `--headless`; any new AI-facing mode gets `--headless` built in from day one, or `--ai-playtest` when it must draw.

## Screenshots

`./dev.sh screenshot` is the only AI-run launch with a window: headless runs draw nothing, so a capture needs the real renderer. It places the window off-screen (`--position -10000,-10000`), `--ai-playtest` minimizes it without focus, a bot plays solo, and the frames land in `_staging/screenshot.png` (or `_staging/screenshot_<n>.png` for a series), with timestamped copies in `_staging/screenshots/` for before/after comparisons. Each run clears the previous `_staging/screenshot*.png` first. `./dev.sh playtest --screenshots N` runs its host the same way and captures N frames spread through the session without ending it.

- Captures are 1152x648, the default window size. `python Tools/zoom_region.py <png> <x> <y> <w> <h>` crops and enlarges a small element; `--grid` overlays coordinates to find them.
- Bots move and fight on their own, so pick moments with `--at`, or take a series (`--frames 6 --interval 0.3`) to catch a swing, a spin or a dash.
- Read the image yourself before claiming how something looks; a green self-test says the measured numbers are right, not that it looks right.

## Weapons

- `Core` has the rules (`Weapons`, `Skills`); `Character/CombatVisuals` has the looks: each weapon's model and grip (`WeaponLook`), and the clip for each skill. A new weapon needs both, plus its assets imported.
- **On the back:** `CharacterRig.AttachToBack` hangs the other set on the `chest` bone. Each look places it: `BackGrip` (the point on the weapon) goes to `BackPosition`, turned by `BackRotation`, in the chest bone's space (+Y up the spine, -Z out of the back). Check a new pose from behind with `./dev.sh screenshot --no-enemies --frames 8 --interval 0.6`: the idle bot turns, so some frames show its back.
- **Bots swap sets** for 1.5 s every 20 s from 9 s in, but never so late that they end a timed session swapped, so every machine sees swaps while each session's checks still read the bot's starting weapons.
- **Grip:** the point on the model the hand holds, in the model's own space. KayKit weapons are mostly modelled around their grip; `staff_A` is modelled around its middle and the scythe high on its shaft, so both carry a grip offset.
- **Striking point and reach:** `CharacterRig.WeaponTip` is the vertex farthest from the grip (the trail runs to it); `CharacterRig.WeaponReach` is how far the weapon's farthest point is from the body centre right now, which is what a skill's `Range` must match (net-test's reach-check).
- **Size and facing:** `WeaponLook.Scale` draws a model bigger or smaller wherever it is (hand, back, ground, ghosts), and `HandTurn` turns it about its own length in the hand. For a weapon whose head sticks out to one side (the scythe), `swing-survey` prints `head_leads`: near 1 the head faces the way the weapon travels at the hit, near -1 it trails behind, and the weapon needs half a turn.
- **New weapon skill:** run `./dev.sh swing-survey` for its hit time and reach (standing and moving), set them in `Core`, then let net-test's reach-check settle the range in play.
- **Enemies** take their looks from `CombatVisuals.LookFor(EnemyDefinition)` (`EnemyLook`): model, weapon, and for a bow the left hand and a rotation (Everdawn's bow settings). An attack can be a clip and a follow-up (`CombatVisuals.FollowUpFor`: the archer's draw, then release); its hit time counts across both, and `./dev.sh swing-survey` measures the release.
- **Projectiles:** an enemy attack with a `Projectile` (Core) looses one through `Arrows.In(tree).Loose` on the host at its hit time instead of testing an arc. `Arrows` flies any such attack, looked up by attack id, so a new ranged enemy is data plus a look.
- Anything that holds a character's meshes (`HitFlash`, the ghosts) gathers them when it needs them or rebuilds on a weapon change: a swapped-out weapon is freed.

## Armour

- `Core` has the tiers (`Armours`); `Character/ArmourLook` has what each looks like: an outfit (the `<Outfit>_Body`, `_ArmLeft`, `_ArmRight`, `_LegLeft` and `_LegRight` models under `assets/character_parts/`) and any pieces worn over it (a breastplate). `Own` is the figure's own parts. One entry per tier, or `Wear` throws.
- **Wearing it:** each part's skinned mesh is moved under the figure's `Skeleton3D` and the figure's own part hidden. The head, helmet and cape are never touched. Works for any `Rig_Medium` figure; parts made for another rig (Everdawn's Black Knight is on the large one) will not fit.
- **Control bones:** Everdawn's part models were exported with their rig's IK and control bones, which the characters here do not have. No vertex is weighted to them, so `ArmourLook` points those binds at the root bone; without that Godot reports each as a missing bone.
- **A new look:** copy the outfit's five parts from Everdawn's `character_parts` (flat here, whichever folder Everdawn keeps them in), run `./dev.sh import`, try it with `./dev.sh armour-lineup <Outfit>` and read the image, then name it in `ArmourLook`. Delete parts no tier uses.
- **Checked by** the trade test: every player's figure wears the parts of the tier the host holds for it (`ArmourLook.IsDressedFor`), on every machine.

## Assets

- Copied from Everdawn as needed, never referenced across repos.
- Keep Everdawn's layout under `GodotClient/assets/` (`characters/`, `weapons/`, `animations/`, ...) and copy each file's `.import` next to it. The `.import` records the `res://` path and UID, so an unchanged path keeps it valid. Never copy `.godot/`.
- Binary files (glb, png, ...) go through Git LFS per `.gitattributes`.
- Knight, Barbarian and the Skeleton Warrior / Minion / Rogue / Mage all share KayKit `Rig_Medium`. Character GLBs carry no animations; clips come from `assets/animations/Rig_Medium_*.glb`.
- **Heads are sized by Everdawn's method, smaller** (`CharacterRig.ShrinkHead`, its `HeadScale` / `HeadgearScale`): head, face and eye meshes at 0.55, headgear at 0.605, scaled on the mesh transforms around the head bone's rest position. Everdawn uses 0.75; this camera looks down on helmets over a foreshortened body, where 0.75 read far bigger than Everdawn's battle view. Meshes are sorted by name (`_Head` / `_Eyes` / `_Jaw`; Helmet / Visor / Hat / Hood / Crown); a model with no head mesh fails loudly. `[head-check]` only reads the scale back off the meshes: whether a size looks right is a screenshot question. An earlier attempt scaled the head bone in a `SkeletonModifier3D`: its numbers read 0.75 but the heads did not look smaller in game, so it was replaced by Everdawn's proven method.
- The rig GLBs import as **animation libraries**, not scenes. When copying another `Rig_*.glb`, set `importer="animation_library"` and `type="AnimationLibrary"` in its `.import` and drop the `path=` / `dest_files=` lines; `./dev.sh import` regenerates them.
- Everdawn's asset folders are self-contained, but its modular character parts are assembled by code (`../Everdawn/GodotClient/scripts/Character/`). Bring that over only when per-part character selection starts.
- Numbers taken from the art (hit times, weapon reach, ground speed, chest bias) are measured from the GLB data, and each is noted where it is used; `./dev.sh swing-survey` measures weapon skills. Blender is available through the Blender MCP for measuring.

## KayKit and Godot quirks

Already solved in Everdawn: [../Everdawn/Docs/Design/godot-pitfalls.md](../../Everdawn/Docs/Design/godot-pitfalls.md). Relevant here: rig facing backwards (`ModelYawOffset`), static accessories needing `BoneAttachment3D`, `AnimationPlayer.Play()` before `AddAnimationLibrary` crashing. Everdawn's "never share an `AnimationLibrary`" rule is about libraries taken from an instanced GLB's player; imported library resources are shared by every character here (`net-test` runs three per process).

- Rig track paths look like `Rig_Medium/Skeleton3D:<bone>`.
- Yaw is counter-clockwise from above, and yaw 0 faces -Z (`Core/Ground.cs`).
- A clip that turns the root bone (Spin's loop) cannot be layered on the upper body; `CombatVisuals.IsFullBody` marks those.
