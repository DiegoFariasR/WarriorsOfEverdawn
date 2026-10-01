---
name: auto-maintain
description: Unattended maintenance. Lands up to N items (refactors first, then polish wins) without manual intervention. Strictly excludes visual, gameplay-number, control and network-ownership changes. Designed to run while the user is away; produces a single review report.
argument-hint: "<budget>"
disable-model-invocation: true
user-invocable: true
---

# Auto-Maintain

Land up to N items (refactors + polish wins) before the user comes back. **Refactors first**, polish after. The orchestrator never commits. Ported from Everdawn's skill of the same name.

Composes [/refactor](../refactor/SKILL.md), [/polish](../polish/SKILL.md) and the [/close-chat](../close-chat/SKILL.md) lenses into one budget-driven sequence. The budget means "this much work landed", not "this much time spent".

## Hard exclusions (refuse if asked otherwise)

- **No gameplay-number changes.** Do not edit the values in `Core/Combat/` (damage, ranges, arcs, hit times, cooldowns, mana costs, enemy stats, waves), `Core/Locomotion/` (speeds, turn rate, dash distance and charges) or `Core/Stats/` (stat formulas). Moving a number without changing it is allowed.
- **No visual changes.** Do not edit `.tscn`, `.gdshader`, materials, environment, or `UiTheme` colour / font / size values. Routing existing literals through existing `UiTheme` helpers IS allowed, as long as the values stay the same.
- **No control or network-ownership changes.** Do not change input mappings in `project.godot`, what each camera mode does with the controls, or which machine owns a piece of state (`Docs/Design/multiplayer.md`).
- **No assets.** Do not copy, import or edit anything under `GodotClient/assets/`.
- **No new skills, subagents or tools, and no `.claude/` edits.** Claude Code blocks autonomous edits under `.claude/` with any tool. Capture proposals in the final report only.

If any phase produces a diff that violates the above, revert that diff and continue.

## Argument parsing

`<budget>` (required integer) = total items to land. Each counts as 1:
- A refactor entry (or one phase of one) drained from `Docs/Design/Refactor/pending-refactors.md`.
- A polish-engineer round with verdict `progress` (a `no-wins` round does NOT count).

A landed item is one that passed its own verification. Skipped or stale-closed refactor entries do NOT count toward the budget — those are queue maintenance, not work landed.

Reasonable values: `5` = small touch-up, `10–15` = focused session, `20+` = long unattended run.

If `$ARGUMENTS` is empty or non-integer, ask the user once for a budget and exit.

## Procedure

The flow is **budget-driven**, not phase-driven. The orchestrator tracks `items_landed` (toward budget) and `polish_no_wins_streak` (reset to 0 whenever a refactor lands).

```
Phase 0 — Baseline (once)
Phase 1 — Lens scan (once, fills the queue with current findings)
Loop while items_landed < budget:
  if pending-refactors has drainable items:
    Phase 2 — Drain (single /refactor agent, cap at remaining budget)
    polish_no_wins_streak = 0
  else if polish_no_wins_streak < 2:
    Phase 3 — Single polish-engineer round
    if verdict == progress: items_landed++, polish_no_wins_streak = 0
    else if verdict == no-wins: polish_no_wins_streak++
    else if verdict == blocked: attempt recovery, else stop
  else if !lens_rescan_attempted_since_last_landing:
    Phase 1 — Lens scan again
    lens_rescan_attempted_since_last_landing = true
    if new entries added > 0: continue loop
    else: stop early (saturated)
  else:
    stop early (saturated)
Phase 4 — Regression gate (once at end)
Phase 5 — Final report
```

### Phase 0 — Baseline (runs once)

1. `./dev.sh format` — establish a clean formatting baseline.
2. `./dev.sh test` — must return 0. If red, **abort**: write a minimal `Dev/auto-maintain-reports/auto-maintain-<timestamp>.md` noting "baseline-red, aborted" with the failure, and stop.
3. `./dev.sh smoke` — must return 0. If red, run it once more and record both results; if still red, abort the same way ("baseline smoke red", with the failing gates). Every later client change is judged against this run, so it has to start green. A gate that failed once and then passed is noted in the report under "Open issues" as possibly flaky.
4. `./dev.sh health` — capture the baseline drift snapshot to `Dev/auto-maintain-reports/auto-maintain-<timestamp>.baseline.txt` for the Phase 4 comparison.
5. `git status --porcelain` — record the files already dirty. The run must not change which files are dirty besides its own edits.

### Phase 1 — Lens scan (staging-file pattern, avoids parallel-write races)

Runs **once at start**, and again only when the queue is empty AND polish has saturated AND budget remains.

| Lens | What it looks for | Output destination |
|---|---|---|
| #5 — Refactor opportunities | Cross-cutting structural debt: files growing past one responsibility, parallel implementations (player vs enemy), duplicated tables, rules living in the client that belong in `Core`, inconsistent patterns at 3+ sites | Append to `pending-refactors.md` as `ready` (or with phases) |
| #4 — Tool improvements | Existing `Tools/*.py` / `./dev.sh` commands and self-test gates with awkward output, missing flags, slow paths, confusing defaults, gates that could pass on a missing value | Append marked `**Type:** Tool improvement` |
| #7 — Subagent and skill improvements | Definition-of-done bullets that proved insufficient, stale procedural advice in `.claude/agents/*.md` and `.claude/skills/*/SKILL.md` | Append marked `**Type:** Subagent improvement`, Status `scheduled-human-session` (landing needs the user to direct a `.claude/` edit) |

**Explicitly excluded autonomous lenses** (need human judgment or session context):
- #1 Memory, #2 Skill candidates, #3 Tool candidates, #6 Subagent candidates → the report's "Proposals" section; never auto-saved.
- #8 Revert candidates and #9 Open topics → need session history, which an unattended run does not have.

**Procedure:**

1. Spawn three `general-purpose` subagents in parallel (one message, three Agent calls). Each writes to its own staging file — parallel writes to `pending-refactors.md` would race:
   - Lens #5 → `Dev/lens-5-findings.md`. "Scan `Core/`, `Core.Tests/` and `GodotClient/scripts/` for cross-cutting refactor opportunities. Cite `file:line`. Only work too large for one polish round. Heading `## [LENS-5] <title>` + `**Why.** / **Proposal.** / **Risk.**` + `---`. Structural proposals cite `Docs/Design/architecture-growth.md`. **Overwrite, don't append.** Validate every `./dev.sh` command name against `./dev.sh help`."
   - Lens #4 → `Dev/lens-4-findings.md`. Same shape, over `dev.sh` and `Tools/`.
   - Lens #7 → `Dev/lens-7-findings.md`. Same shape, over `.claude/agents/` and `.claude/skills/`.
2. After all three return, read each staging file and keep a finding only if it:
   - cites at least one real `file:line` (grep to confirm);
   - does not duplicate an open entry in `pending-refactors.md` or work landed earlier in THIS run;
   - does not propose excluded work (numbers, visuals, controls, network ownership, assets).
3. If more than 8 findings survive, drop the weakest half (vague rationale, missing `file:line`, single-site smells).
4. Merge the survivors into `pending-refactors.md` in one `Edit`, below the "Open entries" marker, each with a `**Status:**` line. Use the next ID after the highest ever assigned — **never reuse an ID, even one drained earlier in the same run** (check `git log -p` on the file when the queue is empty).
5. Delete the staging files.
6. Record `N new entries added` for the report.

### Phase 2 — Drain pending-refactors

**Build the candidate list first** from each entry's `**Status:**` (see the file's "Status field convention"):

- `ready` → candidate.
- `needs-clarification` → excluded. Collect its `**Blocker:**` for the report's "Open blockers".
- `blocked-by-#NN` → excluded until the prerequisite lands.
- `scheduled-human-session` → excluded; listed in the report.
- **Entry with a `## Phases` block** → the candidate is its FIRST phase marked `ready`. When a phase lands, the agent removes it from the list and the next `ready` phase becomes the next candidate.

If the list is empty, skip to Phase 3.

**Spawn the drain agent.** One `general-purpose` subagent following `/refactor`'s multi-iteration mode with `N = min(remaining_budget, len(ready_candidates))`. Pass it the candidate list verbatim, this skill's hard exclusions, and the `.claude/` rule. It picks smallest/safest first.

Each landed item (or phase) increments `items_landed`; stale-closed entries don't. Afterwards reset `polish_no_wins_streak = 0` and `lens_rescan_attempted_since_last_landing = false`.

**Decline rule (NON-NEGOTIABLE).** If the agent fails to land a candidate (a verification regression that reverts cleanly, or scope blowing past one budget unit), it MUST, before returning, do ONE of:

1. **Propose a phase split**: add a `## Phases` block with concrete `ready` sub-steps and attempt the first phase in its next iteration.
2. **Reclassify with a concrete blocker**: set `**Status:** needs-clarification` and add a `**Blocker:**` question a human can answer in 1-3 sentences (e.g. `**Blocker:** Splitting NetSelfTest moved the dash check after the spin check, and dash-check now sees 0 refused dashes in 2 of 3 runs. Is the check order meaningful, or should the gate change?`). "Needs review" and "too risky" are not blockers.

At most one phase split per candidate attempt, then land or reclassify.

### Phase 3 — Single polish-engineer round

Spawn one `polish-engineer` for ONE round (area `all`), passing the prior-rounds summary so it skips what earlier rounds did, plus this skill's hard exclusions.

- `progress` → `items_landed++`, `polish_no_wins_streak = 0`.
- `no-wins` → `polish_no_wins_streak++`.
- `blocked` → attempt recovery (inspect `git diff`, revert the round's changes, re-run `./dev.sh test`). If still red, stop early with `polish-blocked`.

### Phase 4 — Regression gate (runs ONCE at end)

1. `./dev.sh test` — must be green.
2. `./dev.sh smoke` — required when the run's diff touches client code or anything the self-tests read. Skip it only when the gate below prints nothing, and record `smoke: SKIPPED (no triggering paths in diff)`.

   ```sh
   git status --porcelain | awk '{print $2}' | grep -E '^(GodotClient/(scripts|scenes)/|GodotClient/project\.godot$|Core/|dev\.sh$)' >/dev/null && echo "SMOKE_REQUIRED"
   ```

   `Core/` is on the list because the self-tests measure Core's numbers live (reach, turn limit, speeds). If smoke fails, revert the run's Phase 3 polish changes first (identified from `git diff`) and re-run; Phase 2 refactors came through `/refactor`'s own verification and are not auto-reverted. If it still fails, report `early-failure` with the failing gates.
3. `./dev.sh health` — compare with the baseline file; new drift is reported, not blocking.

**Headless rule (NON-NEGOTIABLE).** The self-tests pass `--headless` and a timeout themselves. Never run `./dev.sh run`, `editor`, `host` or `join`, and never launch the Godot executable yourself; the `.claude/hooks/headless_guard.py` hook blocks them anyway. This run makes no visual changes, so it has no use for `./dev.sh screenshot`: bot-driven captures differ from run to run and make no regression gate.

### Phase 5 — Final report (always runs, even on early exit)

Write `Dev/auto-maintain-reports/auto-maintain-<timestamp>.md`:

```
# Auto-Maintain Report — <ISO timestamp>

Budget requested: <N>
Items landed: <M>
Termination: <budget-reached | early-saturated | early-failure | interrupted>

## Items landed (sequential)
- [refactor] #<id> — <one-line summary>
- [polish] <one-line summary of the round's wins>

## Pending-refactors added (this run)
- #<new-id> — <one-line summary> [type: refactor | tool | subagent]

## Lens-scan history
- Round 1 (start): N entries added

## Polish summary
- Polish rounds run / progress rounds / no-wins streak at the end / tests added

## Regression status (end of run)
- test: PASS/FAIL
- smoke: PASS/FAIL/SKIPPED (with reason and failing gates)
- health: <baseline drift> -> <current drift>

## Proposals (human review required — NOT auto-saved)
- Memory / skill / tool / subagent candidates, and any proposed `.claude/` edit with its exact text

## Open blockers (needs-clarification entries you can answer to unblock)
- **#NN** (phase X) — <verbatim Blocker question>

## Needs a human session
- **#NN** — <what the scheduled-human-session entry needs>

## Open issues
- <anything reverted, blocked, or surfaced for attention>

## Files touched (uncommitted)
<git status --porcelain output, minus the files already dirty at baseline>
```

Print the report path so the user can open it straight away.

## Stopping rules (hard limits, override everything)

- **Budget reached** — `items_landed >= budget`. Run Phase 4, write the report.
- **Saturated** — queue empty AND polish reported 2 consecutive no-wins AND the latest lens scan added 0 entries. Report `early-saturated`.
- **Non-recoverable failure** — tests or smoke red after the revert attempt. Report `early-failure`.
- **User interrupt** — jump to Phase 4 + Phase 5 with whatever state exists. Report `interrupted`.
- **Never commit.** Save to disk, report, stop.
- **Never edit what the hard exclusions forbid.** If a refactor entry instructs it, drop the entry with a one-line report note.

## Token economy

Each `/refactor` pass and each polish round spawns its own subagent with fresh context. The orchestrator's footprint per iteration is: read `pending-refactors.md`, dispatch, read the agent's report, update counters, log. Don't read source files in the orchestrator; if its accumulated reads pass ~30 KB, it is doing work the subagents should own.

## Notes

- **Refactors first is intentional.** Queue entries are curated and reviewed; polish is opportunistic.
- `pending-refactors.md` is the synchronization point: Phase 1 appends, Phase 2 drains. Keep its entry format (`**Status:**`, `**Why.** / **Proposal.** / **Risk.**`).
- **Safe to interrupt at any point.** State lives in the filesystem (`pending-refactors.md`, the report, the working tree). Resume = re-run with the same or a smaller budget.
- Polish saturation resets when a refactor lands: an extracted helper usually leaves call sites and tests to tidy.
- Reports and lens staging files live in `Dev/`, which is gitignored.
