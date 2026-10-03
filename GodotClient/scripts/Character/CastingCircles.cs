using EverdawnKit.Magic;
using Godot;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// Everdawn's magic circles (EverdawnKit.Magic.MagicCircle) on a caster, as every machine draws them: one standing in
// front of it, facing the way it throws, from the start of a throw of magic until a moment after the last bolt leaves;
// and one lying under it for as long as it holds a spell on an area. Everdawn's are as big, and linger as long.
public partial class CastingCircles : Node3D
{
    private const float Radius = 0.7f;
    private const float Linger = 0.15f;

    // Just clear of the ground, so the ground does not cut through it.
    private const float UnderFeet = 0.03f;

    private MagicCircle? _held;

    // A throw facing `yaw` whose last bolt leaves `lastLeaves` seconds from now.
    public void Throw(float yaw, float lastLeaves)
    {
        var circle = new MagicCircle { Name = "Thrown" };
        AddChild(circle);
        var forward = Yaw.Forward(yaw);
        circle.Position = forward * Bolts.Leaves + Vector3.Up * Bolts.Height;
        circle.Basis = SpellMeshes.Along(Vector3.Zero, forward);
        circle.Setup(Radius);
        GetTree().CreateTimer(lastLeaves + Linger).Timeout += () =>
        {
            if (IsInstanceValid(circle))
            {
                circle.FadeOut();
            }
        };
    }

    public void Hold(bool holding)
    {
        if (holding && _held == null)
        {
            _held = new MagicCircle { Name = "Held", Position = Vector3.Up * UnderFeet };
            AddChild(_held);
            _held.Setup(Radius);
        }
        else if (!holding && _held != null)
        {
            _held.FadeOut();
            _held = null;
        }
    }
}
