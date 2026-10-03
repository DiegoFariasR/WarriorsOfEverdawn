using Godot;
using WarriorsOfEverdawn.Core.Level;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// "E - sit" or "E - lie down" over the free seat this machine's player can take, while E would take it: not while a
// seller is in reach (E trades), nor while it trades, is down or already rests. Text on the screen, placed each frame
// from where the seat is, as a seller's name is (SellerLabels).
public partial class SeatPrompt : Control
{
    public const string SitText = "E  -  sit";
    public const string LieText = "E  -  lie down";

    // Over the seat, about as high as a body sitting on it.
    private const float Height = 0.9f;
    private const float Girth = 0.5f;

    private readonly Label _label = UiTheme.MakeLabel(SitText, UiTheme.Words, 13, Colors.White, outline: 4);

    // The seat the prompt is over, or none; for the self-tests.
    public Seat? Shown { get; private set; }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        var tree = GetTree();
        var camera = GetViewport().GetCamera3D();
        var player = PlayerCharacter.Local(tree);
        Shown = player is { IsDowned: false, IsTrading: false, IsResting: false } && camera != null
            && tree.CurrentScene.GetNodeOrNull<Market>(Market.NodeName)?.InReachOf(player.GlobalPosition) == null
            ? tree.CurrentScene.GetNodeOrNull<Seating>(Seating.NodeName)?.InReachOf(player.GlobalPosition)
            : null;
        if (Shown is not { } seat)
        {
            _label.Visible = false;
            return;
        }

        _label.Text = seat.Kind == SeatKind.Sit ? SitText : LieText;
        _label.ResetSize();
        var feet = new Vector3(seat.Spot.X, seat.Spot.Y, seat.Spot.Z);
        var over = Overhead.Point(camera, feet, Height, Girth);
        _label.Visible = !camera!.IsPositionBehind(over);
        var at = camera.UnprojectPosition(over);
        _label.Position = new Vector2(at.X - _label.Size.X / 2f, at.Y - _label.Size.Y);
    }
}
