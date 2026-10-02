# Tools Index

Quick lookup for `./dev.sh` subcommands and `Tools/*.py` scripts. Subagents reference this so they don't have to grep around. Source of truth is `./dev.sh help` -- keep this in sync when commands change (`./dev.sh lint-agents` flags commands that no longer exist).

## When to use what

| Want to... | Use |
|---|---|
| Build everything | `./dev.sh build` |
| Run the Core tests | `./dev.sh test` |
| Format the C# | `./dev.sh format` (`--verify-no-changes` to only check) |
| Import copied-in assets | `./dev.sh import` |
| Check a client change end to end (movement, combat, animation, VFX, HUD, dash, guard, across 3 peers) | `./dev.sh net-test` |
| Check player-on-player hits and damage | `./dev.sh pvp-test` |
| Check sellers, the shop window, purchases and weapon improvements (cost taken, weapon delivered, refusals) | `./dev.sh trade-test` |
| Check camera modes, controls mapping and HUD layout | `./dev.sh camera-test` |
| Run all three self-tests (the client regression gate) | `./dev.sh smoke` |
| See every tier of armour on the Knight from the front, or try other outfits on it | `./dev.sh armour-lineup [Outfit,Outfit,...]` -> `_staging/armour-lineup.png` |
| Measure a weapon skill's hit time and reach from its clip (standing and moving), or when a ranged enemy's shot leaves | `./dev.sh swing-survey` |
| Check what lives across a session: waves, removal of the dead and of damage numbers, node count per wave, going down and back up | `./dev.sh playtest [--screenshots N]` (about 3 minutes) |
| See what the game draws (a bot plays solo; off-screen, minimized window) | `./dev.sh screenshot [--at S] [--frames N --interval S] [--camera 1-4] [--zoom F] [--no-ui] [--no-enemies] [--weapon <id>] [--back-weapon <id>]` -> `_staging/screenshot.png` or `_staging/screenshot_<n>.png` |
| Enlarge a small part of a capture | `python Tools/zoom_region.py <png> <x> <y> <w> <h> [--scale N]` (`--grid` to find coordinates) |
| Project-wide drift sweep | `./dev.sh health` (`--only <check>`, `--skip <check>`, `--list`) |
| Find broken links, `file:line` refs and `./dev.sh` commands in the docs | `./dev.sh check-docs` (`--fix` re-points moved files) |
| Check the refactor queue's references still resolve | `./dev.sh audit-refactors` (`--id N`, `--match <text>`, `--by-status`) |
| Check agents, skills and this index reference real things | `./dev.sh lint-agents` |
| Catch unwrapped headless Godot launches in `dev.sh` | `./dev.sh check-godot-timeouts` |
| Change the fortresses (walls, props, spawn points, the safe area) | Edit `Tools/level_fortresses.py`, then `./dev.sh level-fortresses` |
| Check a level layout before launching Godot (solids run together, a blocked gate or spawn point, missing files) | `./dev.sh level-audit` |
| Point this repo's user-level auto-memory at `.claude/memory/` | `python Tools/setup_memory_junction.py` (`--dry-run` first) |

## Self-test logs

| Command | Log | Lines to read |
|---|---|---|
| `./dev.sh net-test` | `_staging/net-test/host.log`, `client1.log` .. `client3.log` (one per weapon) | `[net-check]`, `[combat-check]`, `[ranged-check]`, `[turn-check]`, `[head-check]`, `[speed-check]`, `[skill-check]`, `[trail-check]`, `[flash-check]`, `[ui-check]`, `[dash-check]`, `[spin-dash-check]`, `[lunge-check]`, `[pickup-check]`, `[loot-check]`, `[map-check]`, `[bot-check]` (not asserted on: where each bot spent the session and on what), `[carry-check]`, `[guard-check]`, `[reach-check]`, and the twist fields |
| `./dev.sh pvp-test` | `_staging/pvp-test/host.log`, `client1.log` | `[pvp-check]`, `[pvp-host]`, `[combat-check]`, `[ui-check]` |
| `./dev.sh trade-test` | `_staging/trade-test/<seller>/host.log`, `client1.log` | `[trade-check]`, `[trade-host]` |
| `./dev.sh playtest` | `_staging/playtest/host.log`, `client1.log`, `client2.log` | everything net-test reads, plus `[wave-check]`, `[leak-check]`, `[down-check]` |
| `./dev.sh camera-test` | `_staging/camera-test.log` | `[camera-check]`, `[layout-check]` |

Each check prints its own expectation next to the measurement (`limit_deg_s`, `head_expected`, ...), so the log alone says why a gate failed.

## User only

`./dev.sh run`, `./dev.sh host [--port N] [--pvp]`, `./dev.sh join [address]`, `./dev.sh editor` open game windows. The `headless_guard` hook blocks them for AI sessions.

## Python scripts

| Script | Purpose |
|---|---|
| `Tools/health.py` | Backs `./dev.sh health` |
| `Tools/check_docs.py` | Backs `./dev.sh check-docs` |
| `Tools/audit_refactors.py` | Backs `./dev.sh audit-refactors` |
| `Tools/lint_agents.py` | Backs `./dev.sh lint-agents` |
| `Tools/check_godot_timeouts.py` | Backs `./dev.sh check-godot-timeouts` |
| `Tools/level_fortresses.py` | Backs `./dev.sh level-fortresses`: writes `GodotClient/config/levels/allied-town.layout.json` and `enemy-fortress.layout.json` |
| `Tools/level_audit.py` | Backs `./dev.sh level-audit`; also on the health dashboard |
| `Tools/level_common.py`, `Tools/_glb_stdlib.py` | Shared by the level tools: GLB mesh loading, piece boxes, turned-rectangle tests |
| `Tools/_check_harness.py`, `Tools/_file_scanner.py` | Shared CLI flags, JSON footer and file scanning for the checks |
| `Tools/setup_memory_junction.py` | One-time memory junction setup |
| `Tools/zoom_region.py` | Crop and enlarge a screenshot region (needs Pillow) |
