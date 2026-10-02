using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// Every bolt and dart a staff has thrown that is still in flight. The caster's machine looses them and decides what
// they hit, as it does for its swings; every machine flies its own copy of each from the same start along the same
// straight line, and stops it at a wall on its own, so one costs a message to loose and one more only if it hits a
// body. Looks apart, a skeleton's arrows (Enemy/Arrows) are the same thing decided by the host.
// Design: Docs/Design/magic.md.
public partial class Bolts : Node3D
{
    public const string NodeName = "Bolts";

    // Chest height: where a bolt leaves the staff hand and where it meets a body.
    public const float Height = 1f;

    private const float BurstTime = 0.25f;

    private readonly Dictionary<(long Caster, int Id), Flight> _flights = new();

    // On every machine, as one starts flying: who threw it and with what.
    public static event Action<PlayerCharacter, SkillDefinition>? Loosed;

    // On the caster's machine: one of its own met a body.
    public static event Action<SkillDefinition>? Landed;

    public int InFlight => _flights.Count;

    // How long the oldest in flight has been flying; 0 with none.
    public float OldestFlight => _flights.Values.Select(f => f.Age).DefaultIfEmpty(0f).Max();

    // Bursts still fading where bolts ended.
    public int BurstsShowing { get; private set; }

    public static Bolts In(SceneTree tree) => tree.CurrentScene.GetNode<Bolts>(NodeName);

    // On every machine, from the caster's own (PlayerCharacter.LooseBolt): `id` tells the caster's bolts apart.
    public void Fly(PlayerCharacter caster, int id, SkillDefinition skill, Vector3 from, float yaw)
    {
        if (skill.Projectile == null || skill.Element is not { } element)
        {
            GD.PushError($"[Bolts] {skill.Id} throws nothing, or nothing of an element; no bolt");
            return;
        }

        var direction = Yaw.Forward(yaw);
        var node = Thrown(skill, element, direction);
        AddChild(node);
        node.GlobalPosition = from;
        _flights[(caster.PeerId, id)] = new Flight(caster, skill, element, from, direction, node);
        Loosed?.Invoke(caster, skill);
    }

    // What a skill throws, heading that way: a bolt is Everdawn's ball; a volley's dart its bipyramid, point first.
    public static MeshInstance3D Thrown(SkillDefinition skill, Element element, Vector3 direction)
    {
        var projectile = skill.Projectile!;
        var node = ElementLooks.Made(element, skill.Projectiles > 1
            ? ElementLooks.Bipyramid(projectile.Radius, projectile.Radius * 4f)
            : new SphereMesh { Radius = projectile.Radius, Height = projectile.Radius * 2f, RadialSegments = 16, Rings = 8 });
        node.Basis = new Basis(direction.Cross(Vector3.Up), direction, Vector3.Up);
        return node;
    }

    // From the caster's machine: that one met a body here.
    public void End(long casterPeer, int id, Vector3 at)
    {
        if (_flights.Remove((casterPeer, id), out var flight))
        {
            Burst(flight, at);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        var space = GetWorld3D().DirectSpaceState;
        foreach (var (key, flight) in _flights.ToList())
        {
            var projectile = flight.Skill.Projectile!;
            var before = flight.Position;
            flight.Age += (float)delta;
            flight.Travelled = Mathf.Min(flight.Travelled + projectile.Speed * (float)delta, projectile.MaxDistance);
            var after = flight.Position;
            flight.Node.GlobalPosition = after;

            // Walls and whatever else stands in the world stop it, the same on every machine.
            var wall = space.IntersectRay(PhysicsRayQueryParameters3D.Create(before, after, CollisionLayers.World));
            if (wall.Count > 0)
            {
                _flights.Remove(key);
                Burst(flight, wall["position"].AsVector3());
                continue;
            }

            // The caster may have left with its bolt in the air.
            if (!IsInstanceValid(flight.Caster))
            {
                _flights.Remove(key);
                flight.Node.QueueFree();
                continue;
            }

            if (flight.Caster.IsMultiplayerAuthority() && flight.Caster.StrikeWith(flight.Skill, before, after))
            {
                Landed?.Invoke(flight.Skill);
                flight.Caster.EndBoltEverywhere(key.Id, after);
                continue;
            }

            // Every machine ends a miss on its own at the same distance.
            if (flight.Travelled >= projectile.MaxDistance)
            {
                _flights.Remove(key);
                Burst(flight, after);
            }
        }
    }

    // The bolt goes, and a burst of its element swells and is gone where it ended.
    private void Burst(Flight flight, Vector3 at)
    {
        flight.Node.QueueFree();
        float radius = flight.Skill.Projectile!.Radius;
        var burst = ElementLooks.Made(flight.Element, new SphereMesh { Radius = radius, Height = radius * 2f, RadialSegments = 12, Rings = 6 });
        AddChild(burst);
        burst.GlobalPosition = at;
        BurstsShowing++;
        var tween = burst.CreateTween();
        tween.TweenProperty(burst, "scale", Vector3.One * 2.6f, BurstTime * 0.4f);
        tween.TweenProperty(burst, "scale", Vector3.Zero, BurstTime * 0.6f);
        tween.TweenCallback(Callable.From(() =>
        {
            BurstsShowing--;
            burst.QueueFree();
        }));
    }

    private sealed class Flight
    {
        public Flight(PlayerCharacter caster, SkillDefinition skill, Element element, Vector3 from, Vector3 direction, Node3D node)
        {
            Caster = caster;
            Skill = skill;
            Element = element;
            From = from;
            Direction = direction;
            Node = node;
        }

        public PlayerCharacter Caster { get; }

        public SkillDefinition Skill { get; }

        public Element Element { get; }

        public Vector3 From { get; }

        public Vector3 Direction { get; }

        public Node3D Node { get; }

        public float Travelled { get; set; }

        public float Age { get; set; }

        public Vector3 Position => From + Direction * Travelled;
    }
}
