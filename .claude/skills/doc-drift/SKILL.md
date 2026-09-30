---
name: doc-drift
description: Semantic re-verification of every file:line reference in Docs/Design/*.md and the AGENTS.md files. For each cited line, read the code and confirm it still matches the doc's surrounding claim. Catches the class of bug ./dev.sh check-docs misses -- doc says "the order is X then Y" but the code does Y then X.
argument-hint: "[file: <path>] (default: scan all design + instructions docs)"
disable-model-invocation: true
user-invocable: true
---

# Doc Drift

`./dev.sh check-docs` catches **broken paths** — file moved, line drifted, subcommand renamed. This skill catches **broken claims** — the file still exists, the line still exists, but the surrounding doc text no longer matches what the code does.

The canonical example (from Everdawn): a refactor entry cited a file and claimed one call order was canonical. The cited lines existed; `check-docs` was clean. But the code did the opposite in 6 of 8 sites. Here the same class of drift shows up as numbers: a design doc quoting a range, speed or hit time that `Core` has since retuned.

## Argument parsing

- empty → scan all `Docs/Design/**/*.md` + `AGENTS.md` + `Core/AGENTS.md` + `GodotClient/AGENTS.md` + `CLAUDE.md`.
- `--file <path>` → scan one doc.

## Procedure

1. **Collect** target docs via Glob.

2. **Per doc**, extract:
   - Backticked code refs: `` `ClassName.MethodName` ``, `` `bar_key` ``, `` `field_name` ``.
   - Markdown links to source: `[label](Core/.../File.cs)`, `[label](path:N)`.
   - Numbers quoted from code: ranges, speeds, damage, hit times, costs, gate thresholds.
   - Inline ordering claims: prose like "X happens before Y", "the canonical order is A then B", "calls C, then D".

3. **Per claim**, verify:
   - Backticked symbol → `Grep` the symbol across the repo. If zero hits, flag `symbol-missing`.
   - File link → `Read` the file. If the file doesn't exist, flag `file-missing` (also caught by `check-docs`; this is just defense in depth).
   - **Ordering claim** → grep both sides of the claim, count occurrences in each ordering, compare to what the doc asserts. If the dominant pattern in code is the opposite of what the doc claims, flag `ordering-backwards`.
   - **Quoted number** → find the constant in `Core/` (or the gate in `dev.sh`) and compare. A mismatch is `number-drift`.

4. **Sample claims to look for** (this isn't exhaustive — read the doc's prose and apply judgment):
   - "X is called before Y" / "Y runs after X"
   - "the helper is named X" / "use X to do Y"
   - "the cap is N" / "the threshold is M"
   - "only allies see X" / "only enemies build Y"
   - "X fires once per Y" / "X fires per Z"

5. **Report** by severity:

   ```
   ## Doc drift report — <timestamp>

   ### ordering-backwards (highest priority — docs that lie about code shape)
   - Docs/Design/multiplayer.md:N — claims the host applies damage before the flash RPC; code sends the RPC first
   - ...

   ### number-drift (doc quotes a value the code no longer has)
   - Docs/Design/combat.md:N — says Spin costs 4 mana per revolution; `Skills.Spin.ManaCost` is 5
   - ...

   ### symbol-missing (referenced code that doesn't exist anymore)
   - Docs/Design/locomotion.md:N — references `HeadScaleModifier`, which was removed
   - ...

   ### file-missing (broken path)
   - <usually covered by ./dev.sh check-docs — flag here only if check-docs is failing>
   ```

6. **Save** to `Dev/doc-drift-<timestamp>.md`. Print the path.

## Rules

- **Read-only.** Do not edit docs or code. Surface mismatches; the user (or a follow-up `/refactor`) fixes them.
- **Do not commit.**
- **Pattern detection is fuzzy.** Ordering claims in prose are hard to extract programmatically — when in doubt, surface the ambiguous case to the user rather than asserting wrongness. Mark as `possibly-stale` instead of `ordering-backwards`.
- **This skill complements `./dev.sh check-docs`, doesn't replace it.** Run `check-docs` first to clear mechanical issues, then this skill for semantic ones.

## When to invoke

- After a refactor that renamed core helpers (the docs likely still mention old names).
- When `/refactor` reports a drain attempt found the entry's rationale was backwards (canonical case — this skill would have caught it earlier).
- Periodically — design docs rot fastest in projects with active refactoring.
