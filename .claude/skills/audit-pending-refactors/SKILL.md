---
name: audit-pending-refactors
description: Semantic re-verification of every open entry in Docs/Design/Refactor/pending-refactors.md. Greps cited file:line, reads the code, confirms the entry's "Why" still matches reality. Flags stale entries, landed-but-not-removed entries, and entries whose rationale was backwards.
argument-hint: "[--item ID] (default: audit all open items)"
disable-model-invocation: true
user-invocable: true
---

# Audit Pending Refactors

Walks every open entry in [`Docs/Design/Refactor/pending-refactors.md`](../../../Docs/Design/Refactor/pending-refactors.md) and re-verifies it against the current codebase. Catches stale entries, already-landed entries, and entries whose **rationale is wrong** (not just out of date) — the kind of bug only a drain attempt would normally surface, but at zero risk because we don't edit anything.

This is the safety net for `/refactor` and `/auto-maintain`'s Phase 1/3. The `./dev.sh audit-refactors` mechanical check catches stale **file paths**; this skill catches stale **claims**.

## Argument parsing

- empty → audit every open entry.
- `--item ID` → audit just that one entry.

## Procedure

1. **Read** `Docs/Design/Refactor/pending-refactors.md`. Extract every `## N. ...` open entry (skip the `## Not included` section).

2. **Per entry**, classify:

   | Status | Meaning | Action |
   |---|---|---|
   | `fresh` | Cited `file:line` exists, code matches the entry's description, helper/interface not yet built. | Leave entry alone. |
   | `landed` | The proposed extraction/helper/refactor already exists in the codebase (e.g. someone landed it without removing the entry). | Surface for removal. |
   | `stale-paths` | One or more cited `file:line` references are broken (file moved, line drift). Code might still exist elsewhere. | Surface for narrowing. |
   | `rationale-broken` | The cited code does NOT match what the entry's "Why" claims. (Everdawn's example: an entry said one call order was canonical, but 6 of 8 call sites did the opposite.) | Surface for rewrite. |
   | `excluded` | Entry proposes work that `/auto-maintain` exclusions forbid (gameplay numbers, visuals, controls, network ownership, assets). | Note that it's a human-only entry. |

3. **Verification steps per entry** (read-only):
   - Parse `**Why.**` block — pull out every backticked file/symbol reference and `[link](path)` target.
   - For each `file:line` reference: `Read` the cited lines. Does the code match the description?
   - For each helper/interface name proposed in `**Proposal.**`: `Grep` to see if it already exists. If yes → `landed`.
   - For ordering/sequencing claims in `**Why.**` ("X happens before Y"): grep both sites, classify which order dominates. If the entry's claim is the minority pattern, flag as `rationale-broken`.

4. **Report** as a table:

   ```
   ## Audit report — <timestamp>
   <N> entries audited, <fresh>/<landed>/<stale-paths>/<rationale-broken>/<excluded>

   | # | Title | Status | Notes |
   |---|---|---|---|
   | 1 | <title> | fresh | — |
   | 3 | <title> | landed | the proposed helper already exists at <File.cs:N> |
   | 5 | <title> | rationale-broken | claims order A-then-B; 6 of 8 sites do B-then-A |
   ```

5. **Save the report** to `Dev/audit-refactors-<timestamp>.md`. Print the path.

## Rules

- **Read-only.** Do not edit `pending-refactors.md` or any source file. The user decides what to do with each finding.
- **Do not commit.** Save report to disk, stop.
- **Do not auto-rewrite a `rationale-broken` entry.** Surface the conflict explicitly. The user (or `/refactor` on the next drain attempt) re-derives the truth.
- **Treat skill as advisory.** Subagent grep + read can miss RPCs called by name, Godot signal handlers and dynamic dispatch. A `landed` flag is a strong hint, not a guarantee — the user should spot-check before removing.

## When to invoke

- Before `/auto-maintain` runs (catches the same class of bug `/refactor` would hit, but cheaply).
- After a big refactor session — landed work often leaves stale entries that match what just shipped.
- When `pending-refactors.md` has grown to 10+ items and you want to know which are still real.
