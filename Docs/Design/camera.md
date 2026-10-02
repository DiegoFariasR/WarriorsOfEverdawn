# Camera and controls

Four camera modes, cycled with **C** (controller: **Back**). Each player picks their own; nothing about it goes over the network. The game starts in **mode 3 (Behind)** every time; the choice is not remembered between sessions. A notice at the top of the screen names the mode at start and on every switch.

| Mode | Camera | Move (WASD / left stick) | Aim and turning |
|---|---|---|---|
| 1 Angled | Above and behind at a fixed 56 deg tilt, world-aligned, **orthographic** (an isometric game's view, without the quarter turn) | Across the screen (W is up) | Mouse cursor, or right stick |
| 2 Top-down | Straight down, world-aligned, **orthographic** | Across the screen | Mouse cursor, or right stick |
| 3 Behind | 7.5 behind the character, turning with it (WoW view) | Relative to facing: W forward, S backpedal, A/D strafe | Mouse turns (cursor hidden); vertical mouse tilts the camera between 5 and 60 deg; right stick turns |
| 4 Top-down, turns with you | Straight down, **orthographic**, the character's facing is always up on screen | Relative to facing | Mouse turns (cursor hidden); right stick turns |

- Slice is left mouse, RT or X; Spin is right mouse, Y or RB; dash is Space or B, towards the held movement direction. The same in every mode.
- **The three modes that look down are orthographic** (`CameraModes.IsOrthographic`): no perspective, so a thing is the same size on screen wherever it stands, the far side of the view as large as the near, and walls stand straight instead of leaning away from the middle. Behind keeps its perspective. Each shows, at the player, what the same camera showed with perspective: 17.6 of the field from top to bottom in Angled and 18.7 from straight above, at the normal zoom. There is no horizon in them: what is in view is a patch of ground of that size, and nothing beyond it.
- **Zoom:** mouse wheel (controller: D-pad up/down) scales what the view takes in, in every mode, from half to double, 12% per notch: the camera's distance in Behind, the size of the view in the orthographic modes, whose cameras stay where they are (moving one would change nothing but the haze and the far blur, which are measured from the camera). One zoom level is shared by all modes.
- Modes 3 and 4 capture the mouse. Esc frees the cursor; a click captures it again (and swings).
- The camera eases toward where it should be (rate 12/s), so mode switches and turns glide instead of cutting. Between Behind and an orthographic mode it cuts: there is nothing to glide through between a view with perspective and one without.
- The angled camera stands 1.2 times its offset away along its view (`AngledStandOff`). Without perspective that changes no size; it keeps what is tall near the bottom of the view (the fortress's crypt, 8 high) from rising through the camera at the widest zoom.
- **The view turns instantly, the character does not.** In modes 3 and 4 the mouse turns the camera straight away, and the character turns to catch up at its limited rate (540 deg/s, [locomotion.md](locomotion.md)). Slowing the camera as well would make mouselook feel laggy. In modes 1 and 2 the cursor moves freely and the character turns toward it at the same rate.
- The turning modes fit the locomotion model directly: the aim is the facing, so W, S and A/D select the forward, backpedal and strafe clips. Movement is read relative to the view, not the character, so W always goes where the camera looks.
- This differs from classic WoW, where A/D turn and the right mouse button has to be held to mouselook. Mouselook all the time suits a game whose upper body always faces the aim. Revisit if it feels wrong.

## Texts over things

Whatever is written or drawn over a thing in the world (a seller's name and prompt, HP bars and statuses, damage numbers, the labels of weapons on the ground) is placed by `Util/Overhead`, not by height alone. Height puts a text over a thing only while the camera looks across: from straight above, every height over a spot is the same spot on screen, so a name, a prompt and a bar stacked by height all landed on one another and on the figure.

- `Overhead.Point(camera, feet, height, girth)` is the point that high over the feet, moved up the screen by as much of `girth` (the thing's half width, with what room the text wants) as the camera looks down: nothing looking across, all of it from straight above. That is where the far rim of a body that wide and tall shows on screen.
- Lines are stacked up the screen (`Overhead.Up`), never up the world: the prompt over the name, a damage number rising.
- A seller's name and prompt are text on the screen (`SellerLabels`), like the HP bars and the ground weapons' labels, so they are the same size in every mode. As text in the world they were a few pixels high under the orthographic cameras.
- Damage numbers, "Parry!" and "Block" are still text in the world (`Util/FloatingText`), so they are smaller in the orthographic modes than in Behind.

## Verified

`./dev.sh camera-test` (headless): for each mode it places the camera, feeds W and D through the game's own input mapping (`HumanControls.MoveFor`) with a facing off the world axes, and projects the resulting movement onto a 1280x720 screen. W must move the character up the screen and D to the right, with at most 20% drift off that axis. It also checks that the closest zoom takes in less of the field and the farthest more, and that a length is as long on screen 8 further along the view as at the player in the three orthographic modes (1.000 of it) and shorter in Behind (0.505), and that a name hung over a figure shows above the figure's feet and head on screen in every mode (24 to 28 px at 720 high).

## Where it lives

- `GodotClient/scripts/Main/CameraMode.cs` and `ArenaCamera.cs`: camera placement per mode, mouse capture, mouse turn and tilt.
- `GodotClient/scripts/Player/PlayerControls.cs`: `HumanControls.MoveFor` and the turning modes.
- `Core/Ground.FromFacing`: movement relative to a facing, with tests.

## To tune by eye

- Angled offset (0, 16, 10); top-down height 20; behind distance 7.5 at a default tilt of 22 deg. In the orthographic modes the first two set the size of the view, not what is seen from where.
- Mouse turn 0.005 rad per pixel, tilt 0.004 rad per pixel, stick turn 3 rad/s, camera follow rate 12/s.
- Character turn rate 540 deg/s (`Core/Locomotion/Turning.cs`).
- Zoom range 0.5-2x, 1.12x per wheel notch.

## Open questions

- A quarter turn for the angled mode, the classic isometric diagonal: today it looks along the field, fortress to fortress.

- Camera collision in mode 3 once the arena has walls.
- Click-to-move, in the turning modes and in the others once there are obstacles.
- Remembering the camera mode and zoom between sessions (not for now).
