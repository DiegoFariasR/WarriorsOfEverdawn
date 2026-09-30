---
name: polish
description: Autonomous C# improvement loop -- dedupe, survey, plan, implement, test, repeat N rounds.
argument-hint: "<rounds> [area]"
user-invocable: true
---

# Polish

Orchestrate the N-round C# improvement loop **in the main session**. Each round delegates to a fresh `polish-engineer` subagent that does one round of work and reports back. The main session decides whether to continue and surfaces per-round headlines. Ported from Everdawn.

Why per-round subagents instead of one big subagent: each round starts with clean context, the engineer spends its whole budget on one focused pass, and the main session can stop early on `no-wins` / `blocked`.

## What to do when invoked

1. Parse `$ARGUMENTS` as `<rounds> [area]`:
   - `rounds` (required): integer.
   - `area` (optional, defaults to `all`): `core`, `scripts`, `dedup`, or `all`.

2. For each round `R` from 1 to N:
   a. Spawn a fresh `polish-engineer` subagent. Pass:
      - Round number `R` of `N` and the area.
      - A short paragraph summarising what prior rounds changed (file/symbol level, no diffs). Skip on round 1.
      - "Run exactly ONE round per the process in your agent definition; do not loop."
      - Required output: per-item summary + verdict — `progress`, `no-wins` or `blocked`.
   b. Surface the round's headline to the user: one line per item changed, the verdict, and the build / test / smoke results the engineer reported.
   c. Stop early if:
      - The verdict is `no-wins` two rounds in a row.
      - The verdict is `blocked` and the engineer couldn't recover within its round.
      - The user interrupted.

3. After the loop, print a combined summary: rounds run, items per round, tests added, skipped/blocked rounds.

## Scope reminder

`polish-engineer` improves existing C# in `Core/`, `Core.Tests/` and `GodotClient/scripts/` without changing behaviour. New features and rule changes go to `core-engineer` / `godot-engineer`; scenes, shaders and assets are out of scope.
