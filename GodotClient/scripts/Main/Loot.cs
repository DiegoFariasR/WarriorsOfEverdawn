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

// Gold lying on the ground.
public sealed record GoldPile(int Id, int Amount, Vector3 Position);

// What monsters leave behind. The host decides all of it: every player gets a monster's souls as it dies, and its gold
// falls in a pile where it died and goes, when any player walks up to it, to every player. Co-op among friends, so
// nobody races anybody for it. Present on every machine at the same path, for its RPCs. Design: Docs/Design/combat.md.
public partial class Loot : Node3D
{
    public const string NodeName = "Loot";

    // The coin stacks are a hand high as modelled; drawn bigger, they can be seen from the camera's height.
    private const float PileScale = 1.8f;

    // Godot's Randf can return exactly 1, which a roll never is.
    private const float BelowOne = 0.999999f;

    private static readonly (int UpTo, string Model)[] PileModels =
    {
        (3, "res://assets/props/Money_Coins_Stack_Small.glb"),
        (6, "res://assets/props/Money_Coins_Stack_Medium.glb"),
        (int.MaxValue, "res://assets/props/Money_Coins_Stack_Large.glb"),
    };

    private readonly Dictionary<int, Lying> _piles = new();
    private readonly RandomNumberGenerator _random = new();
    private int _nextId;

    // On every machine, as a pile falls.
    public event Action<GoldPile>? GoldDropped;

    // On every machine, as a pile is picked up, with the peer who walked up to it.
    public event Action<GoldPile, long>? GoldTaken;

    public IEnumerable<GoldPile> Piles => _piles.Values.Select(l => l.Pile);

    public static Loot In(SceneTree tree) => tree.CurrentScene.GetNode<Loot>(NodeName);

    public override void _EnterTree() => EnemyCharacter.Died += OnEnemyDied;

    public override void _ExitTree() => EnemyCharacter.Died -= OnEnemyDied;

    // Host: a machine that has just joined learns what already lies on the ground.
    public void SendAllTo(long peer)
    {
        foreach (var pile in Piles)
        {
            RpcId(peer, MethodName.Place, pile.Id, pile.Amount, pile.Position);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Multiplayer.IsServer())
        {
            return;
        }

        var players = GetTree().GetNodesInGroup(PlayerCharacter.Group).OfType<PlayerCharacter>().ToList();
        foreach (var lying in _piles.Values.ToList())
        {
            lying.Age += (float)delta;
            if (lying.Age < LootRules.GoldSettleTime)
            {
                continue;
            }

            // The position each player last reported, as for hits on players (Docs/Design/multiplayer.md).
            var taker = players.FirstOrDefault(p => !p.IsDowned && Flat(p.NetPosition - lying.Pile.Position) <= LootRules.GoldPickupRadius);
            if (taker == null)
            {
                continue;
            }

            foreach (var player in players)
            {
                player.Vitals.EarnGold(lying.Pile.Amount);
            }

            Rpc(MethodName.Take, lying.Pile.Id, taker.PeerId);
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

        int gold = enemy.Definition.Gold.Roll(Mathf.Min(_random.Randf(), BelowOne));
        if (gold > 0)
        {
            var at = enemy.GlobalPosition;
            Rpc(MethodName.Place, _nextId++, gold, new Vector3(at.X, 0f, at.Z));
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Place(int id, int amount, Vector3 at)
    {
        // A machine joining as a pile falls can hear of it twice: once with everyone, once in the catch-up.
        if (_piles.ContainsKey(id))
        {
            return;
        }

        var model = Assets.InstantiateAtOrigin(PileModels.First(m => amount <= m.UpTo).Model);
        model.Name = $"Gold{id}";
        model.Scale = Vector3.One * PileScale;
        model.Position = at;
        AddChild(model);

        var pile = new GoldPile(id, amount, at);
        _piles[id] = new Lying(pile, model);
        GoldDropped?.Invoke(pile);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Take(int id, long byPeer)
    {
        if (!_piles.Remove(id, out var lying))
        {
            GD.PushError($"[Loot] gold pile {id}, picked up by peer {byPeer}, is not on the ground on this machine");
            return;
        }

        lying.Model.QueueFree();
        GoldTaken?.Invoke(lying.Pile, byPeer);
        if (PlayerCharacter.Find(GetTree(), byPeer) is { } taker)
        {
            FloatingText.Spawn(taker, $"+{lying.Pile.Amount}", UiTheme.GoldHi);
        }
    }

    private static float Flat(Vector3 offset) => new Vector2(offset.X, offset.Z).Length();

    private sealed class Lying
    {
        public Lying(GoldPile pile, Node3D model)
        {
            Pile = pile;
            Model = model;
        }

        public GoldPile Pile { get; }

        public Node3D Model { get; }

        public float Age { get; set; }
    }
}
