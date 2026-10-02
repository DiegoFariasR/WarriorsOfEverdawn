using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Loot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Player;
using WarriorsOfEverdawn.Theme;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// Something on the ground to be walked over: a pile of gold of Amount coins, or a magic orb (Amount 1).
public sealed record GroundLoot(int Id, LootKind Kind, int Amount, Vector3 Position);

// What monsters leave behind. The host decides all of it: every player gets a monster's souls as it dies; its gold
// falls in a pile where it died, now and then with a magic orb over it, and each goes, when any player walks up to
// it, to every player. Co-op among friends, so nobody races anybody for it. Present on every machine at the same
// path, for its RPCs. Design: Docs/Design/combat.md.
public partial class Loot : Node3D
{
    public const string NodeName = "Loot";

    // The coin stacks are a hand high as modelled; drawn bigger, they can be seen from the camera's height.
    private const float PileScale = 1.8f;

    // Godot's Randf can return exactly 1, which a roll never is.
    private const float BelowOne = 0.999999f;

    private static readonly Color OrbText = new(0.85f, 0.95f, 1f);

    // An orb falls with its monster's gold and is picked up in the same step: its text goes over the gold's.
    private const float OrbTextAbove = 0.7f;

    private static readonly (int UpTo, string Model)[] PileModels =
    {
        (3, "res://assets/props/Money_Coins_Stack_Small.glb"),
        (6, "res://assets/props/Money_Coins_Stack_Medium.glb"),
        (int.MaxValue, "res://assets/props/Money_Coins_Stack_Large.glb"),
    };

    private readonly Dictionary<int, Lying> _lying = new();
    private readonly RandomNumberGenerator _random = new();
    private int _nextId;

    // On every machine, as something falls.
    public event Action<GroundLoot>? Dropped;

    // On every machine, as it is picked up, with the peer who walked up to it.
    public event Action<GroundLoot, long>? Taken;

    public IEnumerable<GroundLoot> OnGround => _lying.Values.Select(l => l.Loot);

    // Host only, for test sessions: every monster's chance of an orb, in place of its own (--orb-chance).
    public float? OrbChance { get; set; }

    public static Loot In(SceneTree tree) => tree.CurrentScene.GetNode<Loot>(NodeName);

    public override void _EnterTree() => EnemyCharacter.Died += OnEnemyDied;

    public override void _ExitTree() => EnemyCharacter.Died -= OnEnemyDied;

    // Host: a machine that has just joined learns what already lies on the ground.
    public void SendAllTo(long peer)
    {
        foreach (var loot in OnGround)
        {
            RpcId(peer, MethodName.Place, loot.Id, (int)loot.Kind, loot.Amount, loot.Position);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        var players = GetTree().GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>().ToList();
        foreach (var lying in _lying.Values.ToList())
        {
            lying.Age += (float)delta;
            if (lying.Age < LootRules.SettleTime)
            {
                continue;
            }

            // The position each player last reported, as for hits on players (Docs/Design/multiplayer.md).
            var taker = players.FirstOrDefault(p => !p.IsDowned && Flat(p.NetPosition - lying.Loot.Position) <= LootRules.PickupRadius);
            if (taker == null)
            {
                continue;
            }

            foreach (var player in players)
            {
                Earn(player.Vitals, lying.Loot);
            }

            Rpc(MethodName.Take, lying.Loot.Id, taker.PeerId);
        }
    }

    private static void Earn(PlayerVitals vitals, GroundLoot loot)
    {
        switch (loot.Kind)
        {
            case LootKind.Gold:
                vitals.EarnGold(loot.Amount);
                break;
            case LootKind.Orb:
                vitals.EarnOrbs(loot.Amount);
                break;
            default:
                throw new InvalidOperationException($"Nothing is earned from loot of kind {loot.Kind}");
        }
    }

    private void OnEnemyDied(EnemyCharacter enemy)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        foreach (var player in GetTree().GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>())
        {
            player.Vitals.EarnSouls(enemy.Definition.Souls);
        }

        var at = new Vector3(enemy.GlobalPosition.X, 0f, enemy.GlobalPosition.Z);
        int gold = enemy.Definition.Gold.Roll(Roll());
        if (gold > 0)
        {
            Rpc(MethodName.Place, _nextId++, (int)LootKind.Gold, gold, at);
        }

        if (LootRules.DropsOrb(OrbChance ?? enemy.Definition.OrbChance, Roll()))
        {
            Rpc(MethodName.Place, _nextId++, (int)LootKind.Orb, 1, at);
        }
    }

    private float Roll() => Mathf.Min(_random.Randf(), BelowOne);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Place(int id, int kind, int amount, Vector3 at)
    {
        // A machine joining as something falls can hear of it twice: once with everyone, once in the catch-up.
        if (_lying.ContainsKey(id))
        {
            return;
        }

        var loot = new GroundLoot(id, (LootKind)kind, amount, at);
        var model = ModelFor(loot);
        model.Name = $"{loot.Kind}{id}";
        model.Position = at;
        AddChild(model);

        _lying[id] = new Lying(loot, model);
        Dropped?.Invoke(loot);
    }

    private static Node3D ModelFor(GroundLoot loot)
    {
        switch (loot.Kind)
        {
            case LootKind.Gold:
                var pile = Assets.InstantiateAtOrigin(PileModels.First(m => loot.Amount <= m.UpTo).Model);
                pile.Scale = Vector3.One * PileScale;
                return pile;
            case LootKind.Orb:
                return MagicOrb.Create();
            default:
                throw new InvalidOperationException($"No model for loot of kind {loot.Kind}");
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Take(int id, long byPeer)
    {
        if (!_lying.Remove(id, out var lying))
        {
            GD.PushError($"[Loot] loot {id}, picked up by peer {byPeer}, is not on the ground on this machine");
            return;
        }

        lying.Model.QueueFree();
        Taken?.Invoke(lying.Loot, byPeer);
        if (PlayerCharacter.Find(GetTree(), byPeer) is { } taker)
        {
            if (lying.Loot.Kind == LootKind.Orb)
            {
                FloatingText.Spawn(taker, "+1 orb", OrbText, OrbTextAbove);
            }
            else
            {
                FloatingText.Spawn(taker, $"+{lying.Loot.Amount}", UiTheme.GoldHi);
            }
        }
    }

    private static float Flat(Vector3 offset) => new Vector2(offset.X, offset.Z).Length();

    private sealed class Lying
    {
        public Lying(GroundLoot loot, Node3D model)
        {
            Loot = loot;
            Model = model;
        }

        public GroundLoot Loot { get; }

        public Node3D Model { get; }

        public float Age { get; set; }
    }
}
