using System;
using Godot;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Character;

// The spell a staff holds on an area, as every machine draws it: a ring on the ground round the area for as long as
// the spell is held, and a round of strikes inside it each cycle, in the element's own shape. Looks only: what the
// spell hits is the caster's machine's business (PlayerCharacter.AdvanceSwing).
public partial class SpellArea : Node3D
{
    private const float RingWidth = 0.1f;
    private const float RingHeight = 0.05f;
    private const float SkyHeight = 5f;

    private MeshInstance3D? _ring;
    private Node3D _strikes = null!;
    private Element _element;
    private AreaDefinition? _area;
    private float _cycle;
    private float _sinceStrikes;

    public bool Showing => _area != null;

    // Rounds of strikes drawn since the node was made.
    public int Rounds { get; private set; }

    // Strikes still to be seen.
    public int StrikesAlive => _strikes.GetChildCount();

    public override void _Ready()
    {
        // The area lies on the ground where the spell is aimed, not where the caster stands or turns.
        TopLevel = true;
        _strikes = new Node3D { Name = "Strikes" };
        AddChild(_strikes);
    }

    // Held from now on, a round of strikes every `cycle` seconds, the first at once.
    public void Hold(Element element, AreaDefinition area, float cycle)
    {
        if (_area == area && _element == element)
        {
            _cycle = cycle;
            return;
        }

        _ring?.QueueFree();
        _element = element;
        _area = area;
        _cycle = cycle;
        _sinceStrikes = cycle;
        _ring = ElementLooks.Made(element, new TorusMesh { InnerRadius = area.Radius - RingWidth, OuterRadius = area.Radius, Rings = 48, RingSegments = 6 });
        _ring.Name = "Ring";
        _ring.Position = Vector3.Up * RingHeight;
        AddChild(_ring);
    }

    public void MoveTo(Vector3 centre) => GlobalPosition = centre;

    // Let go: the ring goes, the strikes already falling play out.
    public void Release()
    {
        _ring?.QueueFree();
        _ring = null;
        _area = null;
    }

    public override void _Process(double delta)
    {
        if (_area == null)
        {
            return;
        }

        _sinceStrikes += (float)delta;
        if (_sinceStrikes < _cycle)
        {
            return;
        }

        _sinceStrikes = 0f;
        Rounds++;
        var look = ElementLooks.For(_element);
        for (int i = 0; i < look.StrikesPerCycle; i++)
        {
            Strike(look.Strike, look.Strike == SpellStrike.Ring ? Vector3.Zero : ElementLooks.SpotWithin(_area.Radius * 0.85f));
        }
    }

    private void Strike(SpellStrike shape, Vector3 at)
    {
        var mesh = ElementLooks.Made(_element, shape switch
        {
            SpellStrike.Pillar => new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.45f, Height = 2.6f, RadialSegments = 12 },
            SpellStrike.Spike => new CylinderMesh { TopRadius = 0f, BottomRadius = 0.45f, Height = 1.7f, RadialSegments = 6 },
            SpellStrike.Shard => ElementLooks.Bipyramid(0.22f, 1.1f),
            SpellStrike.Flash => new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.07f, Height = SkyHeight, RadialSegments = 6 },
            SpellStrike.Ring => new TorusMesh { InnerRadius = 0.8f, OuterRadius = 1f, Rings = 40, RingSegments = 8 },
            SpellStrike.Blob => new SphereMesh { Radius = 0.7f, Height = 1.4f, RadialSegments = 16, Rings = 8 },
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Not a strike"),
        });
        _strikes.AddChild(mesh);
        mesh.Rotation = new Vector3(0f, GD.Randf() * Mathf.Tau, 0f);

        // Each strike is one tween from its first frame to its last, and is gone when the tween ends.
        var tween = mesh.CreateTween();
        float size = ElementLooks.Jitter(1f, 0.2f);
        switch (shape)
        {
            case SpellStrike.Pillar:
                mesh.Position = at + Vector3.Up * 1.3f * size;
                mesh.Scale = new Vector3(size, 0.05f, size);
                tween.TweenProperty(mesh, "scale", Vector3.One * size, 0.1f);
                tween.TweenInterval(0.15f);
                tween.TweenProperty(mesh, "scale", new Vector3(0.05f, size, 0.05f), 0.2f);
                break;
            case SpellStrike.Spike:
                mesh.Scale = Vector3.One * size;
                mesh.Position = at + Vector3.Down * 0.9f;
                tween.TweenProperty(mesh, "position", at + Vector3.Up * 0.8f * size, 0.08f);
                tween.TweenInterval(0.25f);
                tween.TweenProperty(mesh, "position", at + Vector3.Down * 0.9f, 0.2f);
                break;
            case SpellStrike.Shard:
                mesh.Scale = Vector3.One * size;
                mesh.Position = at + Vector3.Up * SkyHeight;
                tween.TweenProperty(mesh, "position", at + Vector3.Up * 0.4f, 0.22f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
                tween.TweenProperty(mesh, "scale", Vector3.Zero, 0.12f);
                break;
            case SpellStrike.Flash:
                mesh.Position = at + Vector3.Up * SkyHeight / 2f;
                mesh.Scale = new Vector3(2.5f, 1f, 2.5f);
                tween.TweenProperty(mesh, "scale", new Vector3(0.2f, 1f, 0.2f), 0.16f);
                break;
            case SpellStrike.Ring:
                mesh.Position = at + Vector3.Up * 0.5f;
                mesh.Scale = Vector3.One * 0.3f;
                tween.TweenProperty(mesh, "scale", new Vector3(_area!.Radius, 0.6f, _area.Radius), _cycle);
                break;
            case SpellStrike.Blob:
                mesh.Position = at + Vector3.Up * 0.5f;
                mesh.Scale = Vector3.One * 0.15f;
                tween.TweenProperty(mesh, "scale", Vector3.One * size, 0.25f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
                tween.TweenInterval(0.3f);
                tween.TweenProperty(mesh, "scale", Vector3.Zero, 0.3f);
                break;
        }

        tween.TweenCallback(Callable.From(mesh.QueueFree));
    }
}
