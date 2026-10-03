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
| `Main/` | Arena and session start, launch flags (`LaunchOptions`), camera and camera modes, HUD, overhead bars, weapons on the ground and their labels (`GroundWeapons`, `GroundWeaponLabels`), gold, souls and magic orbs (`Loot`, with `MagicOrb` for how an orb looks), sellers and trade (`Market`, `SellerNpc`, and `ShopPanel`, the one window every seller uses), the two fortresses, their floors and the ways round them (`ArenaMap`, split into `ArenaMap.Floors` and `ArenaMap.Ways`), session rules (PvP) |
| `Level/` | Builds a level layout in the scene (`LevelLayoutNode`): pieces, their textures, solids; format and tools in [Docs/Design/level-layouts.md](../Docs/Design/level-layouts.md) |
| `Theme/` | Everdawn-style colours, fonts, panels and bars (`UiTheme`) |
| `Player/` | Networked character (`PlayerCharacter`), host-owned vitals (`PlayerVitals`), human and bot controls (`PlayerControls`) |
| `Enemy/` | Skeletons (`EnemyCharacter`, host-run AI), wave director, arrows in flight (`Arrows`) |
| `Character/` | What each tier of armour puts on a figure (`ArmourLook`), rig clips (`RigAnimations`), animator layers, torso twist, weapons in the hands and on the back (`CharacterRig`), clip-per-skill and enemy looks (`CombatVisuals`), weapon trails, hit flash, dash ghosts. The figure itself and its toon look are the kit's (`kit/godot/`: `CharacterBody`, `ToonLook`; [Docs/Design/characters.md](../Docs/Design/characters.md)) |
| `Dev/` | Headless self-tests: `NetSelfTest` (net-test, pvp-test, trade-test, magic-test; one part per area, `NetSelfTest.<Area>.cs`), `CameraSelfTest` (camera-test), `WallSelfTest` (wall-test), `PartsSelfTest` (parts-test), `FloorsSelfTest` (floors-test), `WaysDump` (ways-dump); window captures (`ScreenshotCapture`) |
| `Util/` | Small shared helpers |

## Headless self-tests

Headless runs draw nothing, so the self-tests measure instead: each check prints one `[<name>-check] key=value ...` line, and `dev.sh` gates on it (the `gate` helper reads the fields as `v["key"]` in an awk condition).

- The game prints its own limits and expectations next to what it measured (`limit_deg_s`, `head_expected`, `spin_speed_limit`, ...), so a retune in `Core` never leaves a stale number in `dev.sh`.
- A missing line fails its gate, and so does a line without a field the condition names; a field printed as `NaN` fails every comparison. A new check is not done until its gate exists; see [Docs/ops/registering-tools.md](../Docs/ops/registering-tools.md).
- `NetSelfTest` is a partial class split by area: `NetSelfTest.cs` runs the frame loop, tracks players and prints `[net-check]`; `Combat`, `Body`, `Spin`, `Weapon`, `Hud`, `Dash`, `Lunge`, `Pickup`, `Loot`, `Map`, `Trade`, `Bot`, `Ranged`, `Guard`, `Status` (statuses seen, held bodies, burns' and wounds' bites), `Session` (waves, removal of the dead and of damage numbers, node count per wave) and `Down` (going down and back up) each hold their checks' fields, measurements and printed lines. A new check goes into the part for its area (or a new part), wired through `Track`, the frame loop and `PrintSummary`.
- Bots (`--bot`) drive the characters: they seek the nearest hostile, spin while hostiles are in reach, swing (at the air too while closing in, so every skill's reach gets samples), find their way round the walls and into the enemy fortress, run at their target and circle it once close, dash in bursts with a lunge in every third, spin, swing and lunge at the air on a schedule when nothing is in reach, swap their sets, let go of their weapon and take it back, and defend in phases (parrying, then bracing), so every feature gets exercised without a person. Each behaviour takes session time from the others: when a gate comes up short of samples, look at how often the bots get to do that thing before touching the gate: `[bot-check]` prints where each bot spent the session and on what (which kind of skeleton it chased or fought, time in reach of one, time down or unarmed), and `[combat-check]` its hits by skill.
- `./dev.sh playtest` runs the net-test session for 2.5 minutes and adds the `Session` and `Down` gates, which need several waves and a player going down. `--down-at` makes sure one does; the leak tolerances next to the gates in `dev.sh` record what was measured.
- Logs land in `_staging/net-test/`, `_staging/pvp-test/`, `_staging/trade-test/`, `_staging/magic-test/`, `_staging/playtest/`, `_staging/camera-test.log`, `_staging/wall-test.log`, `_staging/parts-test.log` and `_staging/floors-test.log`.
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
| `--wall-check` | Runs `WallSelfTest` |
| `--parts-check` | Runs `PartsSelfTest` |
| `--floors-check` | Runs `FloorsSelfTest` (skeletons on: it needs the crypt's guards) |
| `--ways-dump <x0,z0,x1,z1 or all>` | Runs `Dev/WaysDump`: prints the navigation mesh's polygons in the area (with `all`, only those through the air) and quits, 1 when any goes through the air (`./dev.sh ways-dump`) |
| `--gold-lineup <height>` | Every pile gold falls in, one coin to ten, in a row in the field, seen from that high over the ground (`Dev/Lineup`) |
| `--ring-lineup` | A thrust's row of rings and a player's and a skeleton's arrow with their rings, laid across the field over and over, seen side on (`Dev/RingShow`); `./dev.sh ring-lineup` captures it |
| `--look-lineup <what,...>` | Figures in a row in the field (`Dev/Lineup`): `cast`, a pool of random looks (`townsfolk`, `skeletons@40`), or characters of the parts catalogue (`Druid`, or `Druid:bare` with nothing on) |
| `--camera 1-4` | Starts in that camera mode (numbered as C cycles them) |
| `--ai-playtest` | Minimizes the window without taking focus, so an AI-launched window never covers the user's work |
| `--screenshot`, `--shot-at S`, `--shots N`, `--shot-interval S` | Captures the window (`Dev/ScreenshotCapture`) at S seconds, N frames apart by the interval, then quits; any `--shot-*` implies `--screenshot`. Defaults: 6 s, 1 frame, 0.5 s. Each capture draws its own frame (`RenderingServer.ForceDraw`): the minimized window draws few of its own, and some runs none for seconds on end |
| `--no-ui` | Hides the HUD and overhead bars |
| `--weapon <id>`, `--back-weapon <id>` | This machine's player starts with those weapons in hand and on the back (`greatsword`, `quarterstaff`, `spear`, `scythe`, `sword-and-shield`, `<element>-staff`, `<element>-wand`, each also enchanted and at a level, as `spear+4`, `spear~void`, `spear~void+4`; they must differ); the self-tests give each bot a different pair |
| `--swing-survey` | Runs `Dev/SwingSurvey` and quits (`./dev.sh swing-survey`) |
| `--armour-lineup`, `--outfits A,B,...` | Stands the player's figure in a row in the field between the fortresses, once per tier of armour or once per outfit named, with a camera of its own in front of them (`Dev/Lineup`); `./dev.sh armour-lineup` captures it |
| `--weapon-lineup <id>` | Stands the player's figure in a row holding that weapon: its stance, its guard, each skill held at the moment it lands, on the back, and the weapon lying on the ground (`Dev/Lineup`); `./dev.sh weapon-lineup <id>` captures it |
| `--magic-lineup <elements or all>`, `--magic-barriers` | Stands the player's figure in a row with the staffs of those elements, each holding its area spell with what it throws beside it; or all eight inside their barriers (`Dev/Lineup`); `./dev.sh magic-lineup` captures it |
| `--barrier-drill` | Host only: every barrier that goes up is dealt a blow of 6 through the same path as a skeleton's, so the magic test sees every player's barrier take one (`NetSelfTest.Magic`) |
| `--zoom F` | The camera starts that much nearer or further (below 1 is nearer; the camera keeps it within its limits), for captures |
| `--down-at S` | Host only: S seconds in, takes one player (a client's when there is one) down through the normal damage path; the playtest uses it |
| `--wave-scale N` | Host only: each wave brings N times its skeletons; net-test and playtest pass 4 (`COOP_WAVE_SCALE` in `dev.sh`) and magic-test 3, since waves are not scaled by player count yet |
| `--orb-chance P` | Host only: every monster leaves a magic orb P of the time (0 to 1) in place of its own few in a hundred; net-test and playtest pass 0.25 so every machine sees orbs fall and be picked up, and `./dev.sh screenshot --orb-chance 1` shows them |
| `--potion-chance P` | Host only: every monster leaves a charge of the health potion P of the time (0 to 1) in place of its orb chance; net-test and playtest pass 0.25 (`COOP_POTION_CHANCE`) so every machine sees charges fall, be picked up and be drunk |
| `--start-gold N`, `--start-orbs N`, `--start-armour TIER` | Host only: every player starts with that much gold, that many orbs and that tier of armour (0 to 5), for trying the sellers and the armour; the trade test passes 150 gold at the weaponsmith, 280, 3 and tier 2 at the blacksmith, 10 gold and 1 orb at the merchant, 180 gold and 3 orbs at the enchanter, and 30 gold at the innkeeper |
| `--start-xp N` | Host only: every player starts with that much XP, at the level it reaches and with a point to spend for each level above the first, for trying levels (`./dev.sh host --start-xp 500` is level 5 with 4 points) |
| `--start-spent` | Every player starts spent: a third of its HP and its potion drunk dry (the host's), and no mana (its own machine's), for trying the innkeeper; the trade test passes it there |
| `--trade-drill` | With `--bot`: the bot trades with the seller it starts beside, through the shop window (`NetSelfTest.Trade`); the trade test passes it, and `./dev.sh screenshot --trade-drill` shows the window |
| `--town-drill` | With `--bot`: the bot breaks the nearest crate, sits on the nearest bench and lies on a bed in the inn (`NetSelfTest.Town`); the town test passes it, and `./dev.sh screenshot --town-drill --at 3` shows the bot on the bench (about 11 s, on the bed) |
| `--start-at x,z`, `x,y,z` or `<marker>[:N]` | Host only: players start on that spot of the ground (or at that height: 4.05 a storey up, -3.95 in the crypt), or on a layout's marker (`crypt`, `upstairs:1` for the second of that name, town first; an unknown one stops the launch listing the names), instead of in the town's courtyard. `./dev.sh screenshot --start-at` uses it to look at a place; the playtest starts its players inside a town room, so each has to walk out through a doorway |

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
- **New weapon:** a definition in `Core` (`Weapons.All`, its three skills in `Skills`), a look and a clip per skill in `CombatVisuals`, its models copied and imported, and a place in `dev.sh`'s `SESSION_WEAPONS` so a bot carries it through net-test. Look at it with `./dev.sh weapon-lineup <id>` before trusting any pose: the game's cameras are too high to judge one.
- **One hand, or a piece in the other:** `WeaponLook.OneHanded` takes the other hand off the weapon (`WeaponStance.OneHanded`: arms held in `Melee_Unarmed_Idle`, standing and over the leg clips). `WeaponLook.OffHand` is a second model for the left hand (a shield), with where it sits in the hand slot and where it hangs on the back; `CharacterRig.AttachOffHand` / `HoldOffHand` put it there, `HoldOnBack` and `GroundWeapons.Lying` carry it along. It is drawing only: to the rules the pair is one weapon, and reach and trail are measured on the right hand's model.
- **Enemies** take their looks from `CombatVisuals.LookFor(EnemyDefinition)` (`EnemyLook`): model, weapon, and for a bow the left hand and a rotation (Everdawn's bow settings). An attack can be a clip and a follow-up (`CombatVisuals.FollowUpFor`: the archer's draw, then release); its hit time counts across both, and `./dev.sh swing-survey` measures the release.
- **Who a blow may land on:** a player's are `PlayerCharacter.Foes()` (the standing skeletons, then in PvP the other players who are up), each site keeping its own reach, floor and wall test, and `DealTo` asks the host for the damage; a skeleton's blow or arrow asks `EnemyCharacter.MayStrike` of each player.
- **Projectiles:** an enemy attack with a `Projectile` (Core) looses one through `Arrows.In(tree).Loose` on the host at its hit time instead of testing an arc. `Arrows` flies any such attack, looked up by attack id, so a new ranged enemy is data plus a look.
- Anything that holds a character's meshes (`HitFlash`, the ghosts) gathers them when it needs them or rebuilds on a weapon change: a swapped-out weapon is freed.

## Look

Everdawn's, so the two games are drawn alike. Two places hold all of it:

- **The kit's `ToonLook`** (`kit/godot/ToonLook.cs`, from Everdawn's `CharacterAssembler.ApplyCrudeMetallicLook`): every figure, armour part and weapon is cel-shaded, with the kit's `toon_outline.gdshader` as a second pass for the dark outline, and metal only where the model is metal: by the cell of KayKit's 8 by 4 character texture for bodies, by colour (greys) for weapons. Figures get it part by part as the kit's `CharacterBody.Build` and `Dress` put their parts on (players, skeletons, sellers, armour, the lineups); weapons where their model is made (`CharacterRig.HoldWeapon` and the pieces it places on the back and in the off hand, `GroundWeapons.Lying`, the arrows `Util/Flight` draws). **A new place that instantiates a weapon or anything held has to call `ToonLook.ApplyToWeapon`**, and a figure has to be built through `CharacterBody`, or that one is drawn smooth and without an outline. A skeleton's `_Eyes` keep their own glowing material. The toon materials are shared, one per source material: do not change one for a single figure.
- **`Main/WorldLook`**: the sun, tones, glow and far depth-of-field are Everdawn's (`ForestArena`, `EnvironmentDefaults`): warm, 38 degrees down, soft orthogonal shadows drawn within 60 of the camera, ACES. The rest follows Kay Lousberg's "Basic Environment and Lighting Setup in Godot" (the KayKit author's video, https://www.youtube.com/watch?v=Vfr3n4WKsc0), as Everdawn's now does too: ambient light of a chosen colour (a slightly cool white at 0.5), a sky that is pale blue at the horizon and a greyer blue overhead, a haze of the horizon's colour that only starts 35 from the camera, SSAO at 2, SSIL, and contrast at 1.15. `Arena` applies it to the scene's `WorldEnvironment` and `Sun` as it starts. Three things to know before changing it: with ambient taken from the sky at full contribution Godot ignores `AmbientLightEnergy` (which is why it is a colour); metal mirrors the sky overhead, so a bluer `SkyAbove` turns steel blue; and the haze must stay the horizon's colour or the far fortress stands out against the sky instead of running into it.
- The level's own pieces (walls, floors, props) keep their standard materials, as Everdawn's dungeon pieces do, and the ghosts, spells, barrier and hit flash their own shaders.
- **Figures have a fill light of their own** (Everdawn's `CharacterFill`): a directional light without shadows on the camera, made in `WorldLook`, whose cull mask is render layer 2 alone, the layer `ToonLook` adds every mesh it dresses to (`ToonLook.CharacterLightLayer`). It lifts figures and what they hold without lighting the ground or the walls. Its energy is 0.25, half Everdawn's: Everdawn has it on only indoors, and in this field's sun 0.5 took skin and bone to white. A mesh that skips `ToonLook` gets no fill.
- **Whatever flies, or reaches from afar, asks `Util/Walls`** whether the level is in the way: `Walls.Hit` along each step of a flight (`Util/Flight`, which `Bolts` and `Arrows` both fly), `Walls.Between` for what a spell or a burst reaches. A new projectile or area effect that skips it goes through walls. A flight's first step starts at the caster's chest, never at the hand, which may be inside a wall.
- **A figure is built by the kit's `CharacterBody` from a `CharacterLook`**, never by loading a whole character model: there are none. A new kind of figure is a new look (or a pool of random ones), not a new asset.
- **A text or a bar over a thing goes through `Util/Overhead`**, not `position + Vector3.Up * height`: under the cameras that look straight down, heights over one spot are one spot on screen ([camera.md](../Docs/Design/camera.md), "Texts over things"). Prefer text on the screen (`SellerLabels`, `OverheadBars`, `GroundWeaponLabels`) to `Label3D`, which shrinks with the orthographic views.
- Flames light what is near them: the layouts carry a point light per torch, lantern, campfire and candle ([level-layouts.md](../Docs/Design/level-layouts.md)), which `LevelLayoutNode` adds without shadows.
- Anti-aliasing is the project's (`msaa_3d`, FXAA), the same as Everdawn's. The video adds TAA; it is left off, untried here.
- None of it shows in a headless run. Look at a change with `./dev.sh screenshot`, `weapon-lineup` or `armour-lineup`.

## Magic

- A staff is a weapon whose skills throw or land on an area: `SkillDefinition.Projectile` (with `Projectiles` and `VolleyInterval` for a volley), `SkillDefinition.Area`, and a guard with a `Barrier`. `Core` has the eight (`Weapons.Staffs`, `Skills.StaffOf`); design in [magic.md](../Docs/Design/magic.md).
- **Looks by element, not by staff:** `Character/ElementLooks` says which of the kit's elements each is (`EverdawnKit.Magic.MagicLooks` holds the colours and shader layers, the same as Everdawn's), the colour it leads with and the shape of its area spell's strikes; `ElementLooks.Made(element, mesh)` makes any mesh of an element, and `ElementLooks.Dress(element)` dresses the kit's effects (`SpellImpact`, `LightningFlash`) in its shader. The effects are the kit's: change them there and look at both games (`./dev.sh magic-lineup` here). `CombatVisuals.LookFor` gives every staff the one staff model with its element's ball at the head (`WeaponLook.Glow`), and `ClipFor` gives every thrown skill and every area skill the same two clips, so a seventh staff is a row in `Skills.Staffs` and a row in `ElementLooks`.
- **The claws** are one model in both hands (`OneHanded` with an `OffHand` of the same model), turned in the hand by `WeaponLook.HandRotation` and drawn bigger by `Scale`; a model not made standing up from its grip also says how it lies on the ground (`GroundRotation`). Their Rake and Spin are quick (`SkillDefinition.SwingSpeed`): whatever times a swing uses `CombatTiming.SwingSpeed(skill, attackSpeed)`, not the attack speed alone.
- **The bow** is an arrow's look with `OneHanded`, a `HandRotation` that turns it end for end, and an `OffHand` (the bow itself, in the left hand). Its arrows are thrown skills of a physical type, which `Bolts` flies as it flies bolts and draws as the arrow model (`Bolts.IsArrow`); a thrown skill is cast with the magic clip only when its type is not physical (`CombatVisuals.ClipFor`).
- **A wand and book** are a staff's look with `OneHanded` and an `OffHand` (the book), like the sword and shield. A staff's ball is a thrown skill with a `BlastRadius`, which `Bolts` bursts where it ends and `PlayerCharacter.BlastAt` deals on the caster's machine; a wand's held spell is the player's `SpellArea`.
- **Bolts** (`Main/Bolts`) fly on every machine; the caster's machine decides what they hit (`PlayerCharacter.StrikeWith`). **Area spells** are tested by the caster's machine in `AdvanceSwing` (`SkillHits.Catches`) and drawn on every machine by the player's `SpellArea`. **The barrier** is `PlayerVitals.Barrier` on the host and `BarrierBubble` on every machine.
- Nothing made of magic casts a shadow or is left behind: bolts end in a burst that frees itself, strikes free themselves when their tween ends, and the area's ring goes when the spell is let go.
- Look at a change with `./dev.sh magic-lineup` (and `barriers`): spells are small and brief under the game's cameras.
- **Enchanted weapons** are made on demand by `Weapons.ById` from a kind, an element and a level (`greatsword~fire+3`); the look is the plain weapon's with `WeaponLook.Enchant` set, which `CharacterRig.Adorned` turns into the element's shader over every mesh of the weapon (`ElementLooks.Enchantment`, a strength per element chosen by eye with `./dev.sh weapon-lineup greatsword~fire`).
- **Overlays are shared ground.** A weapon's element, a staff's ball and the hit flash all use a mesh's `MaterialOverlay`. `HitFlash` puts back what was under it when it is done; anything else that takes the overlay for a while must do the same, or enchanted weapons go dark after the first blow (the magic test's `dark_frames`).

## Armour

- `Core` has the tiers (`Armours`); `Character/ArmourLook` has what each looks like: which character its body, its arms and its legs come from (the three need not be the same character) and any pieces worn over it (a breastplate). `Own` is the figure's own parts. One entry per tier, or it throws.
- **Wearing it:** a tier's look is the figure's own look with those slots changed (`ArmourLook.Dressed`), and the figure is dressed in it again (`CharacterBody.Dress`). The head and cape are the figure's own. Parts made for another rig (Everdawn's Black Knight is on the large one) are not in the catalogue here.
- **A new look:** try it with `./dev.sh armour-lineup <Character>` and read the image, then name it in `ArmourLook`. Every medium-rig character of the catalogue is there to be tried.
- **Checked by** the trade test: every player's figure wears the parts of the tier the host holds for it (`ArmourLook.IsDressedFor`), on every machine.

## Assets

- **Character parts, palettes, the bare skeleton and the code that puts a figure together are shared with Everdawn** in the kit, a git submodule at `GodotClient/kit` (its own `README.md` and `AGENTS.md`). Never edit it for this game's sake alone, and never copy a part in beside it. Everything else is copied from Everdawn as needed, never referenced across repos.
- Keep Everdawn's layout under `GodotClient/assets/` (`characters/`, `weapons/`, `animations/`, ...) and copy each file's `.import` next to it. The `.import` records the `res://` path and UID, so an unchanged path keeps it valid. Never copy `.godot/`.
- Binary files (glb, png, ...) go through Git LFS per `.gitattributes`.
- Every figure is KayKit's `Rig_Medium` skeleton (`kit/rig/Rig_Medium.glb`) with parts on it (`kit/character_parts/`, by `kit/config/parts_catalog.json`), all from the kit ([Docs/Design/characters.md](../Docs/Design/characters.md)). Parts carry no animations; clips come from `assets/animations/Rig_Medium_*.glb`.
- **Heads are sized as Everdawn's are** (`CharacterBody.HeadScale` / `HeadgearScale`): the head and face at 0.75, what the head carries at 0.825, scaled on the mesh transforms around the head bone's rest position. Which part is which goes by its slot and by the bone the catalogue says carries it, never by its name. `[head-check]` and `parts-test` only read the scale back off the meshes: whether a size looks right is a screenshot question (`./dev.sh look-lineup`). An earlier attempt scaled the head bone in a `SkeletonModifier3D`: its numbers read 0.75 but the heads did not look smaller in game, so it was replaced by Everdawn's proven method. History and what differs from Everdawn: [characters.md](../Docs/Design/characters.md).
- The rig GLBs import as **animation libraries**, not scenes. When copying another `Rig_*.glb`, set `importer="animation_library"` and `type="AnimationLibrary"` in its `.import` and drop the `path=` / `dest_files=` lines; `./dev.sh import` regenerates them.
- Numbers taken from the art (hit times, weapon reach, ground speed, chest bias) are measured from the GLB data, and each is noted where it is used; `./dev.sh swing-survey` measures weapon skills. Blender is available through the Blender MCP for measuring.

## KayKit and Godot quirks

Already solved in Everdawn: [../Everdawn/Docs/Design/godot-pitfalls.md](../../Everdawn/Docs/Design/godot-pitfalls.md). Relevant here: rig facing backwards (`ModelYawOffset`), static accessories needing `BoneAttachment3D`, `AnimationPlayer.Play()` before `AddAnimationLibrary` crashing (`RigAnimations.AddPlayerTo` makes a figure's player in the safe order). Everdawn's "never share an `AnimationLibrary`" rule is about libraries taken from an instanced GLB's player; imported library resources are shared by every character here (`net-test` runs three per process).

- Rig track paths look like `Rig_Medium/Skeleton3D:<bone>`.
- Yaw is counter-clockwise from above, and yaw 0 faces -Z (`Core/Ground.cs`).
- A clip that turns the root bone (Spin's loop) cannot be layered on the upper body; `CombatVisuals.IsFullBody` marks those.
- **What C# lets go of at exit must not hold the last reference to a texture.** A `Variant` made from a Godot object (`SetShaderParameter("albedo_tex", texture)`) holds it until disposed, and a C# wrapper left to the garbage collector frees its object on the finalizer thread. At exit that can come after the renderer's last flush: the texture's free is queued and never run, and the log ends with `ERROR: N RID allocations of type '...DummyTexture...' were leaked at exit`, which fails the self-test. `--verbose` lists the leaked `Image`s: headless, a texture's `GetImage()` is that same image, so printing `GetImage().GetInstanceId()` per texture before quitting names them. Dispose such a Variant with `using`, and dispose the wrappers a node keeps of materials it made when the node is freed, as `GhostTrail` does.
