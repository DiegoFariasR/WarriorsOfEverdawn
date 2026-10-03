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

// Something on the ground to be walked over: a pile of gold of Amount coins, a magic orb or a potion's charge (Amount 1).
public sealed record GroundLoot(int Id, LootKind Kind, int Amount, Vector3 Position);

// What monsters leave behind. The host decides all of it: every player gets a monster's souls as it dies; its gold
// falls in a pile where it died, now and then with a magic orb or a charge of the health potion over it, and each
// goes, when any player walks up to it, to every player. Co-op among friends, so nobody races anybody for it. A
// charge is picked up only while someone has room for it, so none is wasted. Present on every machine at the same
// path, for its RPCs. Design: Docs/Design/combat.md.
public partial class Loot : Node3D
{
    public const string NodeName = "Loot";

    // A coin is drawn a little smaller than it is modelled: at twice this it lay on the ground the size of a plate.
    private const float PileScale = 0.9f;

    private const string OneCoin = "res://assets/props/Money_Coins_Stack_Single.glb";

    // A treasure's orbs lie this far from its gold.
    private const float TreasureOrbsApart = 0.7f;

    // Godot's Randf can return exactly 1, which a roll never is.
    private const float BelowOne = 0.999999f;

    private static readonly Color OrbText = new(0.85f, 0.95f, 1f);
    private static readonly Color PotionText = new(1f, 0.45f, 0.45f);

    // An orb falls with its monster's gold and is picked up in the same step: its text goes over the gold's.
    private const float OrbTextAbove = 0.7f;

    // A pile shows its gold coin for coin, up to LootRules.MostCoinsShown: one coin, two in a row, three in a
    // triangle, then each further one on top of those three in turn, and the tenth on the middle of them
    // (Tools/gold_piles.py makes them from the one coin).
    private static string PileModel(int coins) => coins == 1 ? OneCoin : $"res://assets/props/Money_Coins_Pile_{coins}.glb";

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

    // Host only, for test sessions: every monster's chance of a potion's charge, in place of its orb chance
    // (--potion-chance).
    public float? PotionChance { get; set; }

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

        var players = PlayerCharacter.All(GetTree()).ToList();
        foreach (var lying in _lying.Values.ToList())
        {
            lying.Age += (float)delta;
            if (lying.Age < LootRules.SettleTime)
            {
                continue;
            }

            // The position each player last reported, as for hits on players (Docs/Design/multiplayer.md).
            var taker = players.FirstOrDefault(p => !p.IsDowned && Yaw.AcrossFloor(p.NetPosition, lying.Loot.Position) <= LootRules.PickupRadius);
            if (taker == null || (lying.Loot.Kind == LootKind.PotionCharge && players.All(p => p.Vitals.PotionFull)))
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
            case LootKind.PotionCharge:
                vitals.EarnPotionCharge();
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

        foreach (var player in PlayerCharacter.All(GetTree()))
        {
            player.Vitals.EarnSouls(enemy.Definition.Souls);
        }

        var at = enemy.GlobalPosition;
        int gold = enemy.Definition.Gold.Roll(Roll());
        if (gold > 0)
        {
            Rpc(MethodName.Place, _nextId++, (int)LootKind.Gold, gold, at);
        }

        if (LootRules.Drops(OrbChance ?? enemy.Definition.OrbChance, Roll()))
        {
            Rpc(MethodName.Place, _nextId++, (int)LootKind.Orb, 1, at);
        }

        // A charge is as rare as an orb, on a roll of its own, and lies a little apart from it.
        if (LootRules.Drops(PotionChance ?? enemy.Definition.OrbChance, Roll()))
        {
            Rpc(MethodName.Place, _nextId++, (int)LootKind.PotionCharge, 1, at + Yaw.Forward(LootRules.PileYaw(_nextId)) * TreasureOrbsApart);
        }
    }

    // Host only: a treasure's gold in one pile at the spot, and its orbs over it, a little apart.
    public void LeaveTreasure(Vector3 at, Cost treasure)
    {
        if (treasure.Gold > 0)
        {
            Rpc(MethodName.Place, _nextId++, (int)LootKind.Gold, treasure.Gold, at);
        }

        for (int orb = 0; orb < treasure.Orbs; orb++)
        {
            Rpc(MethodName.Place, _nextId++, (int)LootKind.Orb, 1, at + Yaw.Forward(LootRules.PileYaw(orb + 1)) * TreasureOrbsApart);
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
        model.AddToGroup(ArenaMap.OnAFloor);
        AddChild(model);

        _lying[id] = new Lying(loot, model);
        Dropped?.Invoke(loot);
    }

    // The pile that much gold lies in, as it is drawn on the ground, turned that far round.
    public static Node3D Pile(int gold, float yaw)
    {
        var pile = Assets.InstantiateAtOrigin(PileModel(LootRules.CoinsShown(gold)));
        pile.Scale = Vector3.One * PileScale;
        pile.Rotation = new Vector3(0f, yaw, 0f);
        return pile;
    }

    private static Node3D ModelFor(GroundLoot loot)
    {
        switch (loot.Kind)
        {
            case LootKind.Gold:
                return Pile(loot.Amount, LootRules.PileYaw(loot.Id));
            case LootKind.Orb:
                return MagicOrb.Create();
            case LootKind.PotionCharge:
                return PotionDrop.Create();
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
        // What floats over the taker is drawn through floors, so only while it is on the camera's subject's floor.
        if (PlayerCharacter.Find(GetTree(), byPeer) is { } taker && ArenaMap.In(GetTree()).OnSubjectsFloor(taker.GlobalPosition))
        {
            if (lying.Loot.Kind == LootKind.Orb)
            {
                FloatingText.Spawn(taker, "+1 orb", OrbText, OrbTextAbove);
            }
            else if (lying.Loot.Kind == LootKind.PotionCharge)
            {
                FloatingText.Spawn(taker, "+1 potion", PotionText, OrbTextAbove * 2f);
            }
            else
            {
                FloatingText.Spawn(taker, $"+{lying.Loot.Amount}", UiTheme.GoldHi);
            }
        }
    }

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
