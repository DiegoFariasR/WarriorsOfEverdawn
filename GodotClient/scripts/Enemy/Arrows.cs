using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Enemy;

// Every arrow in flight. The host looses them and decides what they hit; every machine flies its own copy of each
// from the same start along the same straight line, so an arrow costs one message to loose and one more only if it
// hits. Present on every machine at the same path, for its RPCs. Design: Docs/Design/combat.md.
public partial class Arrows : Node
{
    public const string NodeName = "Arrows";

    private readonly Dictionary<int, Flight> _flights = new();
    private int _nextId;

    // On every machine, as an arrow starts flying.
    public static event Action? Loosed;

    // Host only: the player an arrow hit and the damage it took.
    public static event Action<PlayerCharacter, int>? HitPlayer;

    public int InFlight => _flights.Count;

    // How long the oldest arrow in flight has been flying; 0 with none.
    public float OldestFlight => _flights.Values.Select(f => f.Age).DefaultIfEmpty(0f).Max();

    public static Arrows In(SceneTree tree) => tree.CurrentScene.GetNode<Arrows>(NodeName);

    // Host only. Direction is flattened onto the ground: arrows fly level. `damage` is what it deals where it
    // hits, by the state its archer loosed it in.
    public void Loose(SkillDefinition attack, int damage, Vector3 from, Vector3 direction)
    {
        var level = new Vector3(direction.X, 0f, direction.Z);
        if (attack.Projectile == null || level.LengthSquared() < 1e-6f)
        {
            GD.PushError($"[Arrows] {attack.Id} looses no projectile, or has nowhere to aim ({direction}); no arrow");
            return;
        }

        Rpc(MethodName.Fly, _nextId++, attack.Id, damage, from, level.Normalized());
    }

    public override void _PhysicsProcess(double delta)
    {
        foreach (var (id, flight) in _flights.ToList())
        {
            var projectile = flight.Attack.Projectile!;
            var before = flight.Position;
            flight.Age += (float)delta;
            flight.Travelled = Mathf.Min(flight.Travelled + projectile.Speed * (float)delta, projectile.MaxDistance);
            flight.Node.GlobalPosition = flight.Position;

            // The latest position each player reported, as for skeletons' swings (Docs/Design/multiplayer.md).
            if (Multiplayer.IsServer())
            {
                var map = ArenaMap.In(GetTree());
                var victim = GetTree().GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>()
                    .FirstOrDefault(p => !p.IsDowned && !map.IsSafe(p.NetPosition) && Projectiles.Hits(Yaw.ToGround(before), Yaw.ToGround(flight.Position), Yaw.ToGround(p.NetPosition), BodySize.Radius, projectile));
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
            if (flight.Travelled >= projectile.MaxDistance)
            {
                Remove(id);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Fly(int id, string attackId, int damage, Vector3 from, Vector3 direction)
    {
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

        var node = Assets.InstantiateAtOrigin(CombatVisuals.ArrowModel);

        // The model's point is its -Y end.
        var along = -direction;
        node.Basis = new Basis(along.Cross(Vector3.Up), along, Vector3.Up);
        AddChild(node);
        _flights[id] = new Flight(attack, damage, from, direction, node);
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
        }
    }

    private sealed class Flight
    {
        public Flight(SkillDefinition attack, int damage, Vector3 from, Vector3 direction, Node3D node)
        {
            Attack = attack;
            Damage = damage;
            From = from;
            Direction = direction;
            Node = node;
        }

        public SkillDefinition Attack { get; }

        public int Damage { get; }

        public Vector3 From { get; }

        public Vector3 Direction { get; }

        public Node3D Node { get; }

        public float Travelled { get; set; }

        public float Age { get; set; }

        public Vector3 Position => From + Direction * Travelled;
    }
}
