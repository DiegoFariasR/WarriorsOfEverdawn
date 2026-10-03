using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Loot;
using WarriorsOfEverdawn.Enemy;
using WarriorsOfEverdawn.Main;
using WarriorsOfEverdawn.Player;

namespace WarriorsOfEverdawn.Dev;

// [loot-check]: monsters leave gold, and now and then a magic orb or a potion's charge, that every machine sees fall
// and be picked up, with a model shown for each thing on the ground and no other; every player earns it and a soul
// per monster; the HUD shows what the player has; potions are drunk, each healing no more than a charge does; and a
// wisp rises for every soul of every monster seen to die, and each reaches this machine's player or is still on its
// way. On the host, where it is decided, what was collected is exactly what its player has on top of what it started
// with, and its souls are the monsters it saw die.
public partial class NetSelfTest
{
    private readonly Dictionary<LootKind, int> _lootDropped = new();
    private readonly Dictionary<LootKind, int> _lootTaken = new();
    private readonly Dictionary<LootKind, int> _lootCollected = new();
    private Loot? _loot;
    private SoulWisps? _wisps;
    private int _goldDropped;
    private int _deathsSinceHere;
    private int _soulsSinceHere;
    private int _purseMismatchFrames;
    private int _lootModelMismatchFrames;
    private int _drinksSeen;
    private int _drinksHere;
    private int _drinkHealedMost;

    // What every player started the session with, before anything was collected.
    public int GoldAtStart { get; init; }

    private void TrackLoot()
    {
        _loot = Loot.In(GetTree());
        _wisps = SoulWisps.In(GetTree());
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

    private void OnDrank(PlayerCharacter player, int healed)
    {
        _drinksSeen++;
        _drinksHere += player.IsMultiplayerAuthority() ? 1 : 0;
        _drinkHealedMost = Mathf.Max(_drinkHealedMost, healed);
    }

    // Souls only go to players in the game when a monster dies.
    private void CountDeathForLoot(EnemyCharacter enemy)
    {
        if (LocalPlayer() != null)
        {
            _deathsSinceHere++;
            _soulsSinceHere += enemy.Definition.Souls;
        }
    }

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
            + $"loot_model_mismatch_frames={_lootModelMismatchFrames} potions_seen={_lootDropped.GetValueOrDefault(LootKind.PotionCharge)} "
            + $"potions_taken_seen={_lootTaken.GetValueOrDefault(LootKind.PotionCharge)} potions_on_ground={OnGround(LootKind.PotionCharge)} "
            + $"potion_charges_here={local?.Vitals.PotionCharges} drinks_seen={_drinksSeen} drinks_here={_drinksHere} drink_healed_most={_drinkHealedMost} "
            + $"heal_per_charge={HealthPotion.Heal} souls_died_here={_soulsSinceHere} souls_risen={_wisps?.Risen ?? -1} "
            + $"souls_taken_in={_wisps?.Taken ?? -1} souls_flying={_wisps?.Flying ?? -1}");
    }
}
