using System;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// Attached with --camera-check. In every camera mode, W must move the character up the screen and D must move it
// right, through the same input mapping the game uses, and zooming in must bring the camera closer while zooming
// out moves it away. Then, once the HUD has laid out at 1280x720, its player frame and skill bar must sit on screen
// without overlapping. Prints [camera-check] / [layout-check] lines and quits (exit 1 on failure).
public partial class CameraSelfTest : Node
{
    // Facing used for the turning modes; anything off the world axes, so a mode ignoring the facing would fail.
    private const float Facing = 0.7f;
    private const float Step = 2f;

    // How far off the expected screen axis a move may drift, as a share of its length along the axis.
    private const float Tolerance = 0.2f;

    // Containers settle their sizes over the first frames; the layout is read after this many.
    private const int LayoutFrames = 5;
    private const float ScreenMargin = 8f;

    private readonly ArenaCamera _camera;
    private readonly Node3D _players;
    private readonly Hud _hud;
    private bool _cameraPassed;
    private int _framesSinceCamera = -1;

    public CameraSelfTest(ArenaCamera camera, Node3D players, Hud hud)
    {
        _camera = camera;
        _players = players;
        _hud = hud;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public CameraSelfTest()
        : this(null!, null!, null!)
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

        var player = _players.GetChildren().OfType<PlayerCharacter>().FirstOrDefault(p => p.IsMultiplayerAuthority());
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
            _camera.GlobalTransform = _camera.GoalTransform(player);

            var origin = _camera.UnprojectPosition(player.GlobalPosition);
            var up = _camera.UnprojectPosition(player.GlobalPosition + HumanControls.MoveFor(mode, new Vector2(0f, -1f), Facing) * Step) - origin;
            var right = _camera.UnprojectPosition(player.GlobalPosition + HumanControls.MoveFor(mode, new Vector2(1f, 0f), Facing) * Step) - origin;

            float normal = DistanceAtZoom(player, 1f);
            float near = DistanceAtZoom(player, 0f);
            float far = DistanceAtZoom(player, float.MaxValue);
            _camera.Zoom = 1f;

            bool passed = up.Y < 0f && Mathf.Abs(up.X) <= -up.Y * Tolerance
                && right.X > 0f && Mathf.Abs(right.Y) <= right.X * Tolerance
                && near < normal && normal < far;
            allPassed &= passed;
            GD.Print($"[camera-check] mode={mode} forward_on_screen=({up.X:F0},{up.Y:F0}) right_on_screen=({right.X:F0},{right.Y:F0}) "
                + $"distance_zoomed_in/normal/out={near:F1}/{normal:F1}/{far:F1} {(passed ? "ok" : "FAIL")}");
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

    // Zoom clamps, so 0 and float.MaxValue land on the nearest and farthest settings.
    private float DistanceAtZoom(PlayerCharacter player, float zoom)
    {
        _camera.Zoom = zoom;
        return _camera.GoalTransform(player).Origin.DistanceTo(player.GlobalPosition);
    }
}
