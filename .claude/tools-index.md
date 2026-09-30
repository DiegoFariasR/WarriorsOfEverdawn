# Tools Index

Quick lookup for `./dev.sh` subcommands and `Tools/*.py` scripts. Subagents reference this so they don't have to grep around. Source of truth is `./dev.sh help` -- keep this in sync when commands change (`./dev.sh lint-agents` flags commands that no longer exist).

## When to use what

| Want to... | Use |
|---|---|
| Build everything | `./dev.sh build` |
| Run the Core tests | `./dev.sh test` |
| Format the C# | `./dev.sh format` (`--verify-no-changes` to only check) |
| Import copied-in assets | `./dev.sh import` |
| Check a client change end to end (movement, combat, animation, VFX, HUD, dodge, across 3 peers) | `./dev.sh net-test` |
| Check player-on-player hits and damage | `./dev.sh pvp-test` |
| Check camera modes, controls mapping and HUD layout | `./dev.sh camera-test` |
| Run all three self-tests (the client regression gate) | `./dev.sh smoke` |
| Project-wide drift sweep | `./dev.sh health` (`--only <check>`, `--skip <check>`, `--list`) |
| Find broken links, `file:line` refs and `./dev.sh` commands in the docs | `./dev.sh check-docs` (`--fix` re-points moved files) |
| Check the refactor queue's references still resolve | `./dev.sh audit-refactors` (`--id N`, `--match <text>`, `--by-status`) |
| Check agents, skills and this index reference real things | `./dev.sh lint-agents` |
| Catch unwrapped headless Godot launches in `dev.sh` | `./dev.sh check-godot-timeouts` |
| Point this repo's user-level auto-memory at `.claude/memory/` | `python Tools/setup_memory_junction.py` (`--dry-run` first) |

## Self-test logs

| Command | Log | Lines to read |
|---|---|---|
| `./dev.sh net-test` | `_staging/net-test/host.log`, `client1.log`, `client2.log` | `[net-check]`, `[combat-check]`, `[turn-check]`, `[head-check]`, `[speed-check]`, `[skill-check]`, `[trail-check]`, `[flash-check]`, `[ui-check]`, `[dodge-check]`, `[reach-check]`, and the twist fields |
| `./dev.sh pvp-test` | `_staging/pvp-test/host.log`, `client1.log` | `[pvp-check]`, `[pvp-host]`, `[combat-check]`, `[ui-check]` |
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
| `Tools/_check_harness.py`, `Tools/_file_scanner.py` | Shared CLI flags, JSON footer and file scanning for the checks |
| `Tools/setup_memory_junction.py` | One-time memory junction setup |
