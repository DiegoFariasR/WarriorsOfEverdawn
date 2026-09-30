# Architecture Growth Trajectories

This doc answers **"is system X going to grow, in what direction, how fast?"** for the systems whose answer materially changes a refactor or new-feature design choice. Format copied from Everdawn.

The decision frame: a system that grows monthly favors additive shapes (registry, table-driven lookup, one file per case). A system that's closed favors helper-extract or leave-it — adding interface boilerplate for a frozen set is overkill.

Not a design doc. Design docs answer "what should X do." This doc answers "is X going to add cases."

## How to use it

- **Refactor entries** in [pending-refactors.md](Refactor/pending-refactors.md) should cite `[[architecture-growth#<id>]]` when the growth axis drives the chosen approach.
- **New-feature designs** should check the relevant entry to know what shape future-you will expect.
- When a refactor cites this doc, **briefly restate the trajectory in the entry itself**. The doc rots; the entry survives the doc going stale.

## Window and calibration

- **Window:** project inception (2026-09-29) to today. Extend the lookback as the project ages; don't shrink it.
- **Source of truth:** forward-looking design intent (collected from the project lead) trumps git history when they disagree. The project is days old, so every cadence below comes from stated intent, not history.
- **Cadence labels** (per-month rate, anchored to design intent):

| Label | Rate | Interpretation |
|---|---|---|
| `frozen` | 0 expected, set is design-complete | Adding interface boilerplate is overkill; helper-extract or leave-it |
| `rare` | <1 add per 2 months | Helper-extract suffices; abstraction marginal |
| `active` | ~1 add per month | Favor additive shapes (registry, table-driven lookup, one file per case) |
| `constant` | >1 add per month | Table-driven dispatch mandatory; manual fanout will rot |
| `speculative` | No design intent stated yet | Revisit aggressively; treat conservatively |

## How to maintain

- Each entry carries a **revisit trigger** — concrete, event-driven (e.g. "when the second weapon is designed"). Update when the trigger fires.
- A `last reviewed` date stamps when the entry was last touched. It's a record, not a deadline.
- Update an entry when its cadence shifts, when the revisit trigger fires, or when a refactor closes/reshapes the system.
- Add new entries when a refactor or new-feature design hinges on a trajectory not yet captured.

---

## 1. Weapons and their skills

**Files:** [Core/Combat/SkillDefinition.cs](../../Core/Combat/SkillDefinition.cs) (`Skills`), [GodotClient/scripts/Character/CombatVisuals.cs](../../GodotClient/scripts/Character/CombatVisuals.cs) (clip per skill id), [GodotClient/scripts/Player/PlayerCharacter.cs](../../GodotClient/scripts/Player/PlayerCharacter.cs) (`SkillSet`).

**Growth axis.** Weapons the player can pick, each with two skills; every skill needs a `SkillDefinition`, a clip, a skill-bar slot and range/hit-time measurements.

**Cadence.** `active`. The game's premise is picking one weapon between several options; the two-handed sword is the first, "then add more options later".

**Refactor implication.** Keep skills as data records looked up by id (the current shape). The player's `SkillSet` is one hardcoded pair today; when the second weapon lands, a weapon record (its two skills, model and look) replaces it rather than a second hardcoded array.

**Revisit trigger.** When the second weapon is designed.

**Last reviewed.** 2026-09-30

---

## 2. Enemy types

**Files:** [Core/Combat/EnemyDefinition.cs](../../Core/Combat/EnemyDefinition.cs) (`Enemies.All`), [GodotClient/scripts/Character/CombatVisuals.cs](../../GodotClient/scripts/Character/CombatVisuals.cs) (model and weapon per enemy id), [GodotClient/scripts/Enemy/EnemyDirector.cs](../../GodotClient/scripts/Enemy/EnemyDirector.cs) (wave make-up).

**Growth axis.** Enemy kinds (stats, attack skill, look) and, later, looks assembled from individual KayKit parts.

**Cadence.** `active`. Skeletons are the start; the user plans to "select each part" later, and more enemy kinds are implied by waves that are placeholders today.

**Refactor implication.** Registry of records (the current `Enemies.All` + `ById`), not a switch per kind. Per-part looks will replace `CombatVisuals`' (model, weapon) pair with an assembled look; Everdawn's part assembly (`../Everdawn/GodotClient/scripts/Character/`) is the reference when that starts.

**Revisit trigger.** When a third enemy kind or per-part looks start.

**Last reviewed.** 2026-09-30

---

## 3. Camera modes

**Files:** [GodotClient/scripts/Main/CameraMode.cs](../../GodotClient/scripts/Main/CameraMode.cs), [GodotClient/scripts/Main/ArenaCamera.cs](../../GodotClient/scripts/Main/ArenaCamera.cs). Design: [camera.md](camera.md).

**Growth axis.** Camera and control modes cycled with C.

**Cadence.** `frozen`. The user specified exactly four modes.

**Refactor implication.** A `switch` per mode is right; no strategy interface or per-mode classes.

**Revisit trigger.** A fifth mode is proposed.

**Last reviewed.** 2026-09-30

---

## 4. Leg directions

**Files:** [Core/Locomotion/LegDirection.cs](../../Core/Locomotion/LegDirection.cs), [Core/Locomotion/LegDirectionSelector.cs](../../Core/Locomotion/LegDirectionSelector.cs). Design: [locomotion.md](locomotion.md).

**Growth axis.** Directional leg clips the lower body blends between.

**Cadence.** `frozen`. KayKit's Rig_Medium has one clip per direction for four directions, and the design is built on those four.

**Refactor implication.** Switches over `LegDirection` are fine; don't generalise to N directions.

**Revisit trigger.** Diagonal clips are authored or bought.

**Last reviewed.** 2026-09-30

---

## 5. Stats

**Files:** [Core/Stats/CharacterStats.cs](../../Core/Stats/CharacterStats.cs) (`StatRules`). Design: [ui.md](ui.md), "Stats and mana".

**Growth axis.** Which stats exist (STR, WIS, AGI today) and what each one drives.

**Cadence.** `speculative`. The user called the current stats a first pass that "will be elaborated more later".

**Refactor implication.** Keep every stat formula in `StatRules` and don't abstract over stats until the design settles.

**Revisit trigger.** When stats are designed beyond the first pass.

**Last reviewed.** 2026-09-30

---

## 6. Headless self-test checks

**Files:** [GodotClient/scripts/Dev/NetSelfTest.cs](../../GodotClient/scripts/Dev/NetSelfTest.cs), [GodotClient/scripts/Dev/CameraSelfTest.cs](../../GodotClient/scripts/Dev/CameraSelfTest.cs), the gates in `net_test` / `pvp_test` / `camera_test` in [dev.sh](../../dev.sh).

**Growth axis.** One measured check per feature (a `[*-check]` log line plus its `dev.sh` gate), since headless runs draw nothing and the checks are the only automated verification of the client.

**Cadence.** `constant`. Every feature of the first slice added a check (twelve `net-test` gates so far), and new features will keep doing so.

**Refactor implication.** Favor one unit per check: `NetSelfTest` is the largest script in the client and grows with every feature, so a split into one file (or partial) per check is the expected shape once it next grows, with the `dev.sh` gate next to its check's description.

**Revisit trigger.** The next check added to `NetSelfTest`.

**Last reviewed.** 2026-09-30
