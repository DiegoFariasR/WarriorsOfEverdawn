# Registering New Tools and Subagents

Index files rot silently if you skip surfaces. `./dev.sh lint-agents` catches *broken* references but not *missing* ones — this checklist is the missing-reference backstop. Copied from Everdawn.

## New tool (`Tools/*.py` or new `./dev.sh` subcommand)

Touch:

1. `dev.sh` — `case` branch + a line in the `usage` table.
2. `.claude/tools-index.md` — row in "When to use what" + row in the matching subsection.
3. `.claude/settings.json` — only if the command needs an explicit allow rule.
4. `AGENTS.md` "Commands" table — when the command is one AI sessions should reach for.
5. `.claude/agents/*.md` and `.claude/skills/*/SKILL.md` — cross-reference from any subagent or skill whose workflow should use it.
6. `Tools/health.py` — when the tool is a drift check that belongs on the dashboard.

**Backstop:** `grep -rn <similar-existing-tool>` and tick every match — same-shape tools belong in the same surfaces.

## New Godot self-test check

A new `[*-check]` line in `NetSelfTest` (in the `NetSelfTest.<Area>.cs` part for its area) / `CameraSelfTest` needs its gate in `dev.sh` (`gate` helper, or a hand-rolled awk block), a mention in the matching command row of `AGENTS.md`, and the "Verified by" paragraph of the design doc it verifies. A check with no gate is a printed number nobody reads.

## New subagent (`.claude/agents/*.md`)

1. Add a row to `.claude/agents/README.md` decision table.
2. Add to "Common confusions" if it has a near-neighbour.
3. Add it to the `specialized subagents (...)` roster in `AGENTS.md`.
4. Run `./dev.sh lint-agents` to verify references resolve.

## Renames

Do all of the above for the new name plus a repo-wide grep for the old name (agent `.md` files, skills, `dev.sh`, `tools-index.md`, `AGENTS.md`, `settings.json`).
