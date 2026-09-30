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
| `Main/` | Arena and session start, launch flags (`LaunchOptions`), camera and camera modes, HUD, overhead bars, session rules (PvP) |
| `Theme/` | Everdawn-style colours, fonts, panels and bars (`UiTheme`) |
| `Player/` | Networked character (`PlayerCharacter`), host-owned vitals (`PlayerVitals`), human and bot controls (`PlayerControls`) |
| `Enemy/` | Skeletons (`EnemyCharacter`, host-run AI), wave director |
| `Character/` | Rig clips (`RigAnimations`), animator layers, torso twist, rig pieces and head sizing (`CharacterRig`), clip-per-skill and enemy looks (`CombatVisuals`), weapon trails, hit flash, dodge ghosts |
| `Dev/` | Headless self-tests: `NetSelfTest` (net-test, pvp-test), `CameraSelfTest` (camera-test) |
| `Util/` | Small shared helpers |

## Headless self-tests

Headless runs draw nothing, so the self-tests measure instead: each check prints one `[<name>-check] key=value ...` line, and `dev.sh` gates on it (the `gate` helper reads the fields as `v["key"]` in an awk condition).

- The game prints its own limits and expectations next to what it measured (`limit_deg_s`, `head_expected`, `spin_speed_limit`, ...), so a retune in `Core` never leaves a stale number in `dev.sh`.
- A missing line fails its gate. A new check is not done until its gate exists; see [Docs/ops/registering-tools.md](../Docs/ops/registering-tools.md).
- Bots (`--bot`) drive the characters: they seek the nearest hostile, swing, spin while hostiles are in reach and dodge in bursts, so every feature gets exercised without a person.
- Logs land in `_staging/net-test/`, `_staging/pvp-test/` and `_staging/camera-test.log`.
- `KNOWN_DISCONNECT_ERROR` in `dev.sh` whitelists one engine error on disconnect (godot#86814); every other `ERROR` in a log fails the run.

## Launch flags

Parsed by `scripts/Main/LaunchOptions.cs`, after the `--` separator: `--host`, `--join <address>`, `--port N`, `--pvp`, `--no-enemies`, `--bot`, `--quit-after <seconds>`, `--camera-check`. The self-tests are these flags plus `--headless`; any new AI-facing mode gets `--headless` built in from day one.

## Assets

- Copied from Everdawn as needed, never referenced across repos.
- Keep Everdawn's layout under `GodotClient/assets/` (`characters/`, `weapons/`, `animations/`, ...) and copy each file's `.import` next to it. The `.import` records the `res://` path and UID, so an unchanged path keeps it valid. Never copy `.godot/`.
- Binary files (glb, png, ...) go through Git LFS per `.gitattributes`.
- Knight, Barbarian and the Skeleton Warrior / Minion / Rogue / Mage all share KayKit `Rig_Medium`. Character GLBs carry no animations; clips come from `assets/animations/Rig_Medium_*.glb`.
- **Heads are sized as in Everdawn** (`CharacterRig.ShrinkHead`, its `HeadScale` / `HeadgearScale`): head, face and eye meshes at 0.75, headgear at 0.825, scaled on the mesh transforms around the head bone's rest position. Meshes are sorted by name (`_Head` / `_Eyes` / `_Jaw`; Helmet / Visor / Hat / Hood / Crown); a model with no head mesh fails loudly. An earlier attempt scaled the head bone in a `SkeletonModifier3D`: its numbers read 0.75 but the heads did not look smaller in game, so it was replaced by Everdawn's proven method.
- The rig GLBs import as **animation libraries**, not scenes. When copying another `Rig_*.glb`, set `importer="animation_library"` and `type="AnimationLibrary"` in its `.import` and drop the `path=` / `dest_files=` lines; `./dev.sh import` regenerates them.
- Everdawn's asset folders are self-contained, but its modular character parts are assembled by code (`../Everdawn/GodotClient/scripts/Character/`). Bring that over only when per-part character selection starts.
- Numbers taken from the art (hit times, blade reach, ground speed, chest bias) are measured from the GLB data, and each is noted where it is used. Blender is available through the Blender MCP for measuring.

## KayKit and Godot quirks

Already solved in Everdawn: [../Everdawn/Docs/Design/godot-pitfalls.md](../../Everdawn/Docs/Design/godot-pitfalls.md). Relevant here: rig facing backwards (`ModelYawOffset`), static accessories needing `BoneAttachment3D`, `AnimationPlayer.Play()` before `AddAnimationLibrary` crashing. Everdawn's "never share an `AnimationLibrary`" rule is about libraries taken from an instanced GLB's player; imported library resources are shared by every character here (`net-test` runs three per process).

- Rig track paths look like `Rig_Medium/Skeleton3D:<bone>`.
- Yaw is counter-clockwise from above, and yaw 0 faces -Z (`Core/Ground.cs`).
- A clip that turns the root bone (Spin's loop) cannot be layered on the upper body; `CombatVisuals.IsFullBody` marks those.
