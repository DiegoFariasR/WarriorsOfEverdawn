---
name: smoke
description: Run the headless Godot self-tests (camera-test, net-test, pvp-test) and report each gate. Use after GodotClient or Core changes that could affect play.
argument-hint: "[camera-test | net-test | pvp-test]"
disable-model-invocation: false
user-invocable: true
---

# Godot Smoke Tests

Wraps `./dev.sh smoke`, which builds the client and runs the three headless self-tests in turn: `camera-test` (camera modes, controls mapping, HUD layout), `net-test` (host + 2 bot clients: movement, combat, animation, VFX, HUD, dodge) and `pvp-test` (PvP host + 1 bot client). Each prints its `[*-check]` lines and fails on any gate. The Everdawn counterpart is `/smoke` over `visual-smoke`.

## Argument

- empty: run all three (`./dev.sh smoke`).
- `camera-test`, `net-test` or `pvp-test`: run just that one.

## Procedure

1. **Run** the command. It takes about a minute for all three; the Godot processes are headless and time-limited, so nothing opens on screen.
2. **Report** each test's result and quote every `FAIL` line verbatim, with the log path it names (`_staging/net-test/`, `_staging/pvp-test/`, `_staging/camera-test.log`).
3. **On failure**, read the failing log around the failing `[*-check]` line and state what the numbers say (e.g. `max_turn_deg_s=612 limit_deg_s=540`). Do not auto-fix — hand back to the user. A `NOTE ... known Godot disconnect error` line is not a failure (godot#86814).
4. If a gate fails once and passes on a re-run, say so explicitly; don't report a flaky pass as a clean one.

## Rules

- Never commit anything after running. Report-only.
- Never substitute `./dev.sh run/host/join` to "see it": those open windows and are blocked for AI sessions.
- A green smoke run says the measured things are right. What only shows on screen (how an effect looks, how motion reads) stays the user's to check.
