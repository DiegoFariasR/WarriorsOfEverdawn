using System;
using Godot;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Dev;

// Attached with --camera-check. In every camera mode, W must move the character up the screen and D must move it
// right, through the same input mapping the game uses, and zooming in must take in less of the field while zooming
// out takes in more. In the orthographic modes a length must be as long on screen far off as it is at the player,
// and in Behind shorter. A name hung over a figure (Overhead) must show above the figure's feet and head on
// screen in every mode. Then, once the HUD has laid out at 1280x720, its player frame and skill bar must sit on screen
// without overlapping. Prints [camera-check] / [layout-check] lines and quits (exit 1 on failure).
public partial class CameraSelfTest : Node
{
    // Facing used for the turning modes; anything off the world axes, so a mode ignoring the facing would fail.
    private const float Facing = 0.7f;
    private const float Step = 2f;

    // How far off the expected screen axis a move may drift, as a share of its length along the axis.
    private const float Tolerance = 0.2f;

    // How far past the player, along the view, a length is measured again.
    private const float FurtherOff = 8f;

    // How far above a figure its name must show on screen, in pixels at 720 high.
    private const float LabelClear = 8f;

    // Containers settle their sizes over the first frames; the layout is read after this many.
    private const int LayoutFrames = 5;
    private const float ScreenMargin = 8f;

    private readonly ArenaCamera _camera;
    private readonly Hud _hud;
    private bool _cameraPassed;
    private int _framesSinceCamera = -1;

    public CameraSelfTest(ArenaCamera camera, Hud hud)
    {
        _camera = camera;
        _hud = hud;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public CameraSelfTest()
        : this(null!, null!)
    {
    }

    public override void _Process(double delta)
    {
        if (_framesSinceCamera >= 0)
        {
            if (++_framesSinceCamera >= LayoutFrames)
            {
                bool layoutPassed = CheckLayout();
                GetTree().Quit(_cameraPassed && layoutPassed ? 0 : 1);
                SetProcess(false);
            }

            return;
        }

        var player = PlayerCharacter.Local(GetTree());
        if (player == null)
        {
            return;
        }

        // The headless window defaults to a tiny size; a real one keeps the printed pixel offsets meaningful.
        GetWindow().Size = new Vector2I(1280, 720);
        player.AimYaw = Facing;
        _camera.ViewYaw = Facing;
        bool allPassed = true;
        foreach (var mode in Enum.GetValues<CameraMode>())
        {
            _camera.SetMode(mode);
            _camera.Snap(player);

            var origin = _camera.UnprojectPosition(player.GlobalPosition);
            var up = _camera.UnprojectPosition(player.GlobalPosition + HumanControls.MoveFor(mode, new Vector2(0f, -1f), Facing) * Step) - origin;
            var right = _camera.UnprojectPosition(player.GlobalPosition + HumanControls.MoveFor(mode, new Vector2(1f, 0f), Facing) * Step) - origin;

            // The same length, across the view, at the player and further along the view.
            var across = _camera.GlobalBasis.X * Step;
            var beyond = player.GlobalPosition - _camera.GlobalBasis.Z * FurtherOff;
            float here = _camera.UnprojectPosition(player.GlobalPosition + across).DistanceTo(origin);
            float there = _camera.UnprojectPosition(beyond + across).DistanceTo(_camera.UnprojectPosition(beyond));
            float sizeFurtherOff = there / here;
            bool sizeAsWanted = mode.IsOrthographic() ? Mathf.Abs(sizeFurtherOff - 1f) < 0.001f : sizeFurtherOff < 0.9f;

            // A seller's name, hung over the player as it is over a seller.
            var feet = player.GlobalPosition;
            var name = Overhead.Point(_camera, feet, SellerNpc.NameHeight, SellerNpc.Girth);
            float figureTop = Mathf.Min(_camera.UnprojectPosition(feet).Y, _camera.UnprojectPosition(feet + Vector3.Up * SellerNpc.NameHeight).Y);
            float nameAbove = figureTop - _camera.UnprojectPosition(name).Y;
            bool labelsClear = nameAbove >= LabelClear;

            float normal = ViewAtZoom(player, 1f);
            float near = ViewAtZoom(player, 0f);
            float far = ViewAtZoom(player, float.MaxValue);
            _camera.Zoom = 1f;

            bool passed = up.Y < 0f && Mathf.Abs(up.X) <= -up.Y * Tolerance
                && right.X > 0f && Mathf.Abs(right.Y) <= right.X * Tolerance
                && near < normal && normal < far
                && sizeAsWanted && labelsClear;
            allPassed &= passed;
            GD.Print($"[camera-check] mode={mode} orthographic={mode.IsOrthographic()} forward_on_screen=({up.X:F0},{up.Y:F0}) right_on_screen=({right.X:F0},{right.Y:F0}) "
                + $"size_further_off={sizeFurtherOff:F3} view_zoomed_in/normal/out={near:F1}/{normal:F1}/{far:F1} "
                + $"name_above_figure_px={nameAbove:F0} {(passed ? "ok" : "FAIL")}");
        }

        _cameraPassed = allPassed;
        _framesSinceCamera = 0;
    }

    private bool CheckLayout()
    {
        var screen = new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size).Grow(-ScreenMargin);
        var frame = _hud.PlayerFrame.GetGlobalRect();
        var skills = _hud.SkillBar.GetGlobalRect();
        bool passed = frame.HasArea() && skills.HasArea()
            && screen.Encloses(frame) && screen.Encloses(skills) && !frame.Intersects(skills);
        GD.Print($"[layout-check] screen={screen.Size.X + 2 * ScreenMargin:F0}x{screen.Size.Y + 2 * ScreenMargin:F0} "
            + $"player_frame={Describe(frame)} skill_bar={Describe(skills)} {(passed ? "ok" : "FAIL")}");
        return passed;
    }

    private static string Describe(Rect2 rect) => $"({rect.Position.X:F0},{rect.Position.Y:F0} {rect.Size.X:F0}x{rect.Size.Y:F0})";

    // How much of the field the view takes in at this zoom. Zoom clamps, so 0 and float.MaxValue land on the
    // nearest and farthest settings.
    private float ViewAtZoom(PlayerCharacter player, float zoom)
    {
        _camera.Zoom = zoom;
        return _camera.ViewHeightAt(player);
    }
}
