# Agent routing

Quick decision table for delegating work. When the main agent sees a task, match it here first before doing the work directly. Each row's trigger is mutually exclusive with the others by design — if two seem to fit, re-read the negative-scope notes to pick. Same shape as Everdawn's roster, sized to this project's two code domains.

## Decision table

| Task shape | Agent | Tools | Notes |
|---|---|---|---|
| Non-trivial C# under `Core/` + its tests in `Core.Tests/` (combat rules, skills, enemies, locomotion, dodge, stats, AI decisions) | `core-engineer` | Read, Write, Edit, Bash | **Opus.** Writes the failing test before the rule. Gate: `./dev.sh test`. |
| C# / `.tscn` work under `GodotClient/` (player, enemies, animation layers, camera, HUD, networking, self-tests) | `godot-engineer` | Read, Write, Edit, Bash | Sonnet. Gate: build + the self-test covering the change (`./dev.sh smoke` runs all three). |
| One round of C# improvement work (dedup + survey + implement + test) | `polish-engineer` | Read, Write, Edit, Bash | Sonnet. Invoked once per round by `/polish <rounds> [area]` and by `/auto-maintain`; the skill orchestrates the loop in the main session. |

## Common confusions — pick the right neighbour

- **"Spin should cost more mana / Slice should reach further"** → `core-engineer`. Numbers live in `Core/Combat/`; a range change also needs the `reach-check` gate to stay green, which `core-engineer` hands to `godot-engineer` if the measured reach moves.
- **"The legs slide / the chest drifts off the aim"** → `godot-engineer`. Animation layers and the torso twist live in the client; `Core` only picks the leg direction.
- **"Add a dodge-like mechanic"** → both, in order: `core-engineer` for the rule (charges, distance) with tests, then `godot-engineer` for movement, animation, network and its self-test check.
- **"Polish the codebase"** → `polish-engineer`. It covers both `Core` and `GodotClient/scripts/`, but only improves existing code; new behaviour goes to the two engineers.

## Negative routing

These do NOT need a subagent — handle in the main agent:

- One-line fixes (typos, single-call rename, obvious missing null check).
- Pure exploration ("where is X defined?") — use `Grep`/`Glob` directly, or spawn an `Explore` agent if multi-file.
- Planning ("what should we do about Y?") — answer directly.
- Reading docs / answering questions.
- Anything the user is interactively driving step-by-step.

## When to use Opus

- `core-engineer` tasks (rules every other layer builds on).
- Architecture decisions that span `Core` and `GodotClient`, or the network authority model.
- Major refactors with non-obvious migration paths.

Everything else: Sonnet. The agent definitions pin the right model.

## When to NOT delegate

- If you've already loaded the context, doing the work directly is cheaper than spawning a subagent and re-loading it there.
- If the task is so small the agent's setup overhead exceeds the work.
- If the user is asking exploratory questions — answer first, delegate only when implementing.

## Adding a new agent

1. Drop a `.md` under `.claude/agents/` with frontmatter: `name`, `description` (trigger-first, with explicit Do NOT scope), `tools`, `model`.
2. Add a row to the decision table above.
3. Add to the "Common confusions" list if it has a near-neighbour.
4. Add it to the `specialized subagents (...)` roster in the root `AGENTS.md`.
5. Run `./dev.sh lint-agents`.

The description field is the only thing the main agent reads to decide routing. Make it sharp.
