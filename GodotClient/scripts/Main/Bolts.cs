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

// Every bolt and dart a staff has thrown, and every arrow a player's bow has loosed, that is still in flight. The caster's machine looses them and decides what
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

    // On every machine, as one that bursts ends: who threw it and with what.
    public static event Action<PlayerCharacter, SkillDefinition>? Burst;

    public int InFlight => _flights.Count;

    // How long the oldest in flight has been flying; 0 with none.
    public float OldestFlight => _flights.Values.Select(f => f.Age).DefaultIfEmpty(0f).Max();

    // Bursts still fading where bolts ended.
    public int BurstsShowing { get; private set; }

    public static Bolts In(SceneTree tree) => tree.CurrentScene.GetNode<Bolts>(NodeName);

    // On every machine, from the caster's own (PlayerCharacter.LooseBolt): `id` tells the caster's bolts apart.
    public void Fly(PlayerCharacter caster, int id, SkillDefinition skill, Vector3 from, float yaw)
    {
        if (skill.Projectile == null)
        {
            GD.PushError($"[Bolts] {skill.Id} throws nothing; no bolt");
            return;
        }

        var direction = Yaw.Forward(yaw);
        var node = Thrown(skill, direction);
        AddChild(node);
        node.GlobalPosition = from;
        _flights[(caster.PeerId, id)] = new Flight(caster, skill, from, direction, node);
        Loosed?.Invoke(caster, skill);
    }

    // Whether what the skill throws is an arrow: a thing of wood and steel, not of an element.
    public static bool IsArrow(SkillDefinition skill) => DamageTypes.FamilyOf(skill.Type) == DamageFamily.Physical;

    // What a skill throws, heading that way: a bolt is Everdawn's ball; a volley's dart its bipyramid, point first;
    // a bow's is an arrow, with the bow's enchantment playing over it if it has one.
    public static Node3D Thrown(SkillDefinition skill, Vector3 direction)
    {
        if (IsArrow(skill))
        {
            var arrow = Assets.InstantiateAtOrigin(CombatVisuals.ArrowModel);
            ToonLook.ApplyToWeapon(arrow);
            if (skill.Element is { } enchantment)
            {
                foreach (var mesh in arrow.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false).OfType<MeshInstance3D>())
                {
                    mesh.MaterialOverlay = ElementLooks.Enchantment(enchantment);
                }
            }

            // The model's point is its -Y end.
            var along = -direction;
            arrow.Basis = new Basis(along.Cross(Vector3.Up), along, Vector3.Up);
            return arrow;
        }

        var element = skill.Element ?? throw new InvalidOperationException($"{skill.Id} throws magic of no element");
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
            Finish(flight, at);
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

            // The caster may have left with its bolt in the air.
            if (!IsInstanceValid(flight.Caster))
            {
                _flights.Remove(key);
                flight.Node.QueueFree();
                continue;
            }

            // Walls and whatever else stands in the world stop it, the same on every machine. One that bursts does
            // so there too, and the caster's machine decides what the burst catches.
            bool mine = flight.Caster.IsMultiplayerAuthority();
            var wall = space.IntersectRay(PhysicsRayQueryParameters3D.Create(before, after, CollisionLayers.World));
            if (wall.Count > 0)
            {
                EndHere(key, flight, wall["position"].AsVector3(), mine);
                continue;
            }

            // A body it touches on the way: the caster's machine deals with it and tells the others it ended there.
            if (mine && flight.Caster.StrikeWith(flight.Skill, before, after))
            {
                Landed?.Invoke(flight.Skill);
                flight.Caster.EndBoltEverywhere(key.Id, after);
                continue;
            }

            // Every machine ends a miss on its own at the same distance.
            if (flight.Travelled >= projectile.MaxDistance)
            {
                EndHere(key, flight, after, mine);
            }
        }
    }

    // Ended by the world and not by a body: every machine sees that for itself.
    private void EndHere((long Caster, int Id) key, Flight flight, Vector3 at, bool mine)
    {
        _flights.Remove(key);
        Finish(flight, at);
        if (mine && flight.Skill.BlastRadius > 0f)
        {
            flight.Caster.BlastAt(flight.Skill, at);
        }
    }

    // The bolt goes, and a burst of its element swells and is gone where it ended: a puff for a bolt, and for a ball
    // that bursts, as wide as what it catches. A plain arrow just goes.
    private void Finish(Flight flight, Vector3 at)
    {
        flight.Node.QueueFree();
        if (flight.Skill.Element is not { } element)
        {
            return;
        }

        float blast = flight.Skill.BlastRadius;
        float radius = flight.Skill.Projectile!.Radius;
        var burst = ElementLooks.Made(element, new SphereMesh { Radius = radius, Height = radius * 2f, RadialSegments = 16, Rings = 8 });
        AddChild(burst);
        burst.GlobalPosition = at;
        BurstsShowing++;
        if (blast > 0f)
        {
            Burst?.Invoke(flight.Caster, flight.Skill);
        }

        var tween = burst.CreateTween();
        tween.TweenProperty(burst, "scale", Vector3.One * (blast > 0f ? blast / radius : 2.6f), BurstTime * 0.4f);
        tween.TweenProperty(burst, "scale", Vector3.Zero, BurstTime * 0.6f);
        tween.TweenCallback(Callable.From(() =>
        {
            BurstsShowing--;
            burst.QueueFree();
        }));
    }

    private sealed class Flight
    {
        public Flight(PlayerCharacter caster, SkillDefinition skill, Vector3 from, Vector3 direction, Node3D node)
        {
            Caster = caster;
            Skill = skill;
            From = from;
            Direction = direction;
            Node = node;
        }

        public PlayerCharacter Caster { get; }

        public SkillDefinition Skill { get; }

        public Vector3 From { get; }

        public Vector3 Direction { get; }

        public Node3D Node { get; }

        public float Travelled { get; set; }

        public float Age { get; set; }

        public Vector3 Position => From + Direction * Travelled;
    }
}
