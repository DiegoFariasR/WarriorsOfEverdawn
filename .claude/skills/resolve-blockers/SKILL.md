---
name: resolve-blockers
description: Interactive walk-through of the `needs-clarification` entries in `Docs/Design/Refactor/pending-refactors.md`. Groups blockers by decision shape, batches recommendations per `AskUserQuestion` call, applies edits per pick. Natural follow-up to `/auto-maintain` runs, which surface new blockers and reclassifications.
argument-hint: "(no args)"
disable-model-invocation: true
user-invocable: true
---

# Resolve Blockers

Walk through the `needs-clarification` entries in `Docs/Design/Refactor/pending-refactors.md` and resolve their blockers. Each blocker holds a question that needs a human answer before the entry can become `ready` for autonomous drain. This skill batches them by decision shape so an unattended user-session can knock through 20+ in one sitting.

## When to invoke

- After `/auto-maintain` finishes and reports new `needs-clarification` entries (the auto-maintain run's "Open blockers" section lists them).
- When the queue has accumulated blockers and you want to drain them in one sitting.
- When `./dev.sh health` shows pending-refactor entries with stale refs and you suspect stale = needs-clarification.

Not a fit:
- Single-blocker resolution (just edit the entry directly).
- Bug investigation or gameplay tuning (different work).

## Procedure

1. **Inventory.** Read `Docs/Design/Refactor/pending-refactors.md`. Group every `needs-clarification` entry by decision shape:

   | Group | Shape | Example |
   |---|---|---|
   | **A — Approvals** | Yes/no / pick-one-of-two. Agent approval, mode pick, "accept or close." | accept the extra indirection OR close as not worth it? |
   | **B — Design picks** | Pick-one-of-three (or four). Several legitimate paths; the call shapes future work. | one file per self-test check / partial class / leave as is? |
   | **C — Status checks** | Verification questions. The agent does the lookup; the user confirms the implication. | is the gate this entry wants to change still flaky? |
   | **D — Human-led queue** | "Schedule for session OR close as not worth it." No further design clarification needed; just a scheduling call. | design locked; needs an in-game look after landing. |

2. **Present the groups.** One `AskUserQuestion` listing the four groups with counts. Let the user pick which to start with (or "all" / "stop here"). Recommend Group A first (fastest wins) or Group C (fastest verification).

3. **Per group, batch 3-4 entries per `AskUserQuestion`.** Each question carries:
   - The blocker text (paraphrased to keep length under the AskUserQuestion description budget).
   - 2-4 options labelled with concrete consequences.
   - **A recommendation** in the first option's label ("(Recommended)"), based on:
     - Project conventions (`AGENTS.md`, `Core/AGENTS.md`, `GodotClient/AGENTS.md`).
     - Trade-offs visible in the entry (file size, risk, dependency chain).
     - The entry's own `**Decision (date)**` history if it has one.
   - For Group C (status checks): do the lookup FIRST (`grep`, `git log`, `./dev.sh test`, `./dev.sh smoke`, etc.), then ask the user only after the answer is known.

4. **Apply edits.** Per user pick:
   - **Flip to `ready`**: replace the `**Status:**` line and remove the `**Blocker:**` line (or replace with a `**Decision (YYYY-MM-DD).**` paragraph capturing the rationale).
   - **Phase-split into `ready` sub-phases**: add a `### Phases` block to the entry; mark sub-phases `ready` in sequential order.
   - **`blocked-by-#NN`**: set Status to `blocked-by-#NN` with a one-line note explaining the dependency.
   - **Close** (not actually a blocker, false alarm, or no-longer-needed): remove the entry entirely; the file's header convention says landed/closed items are removed but IDs are not reused.
   - **Stays parked with new trigger**: keep `needs-clarification` but update the Blocker text to be a *concrete trigger* (e.g. "flip to ready when the second weapon is designed") instead of an open question.

5. **Entries whose resolution edits `.claude/`.** Only with the user's explicit go-ahead in this session: the user's pick counts as that direction for the entry it answers.

6. **After each batch.** Optional: run `./dev.sh test` once at the end to confirm the doc-only edits didn't break anything. Tests are unlikely to be affected (edits are all in `pending-refactors.md`), but the cheap sanity-check is worth it.

7. **Final tally.** Print final queue state: `N needs-clarification`, `M ready` (queue-level), `K blocked-by`, total entries. Note phase-level ready phases unlocked this session (the work that will surface next time `/auto-maintain` runs).

## Recommendation framework

When forming the "(Recommended)" pick, default to:

- **Smallest reversible step** over a big-bang change. If a refactor can phase-split into N sub-steps, prefer that over one human-led session.
- **Defer rather than guess** when the decision depends on something that doesn't exist yet (e.g. a second weapon, per-part enemy looks).
- **`blocked-by-#NN` rather than re-deciding** when an entry's resolution depends on another entry's prereq landing.
- **Close rather than park** when the entry's premise turns out to be wrong (e.g. the duplication was only surface-deep; a tool drift never surfaced).
- **Preserve project rules.** `/auto-maintain`'s exclusions (no gameplay numbers, visuals, controls, network ownership, assets, `.claude/` edits or new tools) mean any blocker whose resolution lands inside those exclusions becomes `scheduled-human-session`, not `ready`.

## Stopping rules

- User picks "stop here" → tally what's done, save report.
- All 4 groups walked → tally.
- A blocker requires significant investigation (>10 minutes of reading) before recommending → flag it, skip, move on. Note in final report so user can revisit.

## Rules

- Never commit. Doc edits land in the working tree; user runs `/commit` if they want to land them.
- Never auto-revert an entry without user confirmation (closing an entry is a destructive action).
- Never invent a decision the entry didn't already explore. If the entry's options don't fit the user's intent, propose a new option labelled `(Other approach)` instead of forcing one of the listed ones.
- Match the entry's existing voice. The file is a sustained document — new `**Decision**` blocks should read the same as old ones.
- After every edit batch, the working tree must still be format-clean. Doc edits don't need it, but if a session strays into code edits, run `./dev.sh format`.
