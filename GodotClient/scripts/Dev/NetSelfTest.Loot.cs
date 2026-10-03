using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Loot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;

namespace WarriorsOfEverdawn.Dev;

// [loot-check]: monsters leave gold, and now and then a magic orb, that every machine sees fall and be picked up, with
// a model shown for each thing on the ground and no other; every player earns it and a soul per monster; and the HUD
// shows what the player has. On the host, where it is decided, what was collected is exactly what its player has on
// top of what it started with, and its souls are the monsters it saw die.
public partial class NetSelfTest
{
    private readonly Dictionary<LootKind, int> _lootDropped = new();
    private readonly Dictionary<LootKind, int> _lootTaken = new();
    private readonly Dictionary<LootKind, int> _lootCollected = new();
    private Loot? _loot;
    private int _goldDropped;
    private int _deathsSinceHere;
    private int _purseMismatchFrames;
    private int _lootModelMismatchFrames;

    // What every player started the session with, before anything was collected.
    public int GoldAtStart { get; init; }

    private void TrackLoot()
    {
        _loot = Loot.In(GetTree());
        _loot.Dropped += loot =>
        {
            _lootDropped[loot.Kind] = _lootDropped.GetValueOrDefault(loot.Kind) + 1;
            _goldDropped += loot.Kind == LootKind.Gold ? loot.Amount : 0;
        };
        _loot.Taken += (loot, _) =>
        {
            _lootTaken[loot.Kind] = _lootTaken.GetValueOrDefault(loot.Kind) + 1;
            _lootCollected[loot.Kind] = _lootCollected.GetValueOrDefault(loot.Kind) + loot.Amount;
        };
    }

    // Souls only go to players in the game when a monster dies.
    private void CountDeathForLoot(EnemyCharacter enemy) => _deathsSinceHere += LocalPlayer() != null ? 1 : 0;

    private void MeasureLoot()
    {
        // A model taken this frame is freed at the frame's end.
        if (_loot != null && _loot.GetChildren().Count(c => !c.IsQueuedForDeletion()) != _loot.OnGround.Count())
        {
            _lootModelMismatchFrames++;
        }

        if (LocalPlayer() is { } local
            && (_hud.ShownGold != local.Vitals.Gold.ToString() || _hud.ShownSouls != local.Vitals.Souls.ToString()
                || _hud.ShownOrbs != local.Vitals.Orbs.ToString()))
        {
            _purseMismatchFrames++;
        }
    }

    private void PrintLootCheck(long me)
    {
        var local = LocalPlayer();
        int OnGround(LootKind kind) => _loot?.OnGround.Count(l => l.Kind == kind) ?? -1;
        GD.Print($"[loot-check] me={me} piles_seen={_lootDropped.GetValueOrDefault(LootKind.Gold)} piles_taken_seen={_lootTaken.GetValueOrDefault(LootKind.Gold)} "
            + $"piles_on_ground={OnGround(LootKind.Gold)} gold_dropped={_goldDropped} gold_collected={_lootCollected.GetValueOrDefault(LootKind.Gold)} "
            + $"gold_start={GoldAtStart} gold_here={local?.Vitals.Gold} orbs_seen={_lootDropped.GetValueOrDefault(LootKind.Orb)} orbs_taken_seen={_lootTaken.GetValueOrDefault(LootKind.Orb)} "
            + $"orbs_on_ground={OnGround(LootKind.Orb)} orbs_collected={_lootCollected.GetValueOrDefault(LootKind.Orb)} orbs_here={local?.Vitals.Orbs} "
            + $"souls_here={local?.Vitals.Souls} deaths_since_here={_deathsSinceHere} purse_mismatch_frames={_purseMismatchFrames} "
            + $"loot_model_mismatch_frames={_lootModelMismatchFrames}");
    }
}
