# Core — Instructions

Plain C# with no Godot dependency (`WarriorsOfEverdawn.Core`). See the root [AGENTS.md](../AGENTS.md) for project-wide rules.

## What belongs here

A rule belongs in Core when it can be decided from numbers alone: who is hit by a swing, how much damage, how fast a character turns or moves, which leg clip plays, how many dash charges are left, what an enemy decides to do. The client feeds Core positions, times and inputs and applies the answer.

| Folder | Owns |
|---|---|
| `Combat/` | Skill and enemy definitions (`Skills`, `Enemies`, `PlayerRules`), hit arcs (`MeleeArc`), hit timing at attack speed (`CombatTiming`), cooldowns, stagger, health, enemy AI decisions (`EnemyBrain`) |
| `Locomotion/` | Leg direction selection, move speeds, turn rate, dash rules and charges |
| `Stats/` | STR / WIS / AGI and what they drive (`StatRules`), mana |
| `Characters/` | The pools this game draws random figures from (`LookPools`). The catalogue, a look and the randomizer are the kit's (`GodotClient/kit/core`, namespace `EverdawnKit.Characters`), which Core references |
| root | `Angles` and `Ground`: yaw and ground-plane conventions shared by both sides |

## Rules

- **No Godot types**, not even in signatures. Use `System.Numerics` or plain floats; the client converts at its edge.
- **Numbers from the art are measurements.** A hit time, range or ground speed that comes from a clip or mesh carries a comment saying where it was measured; the client self-tests measure the live value against it.
- **Fail loud.** Lookups by id throw on an unknown id (`Enemies.ById`, `CombatVisuals.ClipFor`) instead of returning a default.
- **Time is passed in.** Core never reads a clock; callers pass the current time or a delta, which keeps every rule deterministic in tests.

## Tests

- Every rule here has tests in `Core.Tests/`, mirroring the folder layout.
- Expected values reference the constants they derive from (root [AGENTS.md](../AGENTS.md), "Test conventions").
- `./dev.sh test` runs them; they need no engine and take seconds.
