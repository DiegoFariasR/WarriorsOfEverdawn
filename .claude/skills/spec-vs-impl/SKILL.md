---
name: spec-vs-impl
description: For one design doc, parse every "shall do X" / "must Y" / "always does Z" claim and locate the implementing code. Reports claims with no clear implementation match. Use when you suspect a design doc has drifted from the system it describes, or when validating a fresh implementation against its spec.
argument-hint: "<doc-path> (required, e.g. Docs/Design/combat.md)"
disable-model-invocation: true
user-invocable: true
---

# Spec vs Implementation

Pick one design doc. Pull every assertion of "the system X does Y" out of it. For each, grep the codebase for the implementing site. Report claims with no clear implementation, or where the implementation appears to do something else.

This is the strongest of the doc-checking trio:
- `./dev.sh check-docs` — mechanical path/symbol resolution
- `/doc-drift` — broad scan, ordering/symbol mismatches across all docs
- `/spec-vs-impl` — deep, focused, one-doc-at-a-time semantic spec audit

## Argument parsing

Required: `<doc-path>` (one design doc to audit).

## Procedure

1. **Read** the entire doc.

2. **Extract assertions** by pattern (apply judgment — not every sentence is an assertion):
   - "X **shall** Y" / "the engine **does** Y" / "Y **happens** when Z"
   - "the cap is N" / "the threshold is M" / "the formula is F"
   - "X is called from Y" / "Y reads from Z"
   - Numbered procedure steps that describe runtime behavior (not authoring rules).

3. **Per assertion**, derive a search target:
   - **Numeric claims** (ranges, speeds, costs, thresholds) → `Grep` for the value as a constant or record field. If the doc says "Spin costs 4 mana" and you find `ManaCost = 4`, match. If you find `ManaCost = 5`, **mismatch** (high priority).
   - **Named-symbol claims** (`X is called from Y`) → `Grep` for the call site. Zero hits → `unimplemented` or `renamed`.
   - **Ordering/sequencing** → grep both ends, compare ordering pattern.
   - **Existence claims** (`the system has X`) → `Grep` for the named type/method/field.

4. **Classify** each assertion:
   - `match` — assertion's described behavior is present in code at the expected shape.
   - `mismatch-value` — numeric / constant value disagrees with code (e.g. doc says 2.3, code says 2.1).
   - `mismatch-shape` — described behavior is in code but doesn't match the assertion (e.g. doc says it's a switch, code is an if-cascade).
   - `unimplemented` — referenced symbol doesn't exist.
   - `inverted` — code does the opposite of what the assertion claims.
   - `unverifiable` — assertion is too abstract to grep (skip; flag as `human-review`).

5. **Report**:

   ```
   ## Spec vs impl report — <timestamp>
   Doc: <path>
   <N> assertions extracted, <match>/<mismatch>/<unimplemented>/<inverted>/<unverifiable>

   ### Mismatches (highest priority)
   - L42: "Spin reaches 2.3" → `Skills.Spin` has Range 2.1 in Core/Combat/SkillDefinition.cs:N
   - ...

   ### Inverted (doc claims the opposite of what code does)
   - L77: "the host decides player hits" → the attacker's machine tests the arc and asks the host to apply damage (PlayerCharacter.cs:N)
   - ...

   ### Unimplemented
   - L120: "click-to-move in the turning modes" → no click-to-move code yet (and the doc lists it as open)
   - ...

   ### Human review (assertions too abstract to grep)
   - L88: "Spin should feel like Diablo's whirlwind" — a feel claim, no specific code to verify
   - ...
   ```

6. **Save** to `Dev/spec-vs-impl-<docname>-<timestamp>.md`. Print the path.

## Rules

- **Read-only.** Do not edit the doc or any code.
- **Do not commit.**
- **One doc at a time.** Auditing all docs at once produces noise and misses subtle mismatches; this skill is deliberately scoped narrow.
- **Numeric mismatches are the highest-confidence signal.** Doc says "range 2.3", code says `Range: 2.1f` — that's almost certainly a real drift. Flag aggressively.
- **Ordering/shape claims are the trickiest.** A switch vs if-cascade may be semantically identical; flag as `mismatch-shape` only when the difference is meaningful (extensibility, performance, correctness).

## When to invoke

- Before a major refactor based on a design doc — confirm the doc matches the system you're about to refactor.
- After landing a refactor that may have moved the implementation away from the doc.
- When onboarding to an unfamiliar subsystem — the report tells you which doc claims you can trust.
- When the user says "the doc lies" or "this doesn't match what I see."
