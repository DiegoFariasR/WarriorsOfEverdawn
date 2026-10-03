# Warriors of Everdawn -- Project Guidelines

Living reference for AI assistants working on this project. Update it when a decision is made or a system changes. Structure and working rules follow the sister project Everdawn (`../Everdawn/AGENTS.md`).

---

## Project Overview

Action RPG, played solo or together by up to 8 players (one of them hosts), co-op or PvP. The player picks one weapon; each weapon has two skills, a lunge (a dash thrown with the attack button) and a guard. Diablo-style framing with fewer enemies. Controls: WASD + mouse to aim, or controller; click-to-move is planned ([camera.md](Docs/Design/camera.md)).

Weapons so far: greatsword, quarterstaff, spear, scythe, sword and shield (one weapon), bow (arrows without end), claws (a pair, the quickest), warhammer (two-handed, the slowest, stuns), and eight magic staffs and eight wands with their books (a bolt or volley, a spell held on an area or a ball that bursts, a barrier: [magic.md](Docs/Design/magic.md)), each with a primary and a held secondary, against waves of the base KayKit skeletons ([combat.md](Docs/Design/combat.md)).

The client is **Godot 4.7.2 .NET** (`GodotClient/`); rules that need no engine live in plain C# (`Core/`). Art comes from Everdawn (`../Everdawn`), copied in as needed.

---

## CRITICAL EXECUTION RULES (read first, every session)

### NEVER launch Godot with a visible window from AI automation

Every AI-spawned Godot process MUST include `--headless`. Exception: `--ai-playtest`, only when a window is genuinely required to capture what the game draws (`./dev.sh screenshot`); the window opens off-screen, minimized and unfocused.

Forbidden — `.claude/hooks/headless_guard.py` BLOCKS these (if you see "BLOCKED:" in stderr, the call is the bug, not the hook):

- `./dev.sh run` / `editor` / `host` / `join`
- Direct Godot executable / `$GODOT` invocation without `--headless` or `--ai-playtest`

**Every AI-run launch in `dev.sh` (`--headless` or `--ai-playtest`) MUST be wrapped with `timeout N`** (or carry an explicit `--quit` flag, or a `# allow-hang: <reason>` opt-out on the same or preceding line). `./dev.sh check-godot-timeouts` (part of `./dev.sh health`) is the gate.

Rationale and the list of already-correct launchers: [Docs/ops/headless-launch.md](Docs/ops/headless-launch.md).

---

## Architecture Rules

### Core (plain C#, no Godot dependency)
`WarriorsOfEverdawn.Core`: rules that can be tested without the engine -- stats and mana, damage, skill and enemy definitions, hit arcs and timing, stagger, dash charges, leg-direction selection, turning and move speeds, enemy AI decisions. Never reference Godot types. Scope rules: [Core/AGENTS.md](Core/AGENTS.md).

### GodotClient (real-time play)
`WarriorsOfEverdawn.csproj`: input, movement, collision, camera, animation, VFX, HUD, networking, and the headless self-tests. Before adding rules logic to a Godot node, check whether it belongs in `Core`. Scope rules, script layout and asset pipeline: [GodotClient/AGENTS.md](GodotClient/AGENTS.md).

### Core.Tests
xunit tests for `Core`. Every mechanic whose logic lives in `Core` ships with tests.

### Solo and multiplayer are one code path
Up to 8 players, one of them hosting; solo is a host nobody joins. Each player's machine owns its character's movement and aim and decides whether its own swings hit; the host owns enemies, all HP, damage and (later) loot. Every machine derives animation from replicated velocity and aim, never from sent poses. Full rules: [Docs/Design/multiplayer.md](Docs/Design/multiplayer.md).

---

## C# Language Version & Code Style

### Language Version

- `Core`, `GodotClient`: `net8.0`, `LangVersion 12`.
- `Core.Tests`: `net10.0`.

### Comments — CRITICAL RULE

**Do not add comments unless the why is genuinely non-obvious from the code.**

Never comment:
- What a method, property, or field does when the name already says it (`/// <summary>` on self-evident members is noise)
- Natural-reading conditionals or assignments
- Pipeline boilerplate, standard patterns, or anything a competent reader would infer instantly

Only comment:
- Formulas and numbers derived from a design decision or a measurement (e.g. a hit time measured from a clip)
- Deliberate omissions or intentional no-ops
- Non-obvious ordering constraints or subtle invariants that would cause bugs if violated

### Test conventions

When a test asserts a value derived from a constant (`MoveSpeed.Run`, `StatRules.ManaPerWis`, `DashRules.Distance`, ...), reference the constant in the expected expression — don't bake the resolved literal. A retune then touches one file instead of every test that pinned the old number. Same for theory inputs that exist only to land on a clean expected number.

**Test helper hygiene.** When a test helper grows past 5 optional parameters, or the same `new ClassName(...)` literal appears in 3+ sites within one file, extract a fixture or builder.

### Formatting

`.editorconfig` is the source of truth. `./dev.sh format` (`dotnet format WarriorsOfEverdawn.slnx`) enforces it for all three projects; `./dev.sh health` checks it without changing anything.

### Reuse vs. duplication

Two occurrences = copy is fine. Three = extract a helper unless the shapes diverge meaningfully (different lifetimes, different invariants, different consumers). Cite `[[architecture-growth#N]]` when growth trajectory decides the call.

---

## Commands

| Command | What it does |
|---|---|
| `./dev.sh build` | Build the solution |
| `./dev.sh test` | Run `Core.Tests` |
| `./dev.sh format` | `dotnet format` the solution |
| `./dev.sh import` | Headless asset import; run after copying assets in |
| `./dev.sh net-test` | Headless host + 6 bot clients, one per melee weapon, about 50 s against waves four times their size; asserts every machine shows each player's weapons in hand and on the back and sees the bots swap sets, weapons dropped lie on the ground everywhere until taken up and are labelled when close, skeletons leave gold, souls and now and then a magic orb that every player earns, players start in the safe town and waves rise in the enemy fortress and find their way out, movement, attacks, hits, kills and damage reach every peer, the chest stays on the aim, turning respects its limit, heads are at their scale, swings play at the attack speed, Spin lands, roots and turns the body, weapon trails and hit blinks show and clear, the blade tip at hit time matches each range, the HUD shows real HP for the player and every skeleton, dashes keep their distance, charges and ghosts, a held Spin carries on through a dash, lunges land as their dash ends and reach their range, weapons are carried in both hands, arrows are drawn where they fly, guards go up, stay up while held, slow the player, block and parry, hits of all three physical damage types reach the host and meet the skeletons' weakness, and every machine sees skeletons bleeding with the status named over their bars |
| `./dev.sh pvp-test` | Headless PvP host + 1 bot client, no skeletons; asserts players hit, damage and see each other, bleed from blades, and, frozen and stunned on cue by the host, neither move nor swing while held |
| `./dev.sh magic-test` | Headless host + 5 bot clients, four with wands (four elements in hand, the other four on the backs), one with staffs and one with an enchanted bow and greatsword, about 40 s; asserts every machine throws bolts (one, or a volley's three), lands them and sees the others', draws the area spells in rounds of strikes, bursts the staff's ball on what it catches, shows barriers and sees its own take blows and come back, that every weapon of an element shows it on every machine, that the host counted hits and barrier blows for every staff, the bow's arrows, and enchanted blows as part magic, that fire met the skeletons' weakness while water and earth were taken as they are, and that skeletons burn and, frozen and stunned on cue by the host, stand held in their ice on every machine |
| `./dev.sh trade-test` | For each seller in the town (weaponsmith, blacksmith, merchant, enchanter), a headless host + 1 bot client beside it with gold and orbs; asserts each opens the shop window, gets the seller's first three offers (three weapons; its hand weapon, its back weapon and its armour each made a step better; its hand weapon, its back weapon and an orb sold for gold; three enchantments of its hand weapon), is refused a fourth by the window and by the host, ends with the right weapons in hand and on the back, the armour worn, the cost taken and the pay given, and that the host agrees |
| `./dev.sh camera-test` | Headless; in every camera mode, W must move the character up the screen and D right; the HUD sits on screen without overlaps |
| `./dev.sh wall-test` | Headless; every kind of bolt, ball and arrow is loosed at a fortress wall from afar and from against it, and must end at the wall's face; the line of sight spells go by is read across the wall and along it |
| `./dev.sh parts-test` | Headless; every part of the character catalogue is put on a figure in its slot and must come out skinned to the rig and sized as its slot says; 200 figures are drawn at random from each pool and dressed |
| `./dev.sh floors-test` | Headless; the player walks up into each of the town's storeys and down into the crypt: every way is found and walked, what is above is hidden and nothing else, and the crypt's guards keep to it, come for the player, fall and leave a treasure the player takes |
| `./dev.sh smoke` | `camera-test`, `wall-test`, `parts-test`, `floors-test`, `net-test`, `pvp-test`, `trade-test` and `magic-test` in turn; the regression gate for client changes |
| `./dev.sh playtest` | `net-test` run for 2.5 minutes with a player taken down on cue: every net-test gate, plus waves keep coming, the dead and damage numbers are removed on time, the node count stays flat across waves, and a downed player stays put and gets back up at full HP. `--screenshots N` captures frames through the session. For changes to anything that lives across a session |
| `./dev.sh swing-survey` | Each weapon skill's clip followed through the hand: when the weapon moves fastest, when it reaches furthest, and how far, standing and moving. Where Core's hit times come from |
| `./dev.sh screenshot` | A bot plays solo in an off-screen, minimized window; saves `_staging/screenshot.png` after `--at` seconds, or a series with `--frames N --interval S`; `--camera 1-4`, `--zoom F`, `--no-ui`, `--no-enemies`, `--weapon <id>`, `--back-weapon <id>`. Read the image to check what the game draws |
| `./dev.sh armour-lineup` | The Knight in each tier of armour, side by side from the front, in the same off-screen window as `screenshot`; saves `_staging/armour-lineup.png`. `armour-lineup Rogue,Cleric,...` shows those outfits instead, to try parts no tier uses yet |
| `./dev.sh weapon-lineup <id>` | The Knight holding that weapon in its stance, its guard and each skill at the moment it lands, carrying it on the back, and the weapon lying on the ground, from the front in the same off-screen window as `screenshot`; saves `_staging/weapon-lineup.png`. For checking how a new weapon sits in the hands |
| `./dev.sh magic-lineup` | The wands casting, side by side from above the shoulder: each holds its area spell with what it throws beside it; `magic-lineup fire,void` shows those elements, `magic-lineup barriers` each staff inside its barrier; saves `_staging/magic-lineup.png`, or with nothing named all eight as `magic-lineup-1.png` and `magic-lineup-2.png` |
| `./dev.sh look-lineup` | Figures in a row from the front, in the same off-screen window as `screenshot`; saves `_staging/look-lineup.png`. `look-lineup cast` (the default) is the player, the enemies and the sellers; `look-lineup townsfolk` or `skeletons` eight figures drawn at random from that pool (`townsfolk@40` from seed 40 on); `look-lineup Druid,Witch` those characters of the parts catalogue as they were made (`Druid:bare` with nothing on) |
| `./dev.sh gold-lineup` | Every pile gold falls in, one coin to ten, in a row on the ground, in the same off-screen window as `screenshot`; saves `_staging/gold-lineup-low.png` (as from behind a player) and `_staging/gold-lineup-high.png` (as the cameras that look down see them) |
| `./dev.sh gold-piles` | Rebuilds the gold pile models (two to ten coins) from the one coin, with Blender in the background; run `./dev.sh import` after |
| `./dev.sh level-fortresses` | Regenerates the two fortress layouts from `Tools/level_fortresses.py` (deterministic; `--seed N`); run after changing the generator, then `level-audit` |
| `./dev.sh level-audit` | Audits `GodotClient/config/levels/*.layout.json` without Godot: missing files, solids run into each other, a marker or a gate blocked, stairs with nowhere to step off at either end, pieces off the ground |
| `./dev.sh level-list <town\|fortress> [x0,z0,x1,z1] [--room -1\|1]` | Lists what a layout has in an area (placements with boxes, markers, areas), in world coordinates or in a room's `(u, v)` |
| `./dev.sh ways-dump [x0,z0,x1,z1]` | Headless; prints the navigation mesh's polygons in the area, or only those that go through the air between floors; exit 1 when any does |
| `./dev.sh health` | Drift dashboard: formatting, doc references, pending refactors, agent and skill docs, headless timeouts, level layouts |
| `./dev.sh run`, `./dev.sh host`, `./dev.sh join [address]`, `./dev.sh editor` | User only; blocked for AI sessions |

`./dev.sh help` lists every command; [.claude/tools-index.md](.claude/tools-index.md) says which to reach for when.

Two kinds of verification, for two kinds of claim. The self-tests' `[*-check]` lines measure what can be measured (chest angle, head scale, blade reach, trail and flash lifetimes, HUD values); headless runs draw nothing, so they cannot say how anything looks. For that, capture with `./dev.sh screenshot` and look at the image (`python Tools/zoom_region.py` crops and enlarges a small element). How motion feels in play stays the user's to judge.

---

## Design docs

Check `Docs/Design/` before implementing a mechanic. Open questions there are undecided -- ask before assuming.

- [Docs/Design/locomotion.md](Docs/Design/locomotion.md) -- facing model, leg direction selection, upper/lower body animation split, clip chest bias, dash.
- [Docs/Design/multiplayer.md](Docs/Design/multiplayer.md) -- authority, what is replicated, connecting, testing, known engine issue.
- Look (toon shading, outlines, light, atmosphere: Everdawn's) -- `GodotClient/AGENTS.md`, "Look".
- [Docs/Design/magic.md](Docs/Design/magic.md) -- the eight magic staffs and the eight wands with their books: bolt or volley by element, the ball a staff throws to burst and the spell a wand holds on an area, the barrier, how they look (Everdawn's colours and shaders), who decides their hits, where they are had, and the enchanter's enchantments of other weapons (a share of the damage made magic).
- [Docs/Design/damage-types.md](Docs/Design/damage-types.md) -- Everdawn's twelve damage types in three families, which weapon and spell deals which, resistances and weaknesses (skeletons'), the statuses each type builds (burning, chilled and frozen, dizzy and stunned, bleeding, the astral tiers) with their cancellations, and the colours a hit, a skill and a status show in.
- [Docs/Design/trade.md](Docs/Design/trade.md) -- sellers in the town (the weaponsmith, the enchanter, the blacksmith who makes weapons and armour better, and the merchant who buys weapons and orbs), the one shop window they all use, weapon levels +1 to +10 and armour's five tiers and what they cost, what selling pays, how a trade is decided and where a bought weapon goes.
- [Docs/Design/level-layouts.md](Docs/Design/level-layouts.md) -- the map: the allied town and the enemy fortress, how they are generated, audited and loaded (Everdawn's level-layout technique), what they do in play.
- [Docs/Design/combat.md](Docs/Design/combat.md) -- hit rules and measured hit times, skills, enemies, waves, player HP and respawn.
- [Docs/Design/characters.md](Docs/Design/characters.md) -- how a figure is made (Everdawn's body part system): parts, slots and the catalogue, looks, head size, armour as a change of parts, palettes, figures drawn at random from a pool, and bringing parts over from Everdawn.
- [Docs/Design/camera.md](Docs/Design/camera.md) -- the four camera modes (C cycles) and the controls each one uses.
- [Docs/Design/ui.md](Docs/Design/ui.md) -- HUD style (ported from Everdawn), elements, what stats and mana do so far, font licences.
- [Docs/Design/architecture-growth.md](Docs/Design/architecture-growth.md) -- per-system growth trajectory (`frozen` / `rare` / `active`) and what it implies for refactors.
- [Docs/Design/Refactor/pending-refactors.md](Docs/Design/Refactor/pending-refactors.md) -- the refactor queue `/refactor` and `/auto-maintain` drain.

---

## Repository Structure

- `Core/`, `Core.Tests/` — engine-free rules and their tests.
- `GodotClient/` — Godot 4.7.2 .NET client (`WarriorsOfEverdawn.csproj`).
- `Docs/Design/` — design references; `Docs/ops/` — how-to notes for tooling.
- `Tools/` — Python drift checks behind `./dev.sh health`.
- `_staging/` — gitignored self-test logs.

**Subsystem entry points.** Per-scope `AGENTS.md` files carry the detail for each tree: [Core/AGENTS.md](Core/AGENTS.md) (what belongs in Core, tests); [GodotClient/AGENTS.md](GodotClient/AGENTS.md) (script layout, assets, KayKit quirks, self-test conventions). This root file carries only the cross-cutting rules.

---

## Design Priorities

1. Keep scope small
2. **Check the growth axis before any structural change.** [Docs/Design/architecture-growth.md](Docs/Design/architecture-growth.md) -- cite `[[architecture-growth#N]]` when a refactor's chosen direction hinges on whether the system will grow.
3. Keep architecture clean: rules in `Core`, presentation and networking in `GodotClient`
4. Keep solo and multiplayer on one code path
5. Prefer simple, robust solutions over overengineering

---

## Rules for AI Assistants

### Before Starting
1. **Check `Docs/Design/`** for design decisions before implementing a mechanic. Open questions are undecided -- ask before assuming.
2. **Before touching assets, rigs or animation**, read [GodotClient/AGENTS.md](GodotClient/AGENTS.md).

### During Implementation
3. **Every new mechanic must include tests.** Rules logic -> `Core/` + tests in `Core.Tests/`. Client behaviour that can be measured -> a `[*-check]` line in the self-tests and its gate in `dev.sh`. Do not consider a feature complete until its checks pass.
4. Godot-specific code -> `GodotClient/` only.
5. **Keep changes focused**: one feature per change. **Do not overengineer.**
6. **Measure, don't assume.** Numbers taken from art (hit times, reach, ground speed, chest bias) are measured from the GLB data and noted where they are used. Everdawn's KayKit quirks are listed in [GodotClient/AGENTS.md](GodotClient/AGENTS.md).

### Subagent routing
7. **For non-trivial tasks, check [.claude/agents/README.md](.claude/agents/README.md) first.** This repo ships specialized subagents (core-engineer, godot-engineer, polish-engineer) with mutually exclusive trigger conditions; the README is the canonical roster and decision table. Tool/command index: [.claude/tools-index.md](.claude/tools-index.md). Run `./dev.sh lint-agents` to verify the agent and skill definitions reference real commands and files.

    **Runnable-verifier contract — applies to every subagent.** Before reporting done, you MUST execute every Definition-of-Done bullet that contains a `./dev.sh` or `dotnet` command, capture the exit code, and confirm it returned 0. Do not check off a shell-command bullet without running it. If a command fails, the agent stops and surfaces the failure rather than reporting a partial completion.

### Helper tools
8. **Flag helper opportunities proactively.** When a repeated manual step could be a script or a `dev.sh` command, say so: "This could be a helper -- want me to build one?"
9. **Register new tools and subagents.** See [Docs/ops/registering-tools.md](Docs/ops/registering-tools.md) for the surfaces to touch.

### Accuracy
10. **Say "I don't know" when uncertain.** Never fabricate. Cite sources (file + line, doc section, measured log line). Do not assert things you have not read.
11. **Quote verbatim** when exact wording matters.
12. **Run an isolation test before defending a change as innocent.** When a user reports a symptom that persists after a change, do not argue logically that the change "can't" be the cause -- propose a concrete test (stash A/B, revert, a measured check) and run it. A green headless check is not proof of what the user sees: the head-size fix read 0.75 in the logs while heads looked unchanged in game. Check a visible change in a screenshot before calling it done.

### Before Closing
13. **Update `Docs/Design/`** when a mechanic lands or a decision changes; keep each doc's "Verified by" paragraph in step with its gates.
14. **Update this file** (or the per-scope one) when a system or convention changes.
15. **Grep for orphans after renames and removals.** When you remove or rename a type, method, flag, log tag or `dev.sh` command, grep the repo for the old name in the same change.

---

## Gameplay Quality Warnings

Design / playtesting review heuristics — not a per-change checklist. Apply only when explicitly asked to review design quality. See [Docs/Design/quality-warnings.md](Docs/Design/quality-warnings.md).
