# Locomotion and facing

How the player's body faces, moves and attacks at the same time. Applies to every `Rig_Medium` character.

## Facing

- The **upper body always faces the aim**. In camera modes 1 and 2 the aim is the mouse cursor or the right stick; in modes 3 and 4 it is the facing the mouse or right stick turns ([camera.md](camera.md)).
- With no aim input (click-to-move, controller with the right stick idle in modes 1 and 2), the aim is the movement direction.
- **Turning is never instant.** Input only sets where the player wants to face; the character's aim, and with it the upper body and the direction its attacks hit, turns toward that at most 540 deg/s (`Core` `Turning.MaxRate`), so half a turn takes a third of a second. This holds in every camera mode. The legs follow the body turn on top of that.
- The **legs face the movement direction**, within a limit, and switch clips beyond it (below).

## Leg direction

Each frame, take the angle between the movement direction and the aim, then pick a leg clip:

| Angle from aim | Leg clip | Cycle |
|---|---|---|
| within 45 deg | `Running_A` | 0.80 s |
| 45 to 135 deg, left | `Running_Strafe_Left` | 0.80 s |
| 45 to 135 deg, right | `Running_Strafe_Right` | 0.80 s |
| beyond 135 deg | `Walking_Backwards` | 1.07 s |

- **Hysteresis:** leave the current quadrant only past about 55 deg from its centre, not 45, so moving along a boundary does not flicker between clips.
- **Leftover angle** (up to +-55 deg from the quadrant centre, with hysteresis): the body turns to the real movement direction, and the spine and chest turn back by the same amount so the upper body stays on the aim. Done in a `SkeletonModifier3D`, which runs after the animation each frame. The counter-turn is split across `spine` and `chest` so the torso does not kink at one joint.
- **Twist limit:** 80 deg. A sudden aim flip would otherwise corkscrew the torso; the body turn catches up within a few frames.
- **Backpedal at walk speed.** KayKit has no running-backwards clip, so moving away from the aim is slower. This is also a design cost for aiming away from the path.
- **Standing still:** the legs turn to follow the aim. There is no turn-in-place clip, so they rotate without stepping. Revisit (small foot shuffle) if it reads badly from the game camera.

### Chest bias of the leg clips

The leg clips turn the chest on their own, measured from the clip data and confirmed in game:

| Clip | Mean chest yaw | Hips yaw |
|---|---|---|
| `Running_A` | +0.3 deg | 0 deg |
| `Running_Strafe_Left` | +15.7 deg | +58 deg |
| `Running_Strafe_Right` | -15.7 deg | -58 deg |
| `Walking_Backwards` | +32.0 deg | 0 deg |

Positive is the character's left. `CharacterAnimator` measures each leg clip's mean chest yaw once at load, from its rotation tracks, and removes it from the twist. The bias fades out while an attack plays, because swings are authored relative to the body. The idle stance keeps its authored chest angle.

With the bias removed, the chest averages 3-9 deg off the aim while running; the rest is stride sway authored into the strafe clips (about +-16 deg). `./dev.sh net-test` gates on frames where the twist turns 20+ deg: the chest measures about 1-9 deg off the aim there, against 33-45 deg without the twist, and the test fails above 12 deg.

## Upper/lower body split

The `Rig_Medium` skeleton branches at `hips`: `spine` -> `chest` -> `head` and both arms on one side, `upperleg.*` on the other. The torso mesh is weighted across `hips`, `spine` and `chest`, so it bends smoothly at the cut.

- Base layer: the leg clip from the table above.
- Stance layer: `Melee_2H_Idle` on the arm bones only, at full weight while moving and none while standing (the idle is that clip). Both arms hang off the chest, so their poses keep the hands together on the weapon over the leg clip's chest; without it the leg clips swing the arms as if empty-handed.
- Attack layer: a one-shot on top, filtered to `spine` and every bone below it.
- Death layer: `Death_A` over everything while the player is down; the torso twist fades out with it.
- **Full body when standing still, upper body only while moving**, blended continuously: the lower-body attack weight is the attack weight times a stillness factor that ramps over about 1/8 s. Some attacks drive the swing through the hips, and lose it when cut at the spine:

| Clip | Hip rotation | Cut at spine |
|---|---|---|
| `Melee_2H_Attack_Slice` | 56 deg | looks stiff |
| `Melee_2H_Attack_Stab` | 33 deg | looks stiff |
| `Melee_2H_Attack_Chop` | 19 deg | fine |
| `Melee_2H_Attack_Spinning` | 0 deg | fine (loop, suits a whirlwind skill) |

- No attack clip moves the root, so movement code stays in charge during a swing. Only the `Dodge_*` clips move the root (about 0.25 m).

## Dash

Like PoE2's dodge roll (`Core/Locomotion/Dash.cs`, first-pass numbers):

- **Space** (controller B). With a movement key held, the dash goes that way (whatever W/A/S/D mean in the current camera mode); with none, it goes where the character faces.
- **Fixed distance:** 3.5 over 0.3 s, whatever the input, unless something solid is in the way.
- **2 charges**, one coming back every 2 s after it is spent; no cost. The skill bar shows a pip per charge and the seconds to the next.
- **A held Spin carries on** through the dash: the dash clip does not show, the Spin keeps the whole body, and the dash only moves it.
- **Cancels** any other swing, and a Spin whose button was let go: the swing stops counting at once and only its animation fades. With the attack button it becomes a **lunge** instead ([combat.md](combat.md), "Thrusts and lunges"): the dash clip then keeps only the legs and a stab takes the upper body.
- **Rolls through skeletons:** they do not block the character while it dashes. No invulnerability: a skeleton's hit still lands if the player is inside its arc at the hit moment.
- **Animation:** `Dodge_Forward` / `_Backward` / `_Left` / `_Right`, picked by the dash direction relative to the facing and fitted to 0.3 s. It plays full-body except the root bone, whose own travel in the clips (0.25-0.65) would otherwise double up with the code's movement and snap back at the end.
- **Ghosts:** a posed copy of the character left behind every 0.06 s of the dash, fading over 0.3 s, in Everdawn's spirit look (`spirit` + `spirit_depth` shaders, cyan tint). Pooled per character.
- On every machine: the owner moves itself and sends one reliable `StartDash`, which says whether a Spin carries on (and `StartLunge` after it for a lunge); every peer then plays the clip and leaves the ghosts.

## Where it lives

- `Core/Locomotion/`: `LegDirectionSelector` (clip choice, hysteresis, leftover angle) and `MoveSpeed`, with tests.
- `GodotClient/scripts/Character/`: `CharacterAnimator` (animation tree, attack layers, chest bias), `TorsoTwistModifier`, `RigAnimations` (clip names, looping).
- `GodotClient/scripts/Player/PlayerCharacter.cs`: feeds velocity and aim in, on every machine.

## Animation speed

KayKit's short-legged characters take small strides, so their clips cover little ground. Each leg clip plays at **movement speed / the clip's own ground speed**, measured at load from how fast its planted foot moves back along the direction of travel (`ClipMotion.GroundSpeed`). The feet never slide, whatever the movement speed.

| Clip | Authored ground speed | Moving at | Plays at |
|---|---|---|---|
| `Running_A` | 3.26 | 5.5 | 1.7x |
| `Running_Strafe_Left` / `_Right` | 3.84 | 5.5 | 1.4x |
| `Walking_Backwards` | 0.77 | 2.5 | 3.2x |
| `Skeletons_Walking` | 0.74 | 2.6 (minion), 2.2 (warrior) | 3.5x, 3.0x |

Swings play at `CombatTiming.AttackSpeed` (2x, [combat.md](combat.md)). Idle, spawn, hit and death clips play at `RigAnimations.PlaybackSpeed` (2x).

## To tune by eye

- `MoveSpeed.Run` (5.5) and `MoveSpeed.Backpedal` (2.5) are first guesses; the legs follow either. Backpedal and the skeleton walk play above 3x, which may look like quick little steps; lowering those speeds calms the legs (1.5 would give about 2x).
- Leg crossfade 0.15 s, body turn rate, attack fade in/out.

## Open questions

- Click-to-move: needs navigation once the arena has obstacles.
