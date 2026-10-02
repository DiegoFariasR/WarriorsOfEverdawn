using Godot;
using WarriorsOfEverdawn.Theme;

namespace WarriorsOfEverdawn.Util;

public static class FloatingText
{
    private const float StartHeight = 2.8f;
    private const float Rise = 1f;
    public const float Duration = 0.8f;

    // `above` lifts it clear of another text spawned over the same anchor at the same moment.
    public static void Spawn(Node3D anchor, string text, Color color, float above = 0f)
    {
        var label = new Label3D
        {
            Text = text,
            Font = UiTheme.Numbers,
            Modulate = color,
            OutlineModulate = Colors.Black,
            FontSize = 64,
            OutlineSize = 12,
            PixelSize = 0.01f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
        };
        anchor.GetTree().CurrentScene.AddChild(label);
        label.GlobalPosition = anchor.GlobalPosition + Vector3.Up * (StartHeight + above);

        var tween = label.CreateTween().SetParallel();
        tween.TweenProperty(label, "global_position", label.GlobalPosition + Vector3.Up * Rise, Duration);
        tween.TweenProperty(label, "modulate:a", 0f, Duration);
        tween.TweenProperty(label, "outline_modulate:a", 0f, Duration);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }
}
