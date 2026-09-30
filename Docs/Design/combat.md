# Combat

First slice: the two-handed sword with its two skills, Slice (primary) and Spin (secondary), against waves of KayKit skeletons. All numbers are first-pass and live in `Core/Combat/`; tune them there.

## Hits

There is no collider on the weapon. A melee hit is an arc test at the skill's hit time (`Core/Combat/MeleeArc`): the target's body edge is within `Range` of the attacker's centre, and the target is inside `HalfArc` of the aim. The arc widens by the target's angular width, so a body at the edge still gets caught. Outside the hit moment (or a sweep's window) a weapon deals no damage.

Hit times are the blade's peak speed in each clip, measured from the GLB data, in clip time. Swings play at **attack speed 2x** (`CombatTiming.AttackSpeed`), so a swing takes half its clip length and the hit lands at half its clip time.

**Ranges are the blade tip's reach** from the body centre at the hit moment, with the real blade lengths from the weapon meshes (sword 1.96, skeleton blade 1.17, skeleton axe 1.0), so the hit area ends where the drawn blade does. `net-test` measures the live tip at every player hit test and fails if it drifts more than 0.25 from the range. Slice reaches 2.02 in the standing clip but about 1.9 in play, where it often runs on the upper body while moving and twisting; its range follows play.

| Skill | Clip (authored length) | Swing takes | Hit lands at | Range | Half arc | Damage |
|---|---|---|---|---|---|---|
| Slice (player, primary) | `Melee_2H_Attack_Slice` (1.10 s) | 0.55 s | 0.19 s | 1.9 | 70 deg | 20 |
| Spin (player, secondary, held, 4 MP per revolution) | `Melee_2H_Attack_Spinning` loop (0.667 s per revolution) | 0.33 s per revolution | the whole revolution | 2.3 (constant 2.28 tip reach) | all round | 10 per target per revolution |

Swing times and hit times above are at the 2x base attack speed. Players swing faster with AGI and hit harder with STR ([ui.md](ui.md), "Stats and mana"): the Knight swings at 2.2x (about 3.3 Spin revolutions a second) and deals 26 with Slice, 13 per Spin revolution.
| Minion chop | `Melee_1H_Attack_Chop` (1.07 s) | 0.54 s | 0.30 s | 1.65 | 45 deg | 6 |
| Warrior chop | `Melee_1H_Attack_Chop` (1.07 s) | 0.54 s | 0.30 s | 1.5 | 45 deg | 12 |

- **Spin is held**: it keeps spinning while the button is down and each next revolution can be paid for, and stops the moment the button is released. Each revolution is its own hit window: every enemy within reach is hit once per revolution. The loop turns the whole body through its root bone, so it plays full-body (the torso twist toward the aim gives way to it). The player can move and turn while spinning, at half run speed in every direction (`MoveSpeedFactor` 0.5; facing means nothing mid-spin, so backpedal is not slower), like Diablo's whirlwind. 4 mana per revolution, paid as each one starts; with 5 mana regenerated per second, a full pool lasts about 6 s of spinning. No cooldown; the skill bar dims it while mana is short.
- **Controls:** Slice is left click (controller RT or X); Spin is right click (controller Y or RB). Holding both spins.
- **Player swings:** the player's own machine tests the arc against enemies as it sees them, then asks the host to apply the damage.
- **Enemy swings:** the host tests the arc against players as it sees them.
- **Dodging:** a dodge cancels a swing and rolls through skeletons, but gives no invulnerability ([locomotion.md](locomotion.md), "Dodge"). Skeletons test their hits against the position a player last reported rather than the smoothed one the host shows, so a dodge counts as early as the host can know about it.
- **Player swings test other players only in PvP** ([multiplayer.md](multiplayer.md)); in co-op they only test skeletons.

## Hit feedback

Every hit, on players and skeletons alike, shows on every machine:

- **Damage number** floating up from the target.
- **Damage blink**, Everdawn's sword-hit flash: a red, noisy glow over the whole character for 0.25 s (`Character/HitFlash`, using Everdawn's `elemental_fire` overlay shader at its Slash settings, fading in over about 0.05 s and out over about 0.07 s).
- **Skeletons flinch** (`Hit_A`) and stagger for 0.3 s, unless mid-swing.

## Weapon trails

Every weapon draws a ribbon along its blade (`Character/WeaponTrail`, adapted from Everdawn's): from 40% up the blade to the tip, the last 0.15 s of motion, fading with age. It shows only during a swing's **hit window**, with a 0.1 s lead-in and 0.08 s follow-through, so the trail marks exactly when a weapon deals damage. Players' trails are warm white; skeletons' are red, so an incoming swing reads as a threat. Trails run off the swing start on every machine, so remote players' swings trail too.

## Enemies

| Enemy | HP | Speed | Attack cooldown | Weapon |
|---|---|---|---|---|
| Skeleton Minion | 40 | 2.6 | 1.6 s | `Skeleton_Blade` |
| Skeleton Warrior | 70 | 2.2 | 2.2 s | `Skeleton_Axe` |

- **AI** (`Core/Combat/EnemyBrain`): the nearest living player within 18 is the target. Chase until within 80% of reach, then attack when the cooldown is ready, otherwise hold and face the target.
- **Spawn:** rises out of the ground (`Skeletons_Spawn_Ground`, 3.57 s authored, 1.8 s at 2x playback); the AI waits for it to finish.
- **Hit:** a 0.3 s stagger with a flinch, unless mid-swing. After a stagger a skeleton cannot be staggered again for 1 s (`Core/Combat/Stagger`). Without that, Spin's hits every 0.3 s kept skeletons staggered outside their reach: in a 20 s test they started 0 attacks. **Death:** `Skeletons_Death` (1 s at 2x), corpse removed after 3 s.
- **Waves** (placeholder): 3 minions and 2 warriors, 11-15 from the centre. The next wave comes 3 s after one is cleared. Not scaled by player count.

## Players

- 100 HP, owned by the host.
- At 0 HP the player goes down (`Death_A`) and gets back up after 4 s at the arena centre with full HP. Placeholder until death and revive are designed.
- HUD: HP bar top left. Damage numbers float over whoever is hit.

## Verified by `./dev.sh net-test`

Blinks fire on skeletons and players and clear after their quarter second. Trails build during swings and are gone after them, skeleton trails appear, and the live blade tip at hit time matches each skill's range. Every peer lands hits of its own, every peer sees skeletons die, damage from all three peers reaches the host, and skeleton attacks land on players. Swing clips are measured playing at the attack speed (2.00x). Every peer spins and lands spin hits; while spinning the player moves but never faster than half run speed (measured 2.75 against 5.5); at full strength the root bone turns one revolution per cycle (measured 1.00; upper-body-only playback would read near 0); and a held Spin carries on past two revolutions on the host and on a client.

## Open questions

- Whether Spin's mana cost, damage and half-speed glide feel right.
- Whether Spin's hits should stagger at all, or the 1 s stagger immunity is the right answer.
- Enemy count and HP scaling with player count.
- Death: respawn timer, teammates reviving, any penalty.
- Hit feedback: hit-stop, knockback, sound.
- Tracing the actual blade path for hits instead of the arc, if arcs ever feel off against the visuals.
- Enemy HP bars.
