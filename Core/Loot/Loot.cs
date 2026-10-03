using System;
using System.Collections.Generic;
using System.Linq;

namespace WarriorsOfEverdawn.Core.Loot;

// What a monster can leave on the ground.
public enum LootKind
{
    Gold,
    Orb,

    // A charge of the health potion (HealthPotion): every player who has room for one gets one.
    PotionCharge,
}

// What a purse holds, and so what a cost can be made up of.
public enum Currency
{
    Gold,
    Souls,
    Orbs,
}

// What something costs: so much of each currency, most often of one alone.
public sealed record Cost(int Gold = 0, int Souls = 0, int Orbs = 0)
{
    public static Cost Nothing { get; } = new();

    public bool IsNothing => Gold == 0 && Souls == 0 && Orbs == 0;

    // The currencies it asks for, with how much of each, in the order they are named.
    public IEnumerable<(Currency Currency, int Amount)> Parts =>
        new[] { (Currency.Gold, Gold), (Currency.Souls, Souls), (Currency.Orbs, Orbs) }.Where(p => p.Item2 > 0);

    // How much more of each currency someone carrying this much would need; nothing, when they can pay.
    public Cost ShortWith(int gold, int souls, int orbs) =>
        new(Math.Max(0, Gold - gold), Math.Max(0, Souls - souls), Math.Max(0, Orbs - orbs));
}

// The gold a monster leaves: anything from Min to Max, each as likely as the next.
public sealed record GoldDrop(int Min, int Max)
{
    // roll is uniform in [0, 1).
    public int Roll(float roll)
    {
        if (Min < 0 || Max < Min)
        {
            throw new InvalidOperationException($"A gold drop needs 0 <= Min <= Max, got {Min} to {Max}");
        }

        return Min + (int)(LootRules.Checked(roll) * (Max - Min + 1));
    }
}

public static class LootRules
{
    // Centre of a player to something on the ground: walking this close picks it up, with no button.
    public const float PickupRadius = 2f;

    // It lies this long before it can be picked up, so it is seen to fall even under a player's feet.
    public const float SettleTime = 0.4f;

    // A pile shows its gold coin for coin up to this many; more gold than that looks as this many do.
    public const int MostCoinsShown = 10;

    // How many coins a pile of that much gold shows.
    public static int CoinsShown(int gold) =>
        gold > 0 ? Math.Min(gold, MostCoinsShown) : throw new ArgumentOutOfRangeException(nameof(gold), gold, "A pile holds at least 1 gold");

    // Each pile lies turned this much further round than the one before it: the golden angle, which never comes
    // back to a turn it has used, so no two piles near each other lie the same way.
    public const float PileTurnStep = 2.39996323f;

    // Which way the pile of that drop lies, in radians about the upright: the same on every machine, by the drop's
    // number, so the piles do not all lie as the model was made.
    public static float PileYaw(int id) =>
        id >= 0 ? (float)(id * (double)PileTurnStep % (2 * Math.PI)) : throw new ArgumentOutOfRangeException(nameof(id), id, "A drop's number is not negative");

    // Whether a monster that leaves something `chance` of the time (a magic orb, a potion charge) leaves it now. roll
    // is uniform in [0, 1).
    public static bool Drops(float chance, float roll)
    {
        if (chance is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(chance), chance, "A chance is from 0 to 1");
        }

        return Checked(roll) < chance;
    }

    internal static float Checked(float roll) =>
        roll is >= 0f and < 1f ? roll : throw new ArgumentOutOfRangeException(nameof(roll), roll, "A roll is at least 0 and below 1");
}

// What a player has earned and not yet spent. Gold buys weapons, gold and orbs improve them and armour, weapons and
// orbs sell for gold; souls buy nothing yet.
public sealed class Purse
{
    // What every player has to spend as a session starts, with one weapon in hand (WeaponSets.Default).
    public const int StartingGold = 100;

    public int Gold { get; private set; }

    public int Souls { get; private set; }

    public int Orbs { get; private set; }

    public void EarnGold(int amount) => Gold += Earned(amount);

    public void EarnSouls(int amount) => Souls += Earned(amount);

    public void EarnOrbs(int amount) => Orbs += Earned(amount);

    public void Earn(Cost amount)
    {
        EarnGold(amount.Gold);
        EarnSouls(amount.Souls);
        EarnOrbs(amount.Orbs);
    }

    public bool CanPay(Cost cost) => ShortOf(cost).IsNothing;

    // How much more of each currency the purse needs before it can pay.
    public Cost ShortOf(Cost cost) => cost.ShortWith(Gold, Souls, Orbs);

    public void Pay(Cost cost)
    {
        if (cost.Gold < 0 || cost.Souls < 0 || cost.Orbs < 0 || !CanPay(cost))
        {
            throw new InvalidOperationException($"Cannot pay {cost}: the purse holds {Gold} gold, {Souls} souls and {Orbs} orbs");
        }

        Gold -= cost.Gold;
        Souls -= cost.Souls;
        Orbs -= cost.Orbs;
    }

    private static int Earned(int amount) =>
        amount >= 0 ? amount : throw new ArgumentOutOfRangeException(nameof(amount), amount, "Earnings cannot be negative");
}
