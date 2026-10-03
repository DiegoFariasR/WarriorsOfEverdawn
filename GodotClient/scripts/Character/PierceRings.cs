using EverdawnKit.Magic;
using Godot;

namespace WarriorsOfEverdawn.Character;

// Rings of air left along the line of a piercing blow or a flying arrow: each stands across the line, opens wider as it
// fades and frees itself, so the oldest, furthest back, are the widest. A thrust lays a row of them from the hand to
// its point as it lands, widest by the hand; an arrow leaves one behind it every so far (RingWake). Looks only.
// Design: Docs/Design/combat.md, "Thrusts and lunges".
public partial class PierceRings : Node3D
{
    public const string NodeName = "PierceRings";

    // A thrust's row: this many, from this wide by the hand to this wide at the point, laid one after another from
    // the hand out over this long.
    public const int RowCount = 5;
    private const float RowWidest = 0.36f;
    private const float RowNarrowest = 0.1f;
    private const float RowLaidOver = 0.08f;

    // A thrust's ring opens to this many times its width as it fades over Lifetime; an arrow's, which start small,
    // the more, so the row it leaves behind it widens like a cone.
    private const float ThrustOpening = 1.6f;
    public const float ArrowOpening = 4f;
    private const float Lifetime = 0.32f;
    private const float Alpha = 0.75f;

    // Thin, as a line drawn round the blow.
    private static readonly TorusMesh UnitRing = new() { InnerRadius = 0.9f, OuterRadius = 1f, Rings = 32, RingSegments = 4 };

    // Rings laid on this machine, and rows of them for thrusts.
    public int Laid { get; private set; }

    public int Rows { get; private set; }

    public static PierceRings In(SceneTree tree) => tree.CurrentScene.GetNode<PierceRings>(NodeName);

    // One ring at `at`, across `direction`, `radius` wide as it appears and `opening` times that as it goes, after
    // `delay` seconds.
    public void Ring(Vector3 at, Vector3 direction, float radius, float opening, Color colour, float delay = 0f)
    {
        Laid++;
        var material = MagicMaterials.Glow(colour, Alpha, emission: 0.6f);
        var ring = new MeshInstance3D
        {
            Mesh = UnitRing,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Basis = SpellMeshes.Along(Vector3.Zero, direction).Scaled(Vector3.One * radius),
            Visible = delay <= 0f,
        };
        AddChild(ring);
        ring.GlobalPosition = at;

        var opened = ring.Scale * opening;
        var tween = ring.CreateTween();
        if (delay > 0f)
        {
            tween.TweenInterval(delay);
            tween.TweenCallback(Callable.From(() => ring.Visible = true));
        }

        tween.TweenProperty(ring, "scale", opened, Lifetime).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(material, "albedo_color:a", 0f, Lifetime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(ring.QueueFree));
    }

    // A thrust's row along the line from `from` (by the hand) to `to` (its point).
    public void Row(Vector3 from, Vector3 to, Color colour)
    {
        Rows++;
        var direction = to - from;
        for (int i = 0; i < RowCount; i++)
        {
            float t = (float)i / (RowCount - 1);
            Ring(from.Lerp(to, t), direction, Mathf.Lerp(RowWidest, RowNarrowest, t), ThrustOpening, colour, delay: t * RowLaidOver);
        }
    }
}

// On a flying arrow: a ring left where it is every Spacing of its flight, into the rings that stay where they are laid.
public partial class RingWake : Node3D
{
    private const float Spacing = 0.9f;
    private const float Radius = 0.07f;

    private Vector3? _last;
    private float _travelled;

    public Color Colour { get; set; } = Colors.White;

    public override void _Process(double delta)
    {
        var now = GlobalPosition;
        if (_last is not { } last)
        {
            _last = now;
            return;
        }

        var moved = now - last;
        _last = now;
        _travelled += moved.Length();
        if (_travelled < Spacing || moved.LengthSquared() < 1e-8f)
        {
            return;
        }

        _travelled = 0f;
        PierceRings.In(GetTree()).Ring(now, moved, Radius, PierceRings.ArrowOpening, Colour);
    }
}
