# Pending Refactors

Tracker for refactors that deserve a dedicated pass — too large for a single polish round. Each item is an independent task — pick one per change. `/refactor` drains it; `/auto-maintain` fills it (lens scan) and drains it unattended.

Items are numbered chronologically as they're noticed. Landed items are removed but numbers are not reused, so gaps in the sequence are expected — see `git log -- Docs/Design/Refactor/pending-refactors.md` for landing history.

## Entry format

```
## NN. Title

**Status:** ready

**Why.** What is wrong today, citing `path/File.cs:line` for every site.

**Proposal.** The change. A structural shape (table, registry, partial-class split) cites `[[../architecture-growth#N]]`.

**Risk.** What could break and which check catches it.
```

`./dev.sh audit-refactors` checks every backticked path, symbol and `./dev.sh` command in open entries still resolves. Put `<!-- docs-lint-skip -->` on the line above a reference that is meant not to exist yet.

## Status field convention

Every entry has a `**Status:**` line that tells `/auto-maintain` how to handle it. Four values:

| Status | Meaning | `/auto-maintain` behavior |
|---|---|---|
| `ready` | Verification fits in one auto-maintain budget unit (`./dev.sh test` + at most `./dev.sh smoke`). Safe for autonomous landing with revert-on-failure. | Included in drain candidates. The `/refactor` agent attempts it. |
| `needs-clarification` | Author or prior agent attempt has identified a specific blocker that a human needs to answer. The entry MUST include a `**Blocker:**` line stating the exact question. Once answered (status flipped to `ready` with the answer integrated), the agent can land it. | **Excluded from autonomous drain.** Surfaced in `/auto-maintain` reports under "Open blockers" so the user can answer them. |
| `blocked-by-#NN` | Has a hard prerequisite on another open entry. No human question — just waiting for the prereq to land. | Excluded until prereq lands. |
| `scheduled-human-session` | Decided, with no open question, but landing needs a person at the keyboard (visual or feel judgement in a real game window). The status note says what the session does. | Excluded from autonomous drain. Picked up by a human-led session. |

`ready` is the default if the field is omitted — but new entries should set it explicitly.

### `**Blocker:**` field (required on `needs-clarification`)

The blocker MUST be a concrete question with enough context that a human can answer in 1-3 sentences. Bad blockers ("needs review", "too risky"); good blockers ("which of options (a)/(b)/(c) below should we pick?", "approve landing if `./dev.sh smoke` passes, even without playing it?", "what's the acceptable drift for the chest-on-aim gate?").

If the blocker is "this needs eyeball verification in a game window and there's no way to automate it" — that's a valid blocker too, but rephrase it as a question: "Approve the agent landing this and you'll check it in game afterwards (with revert option)?" Now it's a yes/no. Headless runs draw nothing, so anything whose correctness is only visible on screen falls here.

## Phases convention (split before parking)

If an entry would be `needs-clarification` because of size or verification cost BUT can be broken into independently-landable steps, list them under `## Phases` with per-phase Status. The entry-level Status reflects the whole; each phase has its own. `/auto-maintain` drains the first phase marked `ready`; when it lands, the agent removes that phase from the list and the next becomes current.

```
## NN. Title

**Status:** needs-clarification (500-line file split; landing phase-by-phase avoids the in-game check on the final phase)

**Blocker:** Approve the agent landing phase 3 if `./dev.sh smoke` passes, even without playing it? (Phases 1-2 are unconditionally ready; phase 3 deletes the most code.)

**Why.** ...

**Proposal.** ...

## Phases

<!-- docs-lint-skip -->
1. **ready** — Extract `FooHelper.cs` and migrate the 3 simplest call sites. Verify: `./dev.sh test`.
2. **ready** — Migrate the remaining call sites. Verify: `./dev.sh test` + `./dev.sh smoke`.
3. **needs-clarification** (see entry-level blocker) — Delete the old helper; collapse the dispatcher.
```

Rule of thumb: a `needs-clarification` entry with NO `## Phases` block is a single-question parked item. A `needs-clarification` entry WITH phases is partially drainable — `/auto-maintain` lands the `ready` phases and surfaces the blocker for the held phase.

When the `/refactor` agent can't land a `ready` item (verification regression that doesn't revert cleanly, or scope blows past one budget unit), it MUST either (a) propose a phase split inline in the entry and continue, or (b) reclassify the entry as `needs-clarification` with a concrete `**Blocker:**` question that a human can answer in 1-3 sentences. Silent skipping is not allowed; vague "needs review" reclassifications are not allowed.

<!-- Open entries go below this line, numbered from 1. -->

## 11. Agent and skill docs predate the eight self-tests and the newer Core folders

**Type:** Subagent improvement

**Status:** scheduled-human-session (the edits are under `.claude/`, which only the user can direct; the exact replacement text is in the 2026-10-03 auto-maintain report)

**Why.** Written before magic, trade, wall, parts and floors tests and the kit landed:
- `.claude/agents/godot-engineer.md:30` and `:47` route client changes to three self-tests ("`./dev.sh smoke` runs all three"); `:28` and `.claude/skills/close-chat/SKILL.md:35` say checks go only in `NetSelfTest` / `CameraSelfTest`; `:16` leaves out the design docs holding the newer tests' "Verified by" paragraphs; `.claude/agents/README.md:10` says "runs all three".
- `.claude/skills/smoke/SKILL.md` (`:3`, `:4`, `:11`, `:15`, `:16`, `:20`, `:21`, `:28`) describes three tests, two clients and a one-minute run; `.claude/tools-index.md` has no floors-test rows and wrong client counts (`:13`, `:44`, `:47`, `:48`).
- `.claude/skills/auto-maintain/SKILL.md:17` and `.claude/agents/core-engineer.md:15`, `:46` do not guard the numbers in `Core/Trade/`, `Core/Loot/` and `Core/Level/`, which trade-test and floors-test read live; `:20` says nothing of the `GodotClient/kit/` submodule; `core-engineer.md:46` still names head scale, now the kit's `HeadSizing`.
- `.claude/agents/polish-engineer.md:97`, `:110`, `:120` run smoke only for client changes, though `/refactor` and `/auto-maintain` also run it for `Core/` changes, whose constants only the live gates check.

**Proposal.** Apply the replacement text from the report in one session, then `./dev.sh lint-agents`.

**Risk.** Lists of self-tests in prose go stale again when a ninth lands.

## 12. /commit insists on `main` and says the repository has no remote

**Type:** Subagent improvement

**Status:** needs-clarification

**Blocker:** Should `/commit` commit on the current branch (as this repository works now, on topic branches pushed to `origin`), and should it push, or stay main-only and local? It also needs to stop on uncommitted edits inside the `GodotClient/kit` submodule; once answered, the `.claude/` edit is yours to direct.

**Why.** `.claude/skills/commit/SKILL.md:3`, `:11`, `:22` say the repository is local with no remote, and `:30` aborts unless on `main`. The repository has `origin` on GitHub, works on `combat-weapons-guard-dash` (all commits after the first), so `/commit` aborts every time; its `git add -A` (`:40`) also leaves out edits made inside the kit submodule without saying so.

**Proposal.** The report carries replacement text for the conservative reading (commit on the current branch, never push, abort on kit edits).

**Risk.** Committing on the wrong branch, if the branch check goes.
