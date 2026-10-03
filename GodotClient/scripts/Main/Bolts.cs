using System;
using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Magic;
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
// body. Util/Flight flies them, as it does a skeleton's arrows (Enemy/Arrows), which the host decides.
// Design: Docs/Design/magic.md.
public partial class Bolts : Node3D
{
    public const string NodeName = "Bolts";

    // Chest height: where a bolt leaves the staff hand and where it meets a body.
    public const float Height = 1f;

    // It leaves the hand this far in front of the caster's chest.
    public const float Leaves = 0.6f;

    // Everdawn's trail is a tenth of a metre to a ball of 0.36: each bolt's is as big against it.
    private const float TrailShare = 0.1f / 0.36f;

    // One that a wall stops ends this far short of it, so what it bursts into starts on its own side of the wall.
    private const float ShortOfWall = 0.05f;

    private readonly Dictionary<(long Caster, int Id), BoltFlight> _flights = new();

    // On every machine, as one starts flying: who threw it and with what.
    public static event Action<PlayerCharacter, SkillDefinition>? Loosed;

    // On the caster's machine: one of its own met a body.
    public static event Action<SkillDefinition>? Landed;

    // On every machine, as one that bursts ends: who threw it and with what.
    public static event Action<PlayerCharacter, SkillDefinition>? Burst;

    // On every machine, as any ends, whatever ended it: what it was and where.
    public static event Action<SkillDefinition, Vector3>? Ended;

    // How long the oldest in flight has been flying; 0 with none.
    public float OldestFlight => Flight.Oldest(_flights.Values);

    // Bursts still fading where bolts ended.
    public int BurstsShowing { get; private set; }

    public static Bolts In(SceneTree tree) => tree.CurrentScene.GetNode<Bolts>(NodeName);

    // On every machine, from the caster's own (PlayerCharacter.LooseBolt): `id` tells the caster's bolts apart,
    // and `chest` is the caster's middle at the height things fly at.
    public void Fly(PlayerCharacter caster, int id, SkillDefinition skill, Vector3 chest, float yaw)
    {
        if (skill.Projectile == null)
        {
            GD.PushError($"[Bolts] {skill.Id} throws nothing; no bolt");
            return;
        }

        var direction = Yaw.Forward(yaw);
        var from = chest + direction * Leaves;
        var node = Thrown(skill, direction);
        AddChild(node);
        node.GlobalPosition = from;
        if (!IsArrow(skill) && skill.Element is { } element)
        {
            node.AddChild(new SpellTrail { Colour = ElementLooks.For(element).Primary, Radius = skill.Projectile.Radius * TrailShare, Into = this });
        }
        else if (IsArrow(skill))
        {
            node.AddChild(new RingWake { Colour = caster.Trail.Colour });
        }
        _flights[(caster.PeerId, id)] = new BoltFlight(caster, skill, chest, from, direction, node);
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
            var arrow = Flight.Arrow(direction, toonLook: true);
            if (skill.Element is { } enchantment)
            {
                foreach (var mesh in arrow.Meshes())
                {
                    mesh.MaterialOverlay = ElementLooks.Enchantment(enchantment);
                }
            }

            return arrow;
        }

        var element = skill.Element ?? throw new InvalidOperationException($"{skill.Id} throws magic of no element");
        var projectile = skill.Projectile!;
        var node = ElementLooks.Made(element, skill.Projectiles > 1
            ? SpellMeshes.Bipyramid(projectile.Radius, projectile.Radius * 4f)
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
            var step = flight.Advance(space, (float)delta);
            flight.Node.GlobalPosition = step.After;

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
            if (step.Wall is { } stopped)
            {
                EndHere(key, flight, stopped - flight.Direction * ShortOfWall, mine);
                continue;
            }

            // A body it touches on the way: the caster's machine deals with it and tells the others it ended there.
            if (mine && flight.Caster.StrikeWith(flight.Skill, step.Before, step.After))
            {
                Landed?.Invoke(flight.Skill);
                flight.Caster.EndBoltEverywhere(key.Id, step.After);
                continue;
            }

            // Every machine ends a miss on its own at the same distance.
            if (flight.AtFullDistance)
            {
                EndHere(key, flight, step.After, mine);
            }
        }
    }

    // Ended by the world and not by a body: every machine sees that for itself.
    private void EndHere((long Caster, int Id) key, BoltFlight flight, Vector3 at, bool mine)
    {
        _flights.Remove(key);
        Finish(flight, at);
        if (mine && flight.Skill.BlastRadius > 0f)
        {
            flight.Caster.BlastAt(flight.Skill, at);
        }
    }

    // The bolt goes, and where it ended its element bursts as Everdawn's spells land (EverdawnKit.Magic.SpellImpact):
    // a bolt's explosive puff, a dart's shatter, and for a ball that bursts an explosion as wide as what it catches. A
    // plain arrow just goes.
    private void Finish(BoltFlight flight, Vector3 at)
    {
        flight.Node.QueueFree();
        Ended?.Invoke(flight.Skill, at);
        if (IsArrow(flight.Skill) || flight.Skill.Element is not { } element)
        {
            return;
        }

        float blast = flight.Skill.BlastRadius;
        var impact = new SpellImpact();
        AddChild(impact);
        impact.GlobalPosition = at;
        BurstsShowing++;
        impact.TreeExiting += () => BurstsShowing--;
        if (blast > 0f)
        {
            Burst?.Invoke(flight.Caster, flight.Skill);
        }

        var kind = flight.Skill.Projectiles > 1 ? ImpactKind.Shatter : ImpactKind.Explosive;
        impact.Play(kind, ElementLooks.For(element).Primary, blast > 0f ? SpellImpact.ExplosiveScaleFor(blast) : 1f, ElementLooks.Dress(element));
    }

    private sealed class BoltFlight : Flight
    {
        public BoltFlight(PlayerCharacter caster, SkillDefinition skill, Vector3 chest, Vector3 from, Vector3 direction, Node3D node)
            : base(skill.Projectile!, chest, from, direction, node)
        {
            Caster = caster;
            Skill = skill;
        }

        public PlayerCharacter Caster { get; }

        public SkillDefinition Skill { get; }
    }
}
