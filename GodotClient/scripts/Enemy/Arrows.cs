using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Enemy;

// Every arrow in flight. The host looses them and decides what they hit; every machine flies its own copy of each
// from the same start along the same straight line, so an arrow costs one message to loose and one more only if it
// hits, and a wall or anything else solid stops them, on every machine alike. Present on every machine at the same path, for its RPCs. Design: Docs/Design/combat.md.
public partial class Arrows : Node
{
    public const string NodeName = "Arrows";

    // Where an arrow leaves the archer: about the bow's height, a little in front of the chest.
    public const float Height = 1.2f;
    public const float Leaves = 0.5f;

    private readonly Dictionary<int, ArrowFlight> _flights = new();
    private int _nextId;

    // On every machine, as an arrow starts flying.
    public static event Action? Loosed;

    // On every machine, as one ends, whatever ended it: where.
    public static event Action<Vector3>? Ended;

    // Host only: the player an arrow hit and the damage it took.
    public static event Action<PlayerCharacter, int>? HitPlayer;

    // How long the oldest arrow in flight has been flying; 0 with none.
    public float OldestFlight => Flight.Oldest(_flights.Values);

    public static Arrows In(SceneTree tree) => tree.CurrentScene.GetNode<Arrows>(NodeName);

    // Host only. Direction is flattened onto the ground: arrows fly level. `damage` is what it deals where it
    // hits, by the state its archer loosed it in, and `chest` the archer's middle at the height arrows fly at.
    public void Loose(SkillDefinition attack, int damage, Vector3 chest, Vector3 direction)
    {
        var level = Yaw.Flat(direction);
        if (attack.Projectile == null || level.LengthSquared() < 1e-6f)
        {
            GD.PushError($"[Arrows] {attack.Id} looses no projectile, or has nowhere to aim ({direction}); no arrow");
            return;
        }

        Rpc(MethodName.Fly, _nextId++, attack.Id, damage, chest, level.Normalized());
    }

    public override void _PhysicsProcess(double delta)
    {
        var space = GetViewport().World3D.DirectSpaceState;
        foreach (var (id, flight) in _flights.ToList())
        {
            var step = flight.Advance(space, (float)delta);

            // The world stops it, the same on every machine, before any body further along the step is looked at.
            if (step.Wall is { } stopped)
            {
                flight.StopAt(stopped);
                Remove(id);
                continue;
            }

            flight.Node.GlobalPosition = step.After;

            // The latest position each player reported, as for skeletons' swings (Docs/Design/multiplayer.md).
            if (Multiplayer.IsServer())
            {
                var map = ArenaMap.In(GetTree());
                var victim = PlayerCharacter.All(GetTree())
                    .FirstOrDefault(p => EnemyCharacter.MayStrike(p, p.NetPosition, step.After.Y - Height, map) && Projectiles.Hits(Yaw.ToGround(step.Before), Yaw.ToGround(step.After), Yaw.ToGround(p.NetPosition), BodySize.Radius, flight.Projectile));
                if (victim != null)
                {
                    // Guarded against where it was loosed from; a parry stops it like a block, with no one to stagger.
                    victim.Vitals.TakeAttack(flight.Damage, flight.From, DamageTypes.MaskOf(flight.Attack.Types));
                    HitPlayer?.Invoke(victim, flight.Damage);
                    Rpc(MethodName.End, id);
                    continue;
                }
            }

            // Every machine ends a miss on its own at the same distance.
            if (flight.AtFullDistance)
            {
                Remove(id);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Fly(int id, string attackId, int damage, Vector3 chest, Vector3 direction)
    {
        var from = chest + direction * Leaves;
        SkillDefinition attack;
        try
        {
            attack = Enemies.AttackById(attackId);
        }
        catch (KeyNotFoundException e)
        {
            GD.PushError($"[Arrows] {e.Message}");
            return;
        }

        // Without the toon look a player's arrow wears: whether a skeleton's should is not decided.
        var node = Flight.Arrow(direction, toonLook: false);
        AddChild(node);
        _flights[id] = new ArrowFlight(attack, damage, chest, from, direction, node);
        node.GlobalPosition = from;
        Loosed?.Invoke();
    }

    // A hit, from the host. The arrow may already be gone here if this machine flew it out of range first.
    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void End(int id) => Remove(id);

    private void Remove(int id)
    {
        if (_flights.Remove(id, out var flight))
        {
            flight.Node.QueueFree();
            Ended?.Invoke(flight.Position);
        }
    }

    private sealed class ArrowFlight : Flight
    {
        public ArrowFlight(SkillDefinition attack, int damage, Vector3 chest, Vector3 from, Vector3 direction, Node3D node)
            : base(attack.Projectile!, chest, from, direction, node)
        {
            Attack = attack;
            Damage = damage;
        }

        public SkillDefinition Attack { get; }

        public int Damage { get; }
    }
}
