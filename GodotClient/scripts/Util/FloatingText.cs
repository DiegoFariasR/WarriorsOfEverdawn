using Godot;
using WarriorsOfEverdawn.Theme;

namespace WarriorsOfEverdawn.Util;

public static class FloatingText
{
    private const float StartHeight = 2.8f;
    private const float Rise = 1f;

    // Clear of the body and of the bar and statuses over it, as the camera sees them (Overhead).
    private const float Girth = 1.1f;
    public const float Duration = 0.8f;

    private const int FontSize = 64;
    private const int OutlineSize = 12;

    // `above` lifts it clear of another text spawned over the same anchor at the same moment; `scale` writes it
    // larger or smaller than the rest. It starts over the anchor and rises up the screen, whichever way the camera
    // looks: rising through the world, it would not move at all seen from straight above. It is drawn over
    // everything, floors included: whoever spawns one over a body on another floor than the camera's subject shows it
    // through the floor between (ArenaMap.OnSubjectsFloor).
    public static void Spawn(Node3D anchor, string text, Color color, float above = 0f, float scale = 1f)
    {
        var label = new Label3D
        {
            Text = text,
            Font = UiTheme.Numbers,
            Modulate = color,
            OutlineModulate = Colors.Black,
            FontSize = Mathf.RoundToInt(FontSize * scale),
            OutlineSize = Mathf.RoundToInt(OutlineSize * scale),
            PixelSize = 0.01f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
        };
        anchor.GetTree().CurrentScene.AddChild(label);
        var camera = anchor.GetViewport().GetCamera3D();
        var up = Overhead.Up(camera);
        label.GlobalPosition = Overhead.Point(camera, anchor.GlobalPosition, StartHeight, Girth) + up * above;

        var tween = label.CreateTween().SetParallel();
        tween.TweenProperty(label, "global_position", label.GlobalPosition + up * Rise, Duration);
        tween.TweenProperty(label, "modulate:a", 0f, Duration);
        tween.TweenProperty(label, "outline_modulate:a", 0f, Duration);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }
}
