using System;
using System.Collections.Generic;
using System.Linq;

namespace WarriorsOfEverdawn.Core.Loot;

// What a monster can leave on the ground.
public enum LootKind
{
    Gold,
    Orb,
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
    public const float PickupRadius = 1.5f;

    // It lies this long before it can be picked up, so it is seen to fall even under a player's feet.
    public const float SettleTime = 0.4f;

    // Whether a monster that leaves a magic orb `chance` of the time leaves one now. roll is uniform in [0, 1).
    public static bool DropsOrb(float chance, float roll)
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

// What a player has earned and not yet spent. Gold buys weapons, gold and orbs improve them; souls buy nothing yet.
public sealed class Purse
{
    public int Gold { get; private set; }

    public int Souls { get; private set; }

    public int Orbs { get; private set; }

    public void EarnGold(int amount) => Gold += Earned(amount);

    public void EarnSouls(int amount) => Souls += Earned(amount);

    public void EarnOrbs(int amount) => Orbs += Earned(amount);

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
