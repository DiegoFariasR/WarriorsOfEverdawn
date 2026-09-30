# Warriors of Everdawn -- Project Guidelines

Living reference for AI assistants working on this project. Update it when a decision is made or a system changes. Structure and working rules follow the sister project Everdawn (`../Everdawn/AGENTS.md`).

---

## Project Overview

Action RPG, played solo or together by up to 8 players (one of them hosts), co-op or PvP. The player picks one weapon; each weapon has two skills. Diablo-style framing with fewer enemies. Controls: WASD + mouse to aim, or controller; click-to-move is planned ([camera.md](Docs/Design/camera.md)).

First slice: the two-handed sword (Slice and Spin) against waves of the base KayKit skeletons.

The client is **Godot 4.7.2 .NET** (`GodotClient/`); rules that need no engine live in plain C# (`Core/`). Art comes from Everdawn (`../Everdawn`), copied in as needed.

---

## CRITICAL EXECUTION RULES (read first, every session)

### NEVER launch Godot with a visible window from AI automation

Every AI-spawned Godot process MUST include `--headless`. There is no minimized-window mode here yet (Everdawn's `--ai-playtest` was not ported), so there is no exception.

Forbidden — `.claude/hooks/headless_guard.py` BLOCKS these (if you see "BLOCKED:" in stderr, the call is the bug, not the hook):

- `./dev.sh run` / `editor` / `host` / `join`
- Direct Godot executable / `$GODOT` invocation without `--headless`

**Every `--headless` launch in `dev.sh` MUST be wrapped with `timeout N`** (or carry an explicit `--quit` flag, or a `# allow-hang: <reason>` opt-out on the same or preceding line). `./dev.sh check-godot-timeouts` (part of `./dev.sh health`) is the gate.

Rationale and the list of already-correct launchers: [Docs/ops/headless-launch.md](Docs/ops/headless-launch.md).

---

## Architecture Rules

### Core (plain C#, no Godot dependency)
`WarriorsOfEverdawn.Core`: rules that can be tested without the engine -- stats and mana, damage, skill and enemy definitions, hit arcs and timing, stagger, dodge charges, leg-direction selection, turning and move speeds, enemy AI decisions. Never reference Godot types. Scope rules: [Core/AGENTS.md](Core/AGENTS.md).

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

When a test asserts a value derived from a constant (`MoveSpeed.Run`, `StatRules.ManaPerWis`, `DodgeRules.Distance`, ...), reference the constant in the expected expression — don't bake the resolved literal. A retune then touches one file instead of every test that pinned the old number. Same for theory inputs that exist only to land on a clean expected number.

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
| `./dev.sh net-test` | Headless host + 2 bot clients, about 25 s; asserts movement, attacks, hits, kills and damage reach every peer, the chest stays on the aim, turning respects its limit, heads are at their scale, swings play at the attack speed, Spin lands, roots and turns the body, weapon trails and hit blinks show and clear, the blade tip at hit time matches each range, the HUD shows real HP for the player and every skeleton, and dodges keep their distance, charges and ghosts |
| `./dev.sh pvp-test` | Headless PvP host + 1 bot client, no skeletons; asserts players hit, damage and see each other |
| `./dev.sh camera-test` | Headless; in every camera mode, W must move the character up the screen and D right; the HUD sits on screen without overlaps |
| `./dev.sh smoke` | `camera-test`, `net-test` and `pvp-test` in turn; the regression gate for client changes |
| `./dev.sh health` | Drift dashboard: formatting, doc references, pending refactors, agent and skill docs, headless timeouts |
| `./dev.sh run`, `./dev.sh host`, `./dev.sh join [address]`, `./dev.sh editor` | User only; blocked for AI sessions |

`./dev.sh help` lists every command; [.claude/tools-index.md](.claude/tools-index.md) says which to reach for when.

There is no screenshot pipeline: headless runs draw nothing, so visual checks are the user's. AI sessions verify with `./dev.sh test` and the self-tests, whose `[*-check]` lines measure what can be measured (chest angle, head scale, blade reach, trail and flash lifetimes, HUD values).

---

## Design docs

Check `Docs/Design/` before implementing a mechanic. Open questions there are undecided -- ask before assuming.

- [Docs/Design/locomotion.md](Docs/Design/locomotion.md) -- facing model, leg direction selection, upper/lower body animation split, clip chest bias, dodge.
- [Docs/Design/multiplayer.md](Docs/Design/multiplayer.md) -- authority, what is replicated, connecting, testing, known engine issue.
- [Docs/Design/combat.md](Docs/Design/combat.md) -- hit rules and measured hit times, skills, enemies, waves, player HP and respawn.
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
12. **Run an isolation test before defending a change as innocent.** When a user reports a symptom that persists after a change, do not argue logically that the change "can't" be the cause -- propose a concrete test (stash A/B, revert, a measured check) and run it. A green headless check is not proof of what the user sees: the head-size fix read 0.75 in the logs while heads looked unchanged in game.

### Before Closing
13. **Update `Docs/Design/`** when a mechanic lands or a decision changes; keep each doc's "Verified by" paragraph in step with its gates.
14. **Update this file** (or the per-scope one) when a system or convention changes.
15. **Grep for orphans after renames and removals.** When you remove or rename a type, method, flag, log tag or `dev.sh` command, grep the repo for the old name in the same change.

---

## Gameplay Quality Warnings

Design / playtesting review heuristics — not a per-change checklist. Apply only when explicitly asked to review design quality. See [Docs/Design/quality-warnings.md](Docs/Design/quality-warnings.md).
