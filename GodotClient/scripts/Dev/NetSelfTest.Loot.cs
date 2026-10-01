using System.Linq;
using Godot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;

namespace WarriorsOfEverdawn.Dev;

// [loot-check]: monsters leave gold that every machine sees fall and be picked up, with a coin stack shown for each
// pile on the ground and no other, every player earns it and a soul per monster, and the HUD shows what the player has. On the host, where it is decided, the gold collected is exactly
// what its player has, and its souls are the monsters it saw die.
public partial class NetSelfTest
{
    private Loot? _loot;
    private int _pilesDropped;
    private int _pilesTaken;
    private int _goldDropped;
    private int _goldCollected;
    private int _deathsSinceHere;
    private int _purseMismatchFrames;
    private int _pileModelMismatchFrames;

    private void TrackLoot()
    {
        _loot = Loot.In(GetTree());
        _loot.GoldDropped += pile =>
        {
            _pilesDropped++;
            _goldDropped += pile.Amount;
        };
        _loot.GoldTaken += (pile, _) =>
        {
            _pilesTaken++;
            _goldCollected += pile.Amount;
        };
    }

    // Souls only go to players in the game when a monster dies.
    private void CountDeathForLoot(EnemyCharacter enemy) => _deathsSinceHere += LocalPlayer() != null ? 1 : 0;

    private void MeasureLoot()
    {
        // A stack taken this frame is freed at the frame's end.
        if (_loot != null && _loot.GetChildren().Count(c => !c.IsQueuedForDeletion()) != _loot.Piles.Count())
        {
            _pileModelMismatchFrames++;
        }

        if (LocalPlayer() is { } local
            && (_hud.ShownGold != local.Vitals.Gold.ToString() || _hud.ShownSouls != local.Vitals.Souls.ToString()))
        {
            _purseMismatchFrames++;
        }
    }

    private void PrintLootCheck(long me)
    {
        var local = LocalPlayer();
        GD.Print($"[loot-check] me={me} piles_seen={_pilesDropped} piles_taken_seen={_pilesTaken} piles_on_ground={_loot?.Piles.Count() ?? -1} "
            + $"gold_dropped={_goldDropped} gold_collected={_goldCollected} gold_here={local?.Vitals.Gold} souls_here={local?.Vitals.Souls} "
            + $"deaths_since_here={_deathsSinceHere} purse_mismatch_frames={_purseMismatchFrames} "
            + $"pile_model_mismatch_frames={_pileModelMismatchFrames}");
    }
}
