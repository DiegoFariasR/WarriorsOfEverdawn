---
name: test-coverage-gap
description: Find Core public surfaces (types, methods) with no direct test coverage. Reports candidates as leads, not conclusions -- integration tests can cover indirectly. Use after a refactor that added or modified public API, or when planning a focused testing pass.
argument-hint: "[area: combat|locomotion|stats|all] (default: all)"
disable-model-invocation: true
user-invocable: true
---

# Test Coverage Gap

Surveys `Core/` for `public` types and methods, cross-references with `Core.Tests/`, and reports surfaces with no direct test reference. Output is **advisory** — integration tests often cover behavior indirectly, and the report should be treated as leads for a focused testing pass, not a coverage SLA.

For real coverage metrics, use `dotnet test` with a coverage collector. This skill is the cheap "what's most likely under-tested" first pass.

## Argument parsing

Default `all`. Other options narrow the scan:
- `combat` — `Core/Combat/`
- `locomotion` — `Core/Locomotion/`
- `stats` — `Core/Stats/`
- `all` — everything under `Core/` (including `Angles` and `Ground` at the root)

## Procedure

1. **Glob** target area for `*.cs` files.

2. **Per file**, extract via Grep:
   - `public (?:static\s+)?(?:async\s+)?[A-Za-z<>?\[\],\s]+\s+([A-Z]\w+)\s*\(` — public methods.
   - `public (?:sealed\s+|abstract\s+)?(?:partial\s+)?(?:class|record|struct|interface|enum)\s+(\w+)` — public types.
   - Skip `Equals`, `GetHashCode`, `ToString`, `Deconstruct` (compiler-generated record members), ctors.

3. **Per symbol**, search `Core.Tests/` for direct usage (`Grep "<SymbolName>\b"`).
   - At least one hit → `covered` (may be indirect; not perfect but good enough).
   - Zero hits → `uncovered`.

4. **Classify uncovered symbols** by likely reason:
   - **Rule entry points** (`MeleeArc.Hits`, `EnemyBrain` decisions, `LegDirectionSelector.Select`) — usually tested under their type's test file. An uncovered one is a real gap.
   - **Helper methods** (`Angles.Wrap`, `Ground.YawOf`, formula functions in `StatRules`) — pure functions; uncovered = real gap, easy to add a unit test.
   - **Data records** (`Skills.*`, `Enemies.*` instances) — values are design numbers; test the rules that read them, not the numbers.
   - **Public accessors / properties** — typically not worth dedicated tests.
   - **Event/signal handlers** — covered by behavioral tests, may not have direct symbol reference.

5. **Report** as:

   ```
   ## Test coverage gap report — <timestamp>
   Area: <area>
   <N> public symbols surveyed, <covered>/<uncovered>

   ### Uncovered helpers (high-priority gaps)
   - `Example.Helper` — pure function, no direct test
   - ...

   ### Uncovered entry points (likely indirect coverage)
   - `Example.Entry` — no direct test, but ExampleTests covers the surrounding rule
   - ...

   ### Uncovered accessors (low priority)
   - `Example.Value` — read-only accessor, probably fine
   - ...
   ```

6. **Save** to `Dev/coverage-gap-<area>-<timestamp>.md`. Print the path.

## Rules

- **Advisory only.** Do not write tests or edit any file. The report is input to a follow-up testing pass the user runs deliberately.
- **Do not commit.**
- **False positives are expected.** A symbol with zero direct grep hits may still be exercised by integration tests via dynamic dispatch or composition. The report's purpose is to flag *suspects*, not to count coverage.
- **Symbols in `*Tests.cs` files don't count** as test references for themselves (a `TestHelper.Foo()` method shouldn't be classed as "tested" because `Foo` appears in test code as its own definition).

## When to invoke

- After a refactor that extracted a new public helper.
- When planning a testing-focused PR — gives a prioritized starting list.
- Periodically (monthly?) to track creeping gaps as the codebase grows.
