---
name: refactor
description: Pick one or more items from Docs/Design/Refactor/pending-refactors.md, implement end-to-end without waiting for confirmation, verify, and remove the entry.
argument-hint: "[N] count or [--item ID] to target a specific entry; empty = run once auto-picking"
disable-model-invocation: true
user-invocable: true
---

# Refactor

Pull pending-refactor items, work each one end-to-end, and remove them from the tracker. Ported from Everdawn.

## Argument parsing

Parse `$ARGUMENTS` first:

| Form | Meaning |
|---|---|
| empty | One iteration; auto-pick an item. |
| `--item ID` (e.g. `--item 3`) | One iteration; pick item `#ID` directly. |
| bare integer `N` (e.g. `3`) | Repeat the full procedure `N` times, auto-picking a fresh item each iteration. |
| `N --item ID` | Reject as ambiguous; ask whether the user wants count or a specific item. |

A bare integer is **always a count**. To pick item 3 say `/refactor --item 3` -- never `/refactor 3`.

## Procedure (one iteration)

1. **Read** `Docs/Design/Refactor/pending-refactors.md`.

2. **List** open items concisely: `#id — one-line summary — status`. Only `ready` items (or an entry's first `ready` phase) are candidates.

3. **Pick** and announce, then proceed — do not wait for confirmation:
   - If `--item ID` was supplied, use that one. Confirm it exists; if it is not `ready`, say so and stop.
   - Otherwise pick based on scope/risk/dependencies (smaller + self-contained beats larger + cross-cutting unless the user has stated a target). State the pick in 1-2 sentences with the tradeoff vs the alternatives, then move to step 4.

4. **Verify scope** before implementing. Refactor entries rot. Grep the claimed file paths and match counts; read the current code at each cited location end-to-end. If grep no longer finds what the entry claims, or the code already does what the proposal asks for, **narrow the proposal or drop the entry with a note** rather than forcing a stale plan. "Claimed 3 sites, only 1 real match, so extracting is over-engineering" is a successful outcome.

5. **Implement** the chosen refactor:
   - **Derive details from the call-site code, not the entry's prose.** The `**Proposal.**` may be outdated or speculative; the code at the call site is the source of truth.
   - Surface every file changed as you go so the user can intervene.
   - One refactor per iteration; don't bundle unrelated cleanup.
   - Don't add comments explaining the refactor, the previous behaviour or "the recent change". That's commit-message content.
   - **No behaviour changes — ever.** A refactor changes structure, not what the game does: no gameplay numbers, controls, network ownership or visuals, even if the entry's proposal calls for it. Do the structural part and leave the rest for the user to request. An entry that is entirely a behaviour change is not a refactor: skip it and pick another.

6. **Verify**:
   - `./dev.sh format`
   - `./dev.sh build`
   - `./dev.sh test`
   - `./dev.sh smoke` if anything under `GodotClient/`, `Core/` or `dev.sh` changed (the self-tests measure Core's numbers live)
   - `./dev.sh playtest` if the change touched `Enemy/EnemyDirector.cs`, death and removal in `Enemy/EnemyCharacter.cs`, `Player/PlayerVitals.cs`, down and revive in `Player/PlayerCharacter.cs`, `Util/FloatingText.cs`, or anything that adds nodes while playing
   - Any extra check the entry names.

   On a failure you can't fix within the entry's scope, revert the iteration's changes and follow the decline rule below.

7. **Close out**:
   - **Delete** the entry (or the landed phase) from `pending-refactors.md`. Landed items are removed; git history keeps them. IDs are never reused.
   - Update docs the refactor affected (`Docs/Design/*.md`, `AGENTS.md`, the per-scope `AGENTS.md`).
   - **Audit-only closures** (resolved by analysis, no code changed): leave the non-obvious finding at the code site as a short comment so nobody re-runs the audit. This is the one exception to step 5's no-comments rule.
   - A follow-up the work uncovered becomes a **new** numbered entry — don't edit the closed one back.

8. **Report**: every file touched, what changed, and the format/build/test/smoke results. **Never commit.**

## Decline rule

If a `ready` item can't be landed (a regression that reverts cleanly, or scope past one pass), do ONE of these before moving on — silent skipping is not allowed:

1. Add a `## Phases` block with concrete `ready` sub-steps and attempt the first.
2. Set `**Status:** needs-clarification` with a `**Blocker:**` question a human can answer in 1-3 sentences.

## Multi-iteration mode (count > 1)

Repeat steps 1-8 `N` times. Each iteration re-reads the tracker, picks a fresh item (no pre-planned sequence), and runs straight through to its report. Stop early when no `ready` candidate is left or an iteration's verification fails and can't be reverted cleanly. Finish with one combined report: `#id — landed | narrowed | dropped | declined (phase split / blocker)`.

## Rules

- **One refactor in flight at a time.**
- **Stop and renegotiate** if the entry turns out bigger or more entangled than stated. Don't balloon silently.
- **Never commit.** The user runs `/commit` themselves.
- **Headless only.** The self-tests launch Godot headless themselves; never run `./dev.sh run/editor/host/join`.

## When writing or extending a pending-refactor entry

- **Cite the trajectory.** A refactor that proposes a structural shape (registry, table-driven lookup, partial-class split, helper extract) cites `[[../architecture-growth#N]]` for the system it touches, with one sentence restating the trajectory in the entry. If no entry covers the system, add one (or flag it) before proposing the shape.
- Housekeeping (a stale path, a rename, dead code) needs no trajectory citation.
- **When the trajectory contradicts the entry's original proposal**, override it in a `**Chosen direction.**` paragraph naming the trajectory entry. Don't silently land the original.
