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

**Files:** [Core/Combat/WeaponDefinition.cs](../../Core/Combat/WeaponDefinition.cs) (`Weapons`), [Core/Combat/SkillDefinition.cs](../../Core/Combat/SkillDefinition.cs) (`Skills`), [GodotClient/scripts/Character/CombatVisuals.cs](../../GodotClient/scripts/Character/CombatVisuals.cs) (model, grip and clips per weapon).

**Growth axis.** Weapons the player can pick, each with two skills; every skill needs a `SkillDefinition`, a clip, a skill-bar slot and range/hit-time measurements.

**Cadence.** `active`. The game's premise is picking one weapon between several options. Five so far: the greatsword, then the quarterstaff, spear and scythe together (2026-09-30), then the sword and shield (2026-10-02), the first held in one hand with a piece in the other. Then six magic staffs and six wands with their books (2026-10-02), the first that throw, that land a spell on an area and that burst; eight of each once ice and lightning came; and the bow (2026-10-02), the first weapon of wood that throws, which needed nothing new in Core but its three skills; and the claws (2026-10-02), which needed one field, a skill's own swing speed; and the warhammer (2026-10-02), which needed nothing but its rows. A weapon is still one definition in Core and one look in the client; a new stance or a second piece is the look's business, and a skill says whether it swings, throws (`Projectile`) or lands on an area (`Area`). Enchantments (2026-10-02) multiply the makes of a weapon without adding definitions: a weapon is its kind, an optional element and a level, made on demand (`Weapons.ById`).

**Refactor implication.** Registry of records, as landed: `WeaponDefinition` (two skills) in `Weapons.All`, looks and clips in `CombatVisuals` tables keyed by id. A new weapon is rows in those tables and its assets, not new code paths. If per-weapon behaviour beyond numbers appears (a charge-up, a projectile), that is the point to reconsider the shape.

**Revisit trigger.** A weapon skill that needs behaviour the shared swing and spin code doesn't have.

**Last reviewed.** 2026-09-30

---

## 2. Enemy types

**Files:** [Core/Combat/EnemyDefinition.cs](../../Core/Combat/EnemyDefinition.cs) (`Enemies.All`), [GodotClient/scripts/Character/CombatVisuals.cs](../../GodotClient/scripts/Character/CombatVisuals.cs) (model and weapon per enemy id), [GodotClient/scripts/Enemy/EnemyDirector.cs](../../GodotClient/scripts/Enemy/EnemyDirector.cs) (wave make-up).

**Growth axis.** Enemy kinds (stats, attack skill, look) and, later, looks assembled from individual KayKit parts. Ranged kinds arrived with the Skeleton Archer (2026-09-30).

**Cadence.** `active`. Skeletons are the start; the user plans to "select each part" later, and more enemy kinds are implied by waves that are placeholders today.

**Refactor implication.** Registry of records (the current `Enemies.All` + `ById`), not a switch per kind. What a kind makes of each damage type is a row too (`EnemyDefinition.Resistances`, [damage-types.md](damage-types.md)): a new kind names its own or shares one, as the skeletons share `Enemies.Skeletal`. Per-part looks will replace `CombatVisuals`' (model, weapon) pair with an assembled look; Everdawn's part assembly (`../Everdawn/GodotClient/scripts/Character/`) is the reference when that starts.

Ranged attacks are data too: a skill with a `Projectile`, flown by `Arrows` by attack id. A new ranged enemy is rows and assets; a projectile that arcs, homes or stops at obstacles is new code.

**Revisit trigger.** Per-part looks start, or a projectile needs behaviour beyond a straight, level flight.

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

**Files:** [GodotClient/scripts/Dev/NetSelfTest.cs](../../GodotClient/scripts/Dev/NetSelfTest.cs) and its parts `NetSelfTest.<Area>.cs`, [GodotClient/scripts/Dev/CameraSelfTest.cs](../../GodotClient/scripts/Dev/CameraSelfTest.cs), the gates in `net_test` / `pvp_test` / `camera_test` in [dev.sh](../../dev.sh).

**Growth axis.** One measured check per feature (a `[*-check]` log line plus its `dev.sh` gate), since headless runs draw nothing and the checks are the only automated verification of the client.

**Cadence.** `constant`. Every feature of the first slice added a check (twelve `net-test` gates so far), and new features will keep doing so.

**Refactor implication.** One part per group of related checks. `NetSelfTest` was split on 2026-09-30 into partial-class files (`Combat`, `Body`, `Spin`, `Weapon`, `Hud`, `Dash`; the playtest then added `Session` and `Down`), each holding its checks' fields, measurements and printed lines; the main part keeps the frame loop and player tracking. A new check joins the part for its area, or gets a new part when none fits; it should not grow the main part.

**Revisit trigger.** A part grows past about 200 lines, or `CameraSelfTest` starts gaining checks per feature too.

**Last reviewed.** 2026-09-30
