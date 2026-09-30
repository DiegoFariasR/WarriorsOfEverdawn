---
name: dead-code-sweep
description: Find private members (methods, fields, constants) with zero in-file usage across Core and GodotClient/scripts. Output is advisory -- reflection, Godot signal handlers, and dynamic dispatch can produce false positives. Use after a refactor that left dangling helpers, or for a focused cleanup pass.
argument-hint: "[area: core|godot|all] (default: all)"
disable-model-invocation: true
user-invocable: true
---

# Dead Code Sweep

Surveys `private` and `private static` declarations and flags those with no in-file usage. Output is **advisory** — the C# compiler already warns on unused private members, but only catches the strictest cases (signal handlers, partial-class cross-file usage, reflection callers all evade it).

Where polish-engineer's survey catches dead code as a side effect, this skill drives the analysis as the primary task and produces a single report.

## Argument parsing

Default `all`. Other options narrow the scan:
- `core` — `Core/`
- `godot` — `GodotClient/scripts/`
- `all` — both above

## Procedure

1. **Glob** target area for `*.cs` files.

2. **Per file**, extract via Grep:
   - `private\s+(?:static\s+)?(?:readonly\s+)?[A-Za-z<>?\[\],\s]+\s+(_?[A-Za-z]\w+)\s*[(={;]` — private members (methods, fields, constants).
   - Skip auto-properties and lambda captures.

3. **Per symbol**, Grep the full file (and all sibling `*.partial.cs` if the type is partial) for usage:
   - Bare symbol name appearing outside the declaration line.
   - For partial classes, also check sibling files in the same directory matching `<TypeName>.*.cs`.
   - Zero non-declaration hits → `unused`.

4. **Filter out known false-positive patterns:**
   - Symbol names matching Godot signal handler conventions (`OnXxx`, `_OnXxx`) — Godot dispatches these by name.
   - `[Signal]`, `[Export]` or `[Rpc]` attributes nearby — RPCs are called by name (`Rpc(MethodName.X)`), often from another peer's code path.
   - Symbols referenced from `.tscn` files under `GodotClient/` (`Grep` `.tscn` files for the symbol name).

5. **Report** in three buckets:

   ```
   ## Dead code sweep report — <timestamp>
   Area: <area>
   <N> private symbols surveyed, <unused>/<used>

   ### High-confidence dead code (no usage, no signal/export attribute, no scene ref)
   - Core/Combat/Example.cs:45 — `private const float LegacyThreshold = 0.5f`
   - ...

   ### Possibly dead (signal-handler shape — Godot may dispatch by name)
   - GodotClient/scripts/Main/Example.cs:120 — `private void OnPeerConnected(long id)` — no direct caller; may be connected to a signal
   - ...

   ### Possibly dead (referenced only from same-class partials)
   - <symbol> — verify it isn't a partial-class cross-file helper
   ```

6. **Save** to `Dev/dead-code-<area>-<timestamp>.md`. Print the path.

## Rules

- **Read-only.** Do not delete any code. The user reviews and removes deliberately.
- **Do not commit.**
- **False positives are expected.** Godot signal connections and RPCs are the big ones — a method only ever called through `Rpc(MethodName.Downed)` or a signal hookup has no direct caller. Always check `.tscn` files and `MethodName.` uses before deleting a handler.
- **C# compiler warnings (`CS0169`, `CS0414`) are stricter signals.** If the compiler already warns, the symbol is definitely dead — focus the report on items the compiler missed.

## When to invoke

- After a polish round that landed a helper extraction (the originals may be dead now).
- After deleting a feature — orphaned helpers often survive the surface removal.
- Periodically — `GodotClient/scripts/` accumulates dead handlers as features change.
