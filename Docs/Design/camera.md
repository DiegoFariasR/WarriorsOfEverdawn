# Camera and controls

Four camera modes, cycled with **C** (controller: **Back**). Each player picks their own; nothing about it goes over the network. The game starts in **mode 3 (Behind)** every time; the choice is not remembered between sessions. A notice at the top of the screen names the mode at start and on every switch.

| Mode | Camera | Move (WASD / left stick) | Aim and turning |
|---|---|---|---|
| 1 Angled | Above and behind at a fixed 58 deg tilt, world-aligned | Across the screen (W is up) | Mouse cursor, or right stick |
| 2 Top-down | Straight down from 20 above, world-aligned (GTA 1 view) | Across the screen | Mouse cursor, or right stick |
| 3 Behind | 7.5 behind the character, turning with it (WoW view) | Relative to facing: W forward, S backpedal, A/D strafe | Mouse turns (cursor hidden); vertical mouse tilts the camera between 5 and 60 deg; right stick turns |
| 4 Top-down, turns with you | Straight down, the character's facing is always up on screen | Relative to facing | Mouse turns (cursor hidden); right stick turns |

- Slice is left mouse, RT or X; Spin is right mouse, Y or RB; dodge is Space or B, towards the held movement direction. The same in every mode.
- **Zoom:** mouse wheel (controller: D-pad up/down) scales the camera distance in every mode, from half to double, 12% per notch. One zoom level is shared by all modes.
- Modes 3 and 4 capture the mouse. Esc frees the cursor; a click captures it again (and swings).
- The camera eases toward where it should be (rate 12/s), so mode switches and turns glide instead of cutting.
- **The view turns instantly, the character does not.** In modes 3 and 4 the mouse turns the camera straight away, and the character turns to catch up at its limited rate (540 deg/s, [locomotion.md](locomotion.md)). Slowing the camera as well would make mouselook feel laggy. In modes 1 and 2 the cursor moves freely and the character turns toward it at the same rate.
- The turning modes fit the locomotion model directly: the aim is the facing, so W, S and A/D select the forward, backpedal and strafe clips. Movement is read relative to the view, not the character, so W always goes where the camera looks.
- This differs from classic WoW, where A/D turn and the right mouse button has to be held to mouselook. Mouselook all the time suits a game whose upper body always faces the aim. Revisit if it feels wrong.

## Verified

`./dev.sh camera-test` (headless): for each mode it places the camera, feeds W and D through the game's own input mapping (`HumanControls.MoveFor`) with a facing off the world axes, and projects the resulting movement onto a 1280x720 screen. W must move the character up the screen and D to the right, with at most 20% drift off that axis. It also checks that the closest zoom brings the camera nearer and the farthest takes it further away.

## Where it lives

- `GodotClient/scripts/Main/CameraMode.cs` and `ArenaCamera.cs`: camera placement per mode, mouse capture, mouse turn and tilt.
- `GodotClient/scripts/Player/PlayerControls.cs`: `HumanControls.MoveFor` and the turning modes.
- `Core/Ground.FromFacing`: movement relative to a facing, with tests.

## To tune by eye

- Angled offset (0, 16, 10); top-down height 20; behind distance 7.5 at a default tilt of 22 deg.
- Mouse turn 0.005 rad per pixel, tilt 0.004 rad per pixel, stick turn 3 rad/s, camera follow rate 12/s.
- Character turn rate 540 deg/s (`Core/Locomotion/Turning.cs`).
- Zoom range 0.5-2x, 1.12x per wheel notch.

## Open questions

- Camera collision in mode 3 once the arena has walls.
- Click-to-move, in the turning modes and in the others once there are obstacles.
- Remembering the camera mode and zoom between sessions (not for now).
