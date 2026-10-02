# Combat

Five weapons of steel and wood, each with two skills, a lunge and a guard, against waves of KayKit skeletons: a primary swing, a held Spin, a thrust thrown with a dash, and a held guard that blocks or parries. Six magic staffs and six wands with their books are weapons too, with the same four things in another form (a bolt or volley; a spell held on an area, or a ball that bursts; a poke; a barrier): [magic.md](magic.md). All numbers are first-pass and live in `Core/Combat/` (`Skills`, `Weapons`); tune them there.

## Weapons

| Weapon | Primary | Secondary | Model | Character |
|---|---|---|---|---|
| Greatsword | Slice | Spin | `sword_2handed` | The all-rounder |
| Quarterstaff | Hit | Spin | `staff_A`, held a quarter from its lower end | Cheapest Spin; short Hit |
| Spear | Thrust | Spin | `spear_A` | Longest reach, strongest single hit on a narrow line |
| Scythe | Swing | Spin | `scythe`, drawn 1.35 times its size and held a little below its origin, blade turned to the front | A two-handed war scythe: widest arc and longest sweep, hardest and dearest Spin |
| Sword and shield | Slash | Spin | `sword_1handed` in the right hand, `shield_badge_color` on the left arm | The defender: the best guard of all, the lightest hits |

- **Two slots:** a player carries up to two weapons, one in hand and one on the back (`Core/Combat/WeaponSets`); either slot can be empty. X (controller d-pad left) swaps them; Q (controller d-pad right) changes the weapon in hand to the next kind, skipping the one on the back. Only between swings. A notice names what is carried, the skill bar follows the weapon in hand, and a slot after Dash names the one on the back. `--weapon <id>` and `--back-weapon <id>` pick the starting weapons (default: greatsword in hand, spear on the back). Q is a placeholder from before weapons could be picked up or bought: it turns the weapon in hand into a plain one of another kind for nothing. It does nothing with an empty hand, since a weapon out of nothing could be sold to the merchant ([trade.md](trade.md)).
- **Dropping:** G (controller right-stick click) lets go of the weapon in hand, between swings. It lies on the ground 0.7 in front, on its side, for anyone to take. To drop the one on the back, swap first.
- **Picking up:** F (controller A) takes a weapon lying within 1.6: of those in reach, the one nearest the spot 0.7 in front of the player (`Pickups.Focused`), so facing a weapon chooses it and two lying together can be told apart. Plain nearest-to-the-player could not be steered: bots that let go of their weapons side by side each stood by their own with the other a hair closer, and neither could take its own back. It goes into the hand if it is empty, else onto the back, and only while one of the two slots is empty: with both full, something has to be dropped first. Two weapons of the same kind can be carried.
- **Unarmed:** with an empty hand there is no swing, Spin, lunge or guard; dashing still works. The arms swing with the run instead of keeping the two-handed stance, and standing uses the unarmed idle (`Melee_Unarmed_Idle`).
- **Label:** within 4 of a weapon on the ground its name shows over it. The one the player would take (the one it faces; with none in reach, the one it faces most nearly) also shows what it does in this player's hands (primary damage and reach, Spin damage and mana per turn, lunge damage, how much its guard stops), and within reach either "F - pick up" or, with both slots full, that they are full and G drops.
- **A pair that is one weapon:** the sword and shield is a single weapon to everything but the eye. It takes one slot, is picked up, dropped, bought, improved and sold as one, and has the same four things every weapon has (a swing, a Spin, a lunge, a guard). Only how it is drawn knows there are two pieces (`WeaponLook.OffHand`): the sword in the right hand and the shield in the left, both on the back (the shield flat between the shoulders, the sword sheathed across it), both on the ground (the shield face up beside the sword), both on the dash ghosts. The trail and the reach are the sword's.
- **One-handed stance:** the sword and shield are held up in the stance KayKit calls unarmed (`Melee_Unarmed_Idle`: sword upright, shield before the chest), standing and over the leg clips alike (`WeaponStance.OneHanded`), so the shield does not swing with the run. The hands stay 0.87 apart. Its slash is `Melee_1H_Attack_Slice_Diagonal`, its lunge `Melee_1H_Attack_Stab`, and its guard the same `Melee_Blocking` every weapon uses, which with a shield on the arm is a shield raised. Its Spin is the two-handed loop, the only one there is: both arms come round in front, the shield before the body and the sword across it.
- **Two-handed:** the other four are held in both hands, as Everdawn's commander holds his spear (`Melee_2H_Idle`, the idle its `basic-spear-2h` and `basic-sword-2h` use). Standing, that clip is the idle; moving, the arms keep its pose over the leg clip (`CharacterAnimator`'s stance layer), so the weapon is never carried in one swinging hand. The hands stay 0.69 apart; with the run clip's arms they were 1.14.
- **The scythe's blade leads:** its model's blade sticks out to one side of the shaft, and as modelled that side trailed behind every swing and pointed back at the wielder in the stance (`swing-survey` read `head_leads` -0.84). It is turned half a turn about the shaft in the hand (`WeaponLook.HandTurn`), which puts the blade in front (0.61) and, with the bigger size, makes its point the furthest thing the weapon reaches with: the Swing went from 1.8 to 2.7 and the Spin from 2.1 to 3.1.
- **On the back:** the greatsword, and the sword of the sword and shield, hang as Everdawn carries a sword out of battle, hilt behind a shoulder and blade down the back (its sheath pose). Staff, spear and scythe are carried by their middle, point up, across the back; the spear, the longest, leans furthest so its butt stays off the ground. Poses per weapon in `Character/CombatVisuals`.
- **On the ground, the host decides** (`Main/GroundWeapons`): a player's machine asks the host to put its weapon down or to hand one over, and the host tells every machine. A weapon two players reach for goes to the first request to arrive; the other is told it was taken. A machine that joins later is sent what already lies there.
- **Multiplayer:** each player's machine owns its sets and replicates both; every machine shows each player's weapon in hand and on the back. Swings and hits travel as skill ids, so a hit always counts with the skill it was swung with, even if the weapon change before it hasn't reached a machine yet ([multiplayer.md](multiplayer.md)).

## Hits

There is no collider on the weapon. A melee hit is an arc test at the skill's hit time (`Core/Combat/MeleeArc`): the target's body edge is within `Range` of the attacker's centre, and the target is inside `HalfArc` of the aim. The arc widens by the target's angular width, so a body at the edge still gets caught. Outside the hit moment (or a sweep's window) a weapon deals no damage.

**Hit times** are measured from the clips (`./dev.sh swing-survey`), in clip time: a swing lands when the weapon moves fastest; the thrust lands at full extension, since its fastest moment is the wind-up. Swings play at **attack speed 2x** (`CombatTiming.AttackSpeed`), so a swing takes half its clip length and the hit lands at half its clip time.

**Ranges are the weapon's reach** at the hit moment: how far its farthest point is from the body centre (`CharacterRig.WeaponReach`), whichever part that is; a scythe's blade curves back towards the wielder, so its point is not always what reaches furthest. `net-test` measures that live at every player hit test and fails if the median drifts more than 0.25 from the range. Ranges follow play: while running, the legs clip keeps the hips, so swings that lunge through the hips reach less than in the standing clip (the survey prints both).

| Skill | Clip (authored length) | Swing takes | Hit lands at | Range (standing clip) | Half arc | Damage |
|---|---|---|---|---|---|---|
| Greatsword Slice | `Melee_2H_Attack_Slice` (1.10 s) | 0.55 s | 0.19 s | 1.9 (1.96) | 70 deg | 20 |
| Quarterstaff Hit | `Melee_2H_Attack_Chop` (1.63 s) | 0.82 s | 0.41 s | 1.55 (2.07) | 45 deg | 16 |
| Spear Thrust | `Melee_2H_Attack_Stab` (1.60 s) | 0.80 s | 0.38 s | 2.7 (2.78) | 20 deg | 48 |
| Scythe Swing | `Melee_2H_Attack_Slice` (1.10 s) | 0.55 s | 0.20 s | 2.7 (2.78) | 90 deg | 18 |
| Sword Slash | `Melee_1H_Attack_Slice_Diagonal` (1.00 s) | 0.50 s | 0.21 s | 1.7 (2.04; 1.53 to 1.85 in play, run to run) | 60 deg | 14 |
| Minion chop | `Melee_1H_Attack_Chop` (1.07 s) | 0.54 s | 0.30 s | 1.65 | 45 deg | 6 |
| Warrior chop | `Melee_1H_Attack_Chop` (1.07 s) | 0.54 s | 0.30 s | 1.5 | 45 deg | 12 |
| Archer shot | `Ranged_Bow_Draw` (1.33 s), then `Ranged_Bow_Release` (1.33 s) | 1.33 s | 0.70 s (the arrow leaves) | shoots from up to 10 | the arrow decides | 8 |

| Spin | Range (constant) | Damage per target per revolution | Mana per revolution |
|---|---|---|---|
| Greatsword | 2.3 | 10 | 4 |
| Quarterstaff | 1.95 | 8 | 3 |
| Spear | 2.4 | 10 | 4 |
| Scythe | 3.1 | 12 | 5 |
| Sword and shield | 1.75 | 7 | 3 |

Every Spin plays the `Melee_2H_Attack_Spinning` loop (0.667 s per revolution, 0.33 s at 2x) with its whole revolution as the hit window, all round. Swing times and hit times above are at the 2x base attack speed. Players swing faster with AGI and hit harder with STR ([ui.md](ui.md), "Stats and mana"): the Knight swings at 2.2x (about 3.3 Spin revolutions a second) and deals 1.3 times the damage above (26 with Slice, 13 per greatsword Spin revolution).

- **Spin is held**: it keeps spinning while the button is down and each next revolution can be paid for, and stops the moment the button is released. Each revolution is its own hit window: every enemy within reach is hit once per revolution. The loop turns the whole body through its root bone, so it plays full-body (the torso twist toward the aim gives way to it). The player can move and turn while spinning, at half run speed in every direction (`MoveSpeedFactor` 0.5; facing means nothing mid-spin, so backpedal is not slower), like Diablo's whirlwind. Mana is paid as each revolution starts; with 5 mana regenerated per second, a full pool lasts about 6 s of greatsword spinning. No cooldown; the skill bar dims it while mana is short.
- **Controls:** the primary is left click (controller RT or X); Spin is right click (controller Y or RB). Holding both spins. Dashing (Space, controller B) with the primary held lunges. Shift (controller LB) holds the guard. X (controller d-pad left) swaps weapon sets; Q (controller d-pad right) changes the weapon in hand; G (right-stick click) drops it; F (controller A) picks one up.
- **Player swings:** the player's own machine tests the arc against enemies as it sees them, then asks the host to apply the damage.
- **Enemy swings:** the host tests the arc against players as it sees them.
- **Dashing while spinning:** a Spin still held carries on through a dash. The dash moves the body at its own speed (the half-speed glide does not apply for those 0.3 s) while the loop keeps the whole body turning: every revolution still hits what is in reach and still costs its mana. Dashing through a pack spins through it.
- **Dashing:** a dash cancels any other swing (a primary swing that had not landed yet goes on as a lunge, below) and rolls through skeletons, but gives no invulnerability ([locomotion.md](locomotion.md), "Dash"). Skeletons test their hits against the position a player last reported rather than the smoothed one the host shows, so a dash counts as early as the host can know about it.
- **Player swings test other players only in PvP** ([multiplayer.md](multiplayer.md)); in co-op they only test skeletons.

## Thrusts and lunges

A thrust covers a narrow line (20 deg half arc) where a swing covers an arc, so it hits twice as hard as the weapon's swing (`Skills.ThrustDamageFactor`). The spear's Thrust is one, and every weapon has another in its lunge.

**Lunge:** a dash thrown with the attack button, whatever the weapon: the greatsword stabs instead of slicing. Holding the primary as the dash starts, pressing it within 0.1 s after (`DashRules.LungeWithin`), or dashing out of a primary swing that has not landed yet all turn the dash into a lunge. The character moves as in any dash (same charge, 3.5 over 0.3 s, same direction rules) with the weapon thrust out (`Melee_2H_Attack_Stab`). The thrust points along the aim, not along the dash: dashing sideways with the aim on a skeleton stabs it in passing.

- **Timing:** the stab plays at whatever speed brings it to full extension exactly as the dash ends (`CombatTiming.LungeSpeed`; about 2.7x from the start of a dash), not at the attack speed. Its hit window opens 40% of the way there, as the point starts forward, and closes at full extension; each enemy the point is carried into is hit once.
- **Animation:** the dash clip keeps the legs and the stab takes the upper body, with the torso twist holding it on the aim. The stab's follow-through then plays out on the upper body (about 0.3 s) before the next swing can start.
- **Cost:** the dash charge it rides on. No mana and no cooldown of its own.
- **Multiplayer:** the owner sends reliable `StartLunge(skillId, landsIn)` after `StartDash`; every machine plays the stab timed to its own copy of the dash. Hits are the owner's, by skill id, like any swing.

| Lunge | Damage | Range |
|---|---|---|
| Greatsword | 40 (twice Slice) | 2.6 |
| Quarterstaff | 32 (twice Hit) | 2.25 |
| Spear | 48 (its Thrust) | 2.75 |
| Scythe | 36 (twice Swing) | 2.8 |
| Sword and shield | 28 (twice Slash) | 2.25 |

The sword's lunge plays the one-handed stab, which is at full extension 0.48 s into its clip where the two-handed one takes 0.76 s; each closes its hit window at its own.

Ranges are the weapon's reach at full extension in a lunge forward, which `net-test` measures live. The forward dash clip leans the hips into the stab, so for the straight weapons that is more than `swing-survey`'s hips-at-rest reach (2.38, 2.05, 2.52); the scythe's hooked blade reaches the same either way (2.77); lunges to the side or back lean less and reach about 0.25 short of their range.

## Guard

Holding Shift (controller LB) raises the weapon in hand as a guard (`Core/Combat/Guard`): the `Melee_Blocking` loop on the upper body at attack speed, so the legs keep moving under it. It goes up only between swings, and while it is up nothing else starts (no swing, no weapon change). A dash or going down drops it. Once lowered it cannot go up again for 0.4 s; the skill bar's guard slot turns gold while it is up and dims while it recovers.

- **Front arc:** an attack from inside the guard's half arc of the aim is guarded; from outside it, the attack lands in full. Swings are judged from where the skeleton stands, arrows from where they were loosed, PvP hits from where the attacker last reported standing.
- **Parry:** an attack that lands within the parry window of the guard going up is parried: no damage. A parried skeleton's swing ends there, and it reels for 1 s (a forced stagger that ignores the stagger immunity, `Stagger.Force`), long enough to be punished. A parried arrow just stops.
- **Block:** after the window, the guard blocks: a share of the damage gets through (rounded, `Guard.DamageThrough`).
- **Moving:** a guarded player moves at the guard's share of the speed for its direction (`MoveSpeed.For`).
- A skeleton's chop lands 0.3 s after its swing shows, so a guard raised early in the wind-up parries it; one held up in advance blocks.

| Guard | Half arc | Damage through a block | Move speed | Parry window | Character |
|---|---|---|---|---|---|
| Greatsword | 75 deg | 0% | 40% | 0.2 s | A wall: blocks everything, barely moves |
| Quarterstaff | 100 deg | 20% | 50% | 0.25 s | Widest arc and longest parry window: the parrying weapon |
| Spear | 60 deg | 25% | 50% | 0.2 s | Narrow guard for a weapon that keeps things at a distance |
| Scythe | 70 deg | 40% | 60% | 0.15 s | Leaky block and the tightest parry, but moves the most freely |
| Sword and shield | 110 deg | 0% | 70% | 0.3 s | The shield: stops everything across the widest front, slows least and parries most easily |

- **Feedback:** "Parry!" in gold or "Block" floats over the player on every machine; a parried skeleton flinches (`Hit_A`).
- **Multiplayer:** the player's machine raises and lowers its guard with reliable RPCs to every machine, and each machine times the guard by its own clock. The host's copy decides, like every hit: the parry window starts when the raise reaches the host, and facing is the aim the player last reported ([multiplayer.md](multiplayer.md)).

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
| Skeleton Archer | 30 | 2.4 | 2.6 s | `bow_withString` (left hand) |

- **AI** (`Core/Combat/EnemyBrain`): the nearest living player outside the allied town is the target, wherever it is (aggro reaches further than the arena is long, so a wave marches out of its fortress on the players). Chase, by the way round the walls ([level-layouts.md](level-layouts.md)), until within 80% of reach, then attack when the cooldown is ready, otherwise hold and face the target. An archer's reach is its shooting range (10, so it stops at 8), and while it cannot shoot yet it backs away from a player closer than 5 (`KeepAway`).
- **Archers** (Skeleton Rogue model): draw, then loose an arrow at the target at the moment the string hand snaps back (1.40 s of clip time across the two clips, measured by `./dev.sh swing-survey`), turning to follow the target while drawing. Fragile (30 HP): reaching them is the answer, and they back off to make that take effort.
- **Arrows** (`Core/Combat/Projectile`, `Enemy/Arrows`): fly level at 16 across the ground for up to 14, aimed at where the target stands as it is loosed, with no lead, so running sideways at long range gets out of the way. A hit is a body within 0.15 of the arrow's path each step (the whole step is tested, so a fast arrow cannot pass through a body between frames). The host decides hits, against the positions players last reported, as for swings; 8 damage. Arrows pass over downed players. Against the self-test bots, which keep moving slowly, most arrows hit (49 of 58 in a 2.5-minute playtest).
- **Spawn:** rises out of the ground (`Skeletons_Spawn_Ground`, 3.57 s authored, 1.8 s at 2x playback); the AI waits for it to finish.
- **Hit:** a 0.3 s stagger with a flinch, unless mid-swing. After a stagger a skeleton cannot be staggered again for 1 s (`Core/Combat/Stagger`). Without that, Spin's hits every 0.3 s kept skeletons staggered outside their reach: in a 20 s test they started 0 attacks. **Death:** `Skeletons_Death` (1 s at 2x), corpse removed after 3 s.
- **Waves** (placeholder): 3 minions, 2 warriors and 2 archers, rising inside the enemy fortress, one to a spawn spot. The next wave comes 3 s after one is cleared. Not scaled by player count; the five-bot self-test sessions triple each wave (`--wave-scale 3`), since one wave shared by five is gone before the slower weapons get a turn.

## Gold, souls and orbs

Three things a player earns (`Core/Loot`, `Main/Loot`). Gold buys weapons from the weaponsmith and, from the blacksmith, the first two tiers of armour; gold with orbs buys the later tiers and has the blacksmith make a weapon better, up to +10, each level hitting harder ([trade.md](trade.md)); souls buy nothing yet. All are kept per player by the host and last for the session.

| Monster | Gold | Souls | Magic orb |
|---|---|---|---|
| Skeleton Minion | 2-4 | 1 | 2% |
| Skeleton Archer | 3-6 | 1 | 3% |
| Skeleton Warrior | 6-10 | 1 | 6% |

- **Gold** falls where a monster dies, as a stack of coins (small up to 3, medium up to 6, large above), any amount in its range as likely as another. Walking within 1.5 of a pile picks it up, with no button; a pile cannot be picked up for its first 0.4 s, so it is seen to fall even under a player's feet. "+N" floats over whoever picked it up.
- **Souls:** one per monster, as it dies. Nothing drops.
- **Magic orbs** are the rare one: a monster leaves one over its gold only a few times in a hundred (about one in four waves of seven). It is picked up as gold is, by walking within 1.5 of it after its first 0.4 s, and "+1 orb" floats over whoever did.
- **How an orb looks** (`Main/MagicOrb`, shaders `orb_gem` and `orb_halo`): a cut gem floating chest high, turning and bobbing, every facet its own colour and the colours drifting round once in 6 s, the facets turned to the eye flashing white; a soft glow round it, a light in the colour of the moment thrown on the ground and on whoever stands near, and sparks rising. No texture or model file: a low-faced sphere and two shaders.
- **Shared:** every player in the game gets a pile's gold or an orb when anyone picks it up, and every player gets a monster's soul whoever killed it. Co-op among friends, so nobody races anybody for any of it. A player who joins later starts from nothing.
- **What lies on the ground stays** until picked up.
- HUD: the three counts in the player frame, the orbs' in the colour the orbs are passing through ([ui.md](ui.md)).

## Players

- 100 HP, owned by the host.
- Armour, a tier from 0 to 5 made better by the blacksmith, stops a tenth of every blow per tier, after the guard has had its share ([trade.md](trade.md)).
- Players start in the allied town, where skeletons neither come nor can hurt them ([level-layouts.md](level-layouts.md)).
- At 0 HP the player goes down (`Death_A`) and gets back up after 4 s in the allied town with full HP. The body on the ground is not shoved by skeletons walking over it (they still bump into it); in the first long playtests they pushed a fallen player 0.25. Placeholder until death and revive are designed.
- HUD: HP bar top left. Damage numbers float over whoever is hit. Skeletons and other players carry a thin HP bar overhead ([ui.md](ui.md)).
- Heads are Everdawn's size (0.75 of the model's, `CharacterRig.HeadScale`), so figures have Everdawn's proportions. They were 0.55 for a while, which made bodies read as squat.

## Verified by `./dev.sh net-test`

Five players, one per weapon, so every skill is measured, each with a different weapon on the back, for about 50 s against tripled waves. Every machine shows each player's weapons in hand and on the back, and sees each bot swap its sets and swap back. Running, both hands stay on a weapon for two (0.69 apart), and the sword and shield stay up in their stance (0.87) instead of swinging with the run. Blinks fire on skeletons and players and clear after their quarter second. Trails build during swings and are gone after them, skeleton trails appear, and the live weapon reach at hit time matches each skill's range (the bots also swing at the air while closing in, so each skill is measured over 8-14 swings). Archers' arrows reach every machine, are drawn where they fly (the arrow model once carried an offset that drew every arrow 9.85 from its path, though hits were tested on the path), and clear once they have flown their distance, and some hit players. Every peer lands hits of its own, every peer sees skeletons die, damage from all four peers reaches the host, and skeleton attacks land on players. Swing clips are measured playing at the attack speed (2.20x, the Knight's AGI). Every peer spins with its own weapon and lands spin hits; while spinning the player moves but never faster than half run speed (measured 2.75 against 5.5); at full strength the root bone turns one revolution per cycle (measured 1.00; upper-body-only playback would read near 0); and a held Spin carries on past two revolutions on the host and on a client. Spins held through dashes carry on (no dash cuts short a Spin whose button is still down), keep testing for hits for the whole dash, and other machines see the player spin through it. The map's own checks are in [level-layouts.md](level-layouts.md). Dashes: the longest covers the fixed distance and none goes further, since a wall can now cut one short. Every bot lets go of its weapon, stands by it unarmed and takes it back: every machine sees all four weapons put down and taken up and ends with none on the ground, no attack starts with an empty hand, and the labels show for exactly the weapons in range, offering the weapon while a slot is free. Skeletons leave gold: every machine sees the piles fall and be picked up, every player ends with the same gold and souls, the host's player holds exactly the gold collected (105 of 139 dropped in one run) and one soul per skeleton it saw die, and the HUD shows both. Every bot lunges and every machine sees it; each lunge's hit window closes within two frames of its dash ending (0.021 s at most); a forward lunge reaches its range (within 0.02); and lunges land hits. Every player raises its guard and every machine sees it; a held guard stays up until a dash or going down drops it (a guard raised after a swing once dropped the next frame, because its loop made the finished swing look active again); a guarded player never moves faster than its weapon's guard allows (measured at most 1.000 of it); and on the host guards both block and parry, and a parried skeleton reels. The bots guard in phases when a skeleton is close: mostly they face the nearest skeleton, turn to one as its swing starts and raise the guard just into it (parries, 5-10 a run); every third phase they raise it at once and hold it (blocks).

## Verified by `./dev.sh playtest`

The same session run for 2.5 minutes with every net-test gate, and one player taken down on cue by the host (`--down-at`) on top of the ones the skeletons take down. Waves keep coming (4 tripled waves, 21 skeletons each, in a run). Dead skeletons are gone within their 3 s and damage numbers within their 0.8 s, on every machine. The node count stays flat from wave to wave (1331-1332 from the second wave on) and no orphan nodes appear: it is the fewest nodes in each wave while every player carries both weapons, leaving out skeletons, damage numbers, gold on the ground, HP bars and arrows (each has its own check), since the fight is already on when a wave rises with players in the fortress; a failing run names what piled up (`grew_at_peak`), and a stray node left behind by every skeleton death was shown to fail it. Every machine sees each downed player get back up after 4.00 s at full HP; the downed player's own machine sees it stay exactly where it fell, make no attacks, take no hits, and rise at the arena centre. In one run: 195 arrows, 160 of them hitting; 38 lunges landing 29 hits; guards blocking 43 attacks and parrying 13, with 12 skeletons reeling.

## Open questions

- Whether Spin's mana cost, damage and half-speed glide feel right.
- Whether Spin's hits should stagger at all, or the 1 s stagger immunity is the right answer.
- Enemy count and HP scaling with player count.
- Death: respawn timer, teammates reviving, any penalty.
- Hit feedback: hit-stop, knockback, sound.
- Tracing the actual blade path for hits instead of the arc, if arcs ever feel off against the visuals.
- Guard: whether each weapon's trade-off (arc, block share, speed, parry window) reads in play; whether a parried arrow should do more than stop; whether a client's parry window should allow for its latency, since the host starts it when the raise arrives.
- Magic orbs: whether they should stay shared; how rare; whether one should be heard or marked when it falls out of view.
- Gold and souls: what they buy; whether gold should be shared or go to whoever picks it up once it buys something; whether piles should fade if nobody collects them; amounts.
- Weapons on the ground: whether any should lie in the arena at the start or drop from skeletons (today the only ones are those players let go of), whether Q should go now that weapons can be picked up, and whether a dropped weapon should stay forever.
- Scythe: bigger and with its blade in front it now out-reaches everything but the spear's thrust, on the widest arc; whether its damage, mana cost or guard should pay for that.
- Spear: its Thrust keeps the `Melee_2H_Attack_Stab` clip; the commander's own attack clip in Everdawn is `Melee_2H_Attack_Slice`, which would make it a swing.
- Lunge: whether 2x on a free dash is too strong (it one-shots minions and archers with every weapon), whether it should point along the dash instead of the aim, and whether the 0.1 s window to press attack after the dash is right.
- Archers: whether they should lead a moving target, whether 8 damage at this accuracy is right, and whether arrows should stop at obstacles once the arena has them.
