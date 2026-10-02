using System;
using System.Collections.Generic;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Loot;

namespace WarriorsOfEverdawn.Core.Trade;

// What a seller needs to know of whoever is buying: what they carry and wear decides what they are offered.
public sealed record Buyer(WeaponSets Weapons, int ArmourTier);

// One thing a seller offers this buyer, for what it costs: a weapon or a tier of armour. A weapon with no Slot is one
// for sale, which goes wherever a bought weapon goes; with a Slot it is the weapon that slot would hold in place of
// the one there now (an improvement of it). Armour is worn at once in place of what was worn. Unavailable says why it
// cannot be had just now, when it cannot; what it names then is what the buyer already has.
public sealed record TradeItem(string Id, string Name, Cost Cost, WeaponDefinition? Weapon = null, WeaponSlot? Slot = null, string? Unavailable = null)
{
    public ArmourDefinition? Armour { get; init; }

    public bool GivesSomething => Weapon != null || Armour != null;
}

// Someone standing in the town who trades. Line says in a few words what for what. What they offer can depend on
// the buyer.
public sealed record SellerDefinition(string Id, string Name, string Line, Func<Buyer, IReadOnlyList<TradeItem>> ItemsFor)
{
    // Null when the seller offers this buyer no such thing.
    public TradeItem? Item(Buyer buyer, string id) => ItemsFor(buyer).FirstOrDefault(i => i.Id == id);
}

public static class Sellers
{
    // First pass: every weapon in its plain make, at one price.
    private const int PlainWeaponPrice = 50;

    private static readonly IReadOnlyList<TradeItem> PlainWeapons =
        Weapons.All.Select(w => new TradeItem(w.Id, w.Name, new Cost(Gold: PlainWeaponPrice), w)).ToList();

    public static readonly SellerDefinition Weaponsmith = new("weaponsmith", "Weaponsmith", "Plain weapons for gold", _ => PlainWeapons);

    // Works on what the buyer has on: one offer each for the weapon in hand, the weapon on the back and the armour
    // worn, always in that order, each the next step up from what is there.
    public static readonly SellerDefinition Blacksmith = new("blacksmith", "Blacksmith", "Your weapons and armour made better",
        buyer => new[]
        {
            TradeRules.Improvement(buyer.Weapons, WeaponSlot.Hand),
            TradeRules.Improvement(buyer.Weapons, WeaponSlot.Back),
            TradeRules.BetterArmour(buyer.ArmourTier),
        });

    public static IReadOnlyList<SellerDefinition> All { get; } = new[] { Weaponsmith, Blacksmith };

    public static SellerDefinition ById(string id) =>
        All.FirstOrDefault(s => s.Id == id) ?? throw new KeyNotFoundException($"Unknown seller '{id}'");
}

public enum BuyOutcome
{
    Bought,
    TooFar,
    Down,
    CannotAfford,
    Unavailable,
}

public static class TradeRules
{
    // A seller's window always shows this many slots, in rows of SlotColumns, filled or empty: the same window for
    // every seller.
    public const int Slots = 9;
    public const int SlotColumns = 3;

    // Centre of a player to a seller: close enough to trade.
    public const float Reach = 2.5f;

    // First pass. Each level costs more than the last: this much gold for every level reached, and an orb for every
    // two (one for +1 and +2, two for +3 and +4, up to five for +9 and +10).
    public const int GoldPerLevel = 40;
    public const int LevelsPerOrb = 2;

    // Armour's first tiers are had for gold alone; from the tier after these, orbs as well.
    public const int ArmourTiersForGoldAlone = 2;

    // First pass, by tier from the first: each costs more than the one before.
    private static readonly Cost[] ArmourCosts =
    {
        new(Gold: 60),
        new(Gold: 120),
        new(Gold: 200, Orbs: 1),
        new(Gold: 300, Orbs: 2),
        new(Gold: 420, Orbs: 3),
    };

    // What improving a weapon to this level costs, from the level below it.
    public static Cost ImprovementCost(int toLevel) =>
        toLevel is >= 1 and <= WeaponDefinition.MaxLevel
            ? new Cost(Gold: GoldPerLevel * toLevel, Orbs: (toLevel + LevelsPerOrb - 1) / LevelsPerOrb)
            : throw new ArgumentOutOfRangeException(nameof(toLevel), toLevel, $"A weapon is improved to levels 1 to {WeaponDefinition.MaxLevel}");

    // What that tier of armour costs, from the tier below it.
    public static Cost ArmourCost(int tier) =>
        tier >= 1 && tier <= ArmourCosts.Length
            ? ArmourCosts[tier - 1]
            : throw new ArgumentOutOfRangeException(nameof(tier), tier, $"Armour is bought in tiers 1 to {ArmourCosts.Length}");

    // The blacksmith's offer for one of the buyer's slots: its weapon one level better, or why there is nothing to do.
    public static TradeItem Improvement(WeaponSets buyer, WeaponSlot slot)
    {
        string id = slot.ToString().ToLowerInvariant();
        string where = slot == WeaponSlot.Hand ? "hand" : "back";
        if (buyer.In(slot) is not { } weapon)
        {
            return new TradeItem(id, $"Nothing {(slot == WeaponSlot.Hand ? "in" : "on")} your {where}", Cost.Nothing, null, slot, "No weapon there to work on");
        }

        if (weapon.Level >= WeaponDefinition.MaxLevel)
        {
            return new TradeItem(id, weapon.Name, Cost.Nothing, null, slot, "As good as it can be made");
        }

        var better = Weapons.AtLevel(weapon, weapon.Level + 1);
        return new TradeItem(id, better.Name, ImprovementCost(better.Level), better, slot);
    }

    // The blacksmith's offer for the armour of a buyer wearing tier `worn`: the next tier up, or why there is nothing
    // to do.
    public static TradeItem BetterArmour(int worn)
    {
        const string Id = "armour";
        if (worn >= Armours.MaxTier)
        {
            var best = Armours.AtTier(worn);
            return new TradeItem(Id, best.Name, Cost.Nothing, Unavailable: "As good as it can be made") { Armour = best };
        }

        var better = Armours.AtTier(worn + 1);
        return new TradeItem(Id, better.Name, ArmourCost(better.Tier)) { Armour = better };
    }

    // The buyer's weapons once the weapon is theirs, and the weapon that had to be put down to make room, if any.
    public static (WeaponSets Sets, WeaponDefinition? PutDown) Receive(WeaponSets buyer, WeaponDefinition weapon, WeaponSlot? slot) =>
        slot is { } held ? (buyer.With(held, weapon), null) : buyer.WithBought(weapon);

    // What comes of a player `distance` from the seller, on its feet or not, asking for this.
    public static BuyOutcome Judge(Purse purse, TradeItem item, float distance, bool down) =>
        down ? BuyOutcome.Down
        : distance > Reach ? BuyOutcome.TooFar
        : item.Unavailable != null || !item.GivesSomething ? BuyOutcome.Unavailable
        : purse.CanPay(item.Cost) ? BuyOutcome.Bought
        : BuyOutcome.CannotAfford;

    // Judges the purchase and, when it goes through, takes the cost out of the purse.
    public static BuyOutcome Buy(Purse purse, TradeItem item, float distance, bool down)
    {
        var outcome = Judge(purse, item, distance, down);
        if (outcome == BuyOutcome.Bought)
        {
            purse.Pay(item.Cost);
        }

        return outcome;
    }
}
