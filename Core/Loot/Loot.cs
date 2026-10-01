using System;

namespace WarriorsOfEverdawn.Core.Loot;

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

        if (roll is < 0f or >= 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(roll), roll, "A roll is at least 0 and below 1");
        }

        return Min + (int)(roll * (Max - Min + 1));
    }
}

public static class LootRules
{
    // Centre of a player to a pile of gold: walking this close picks it up, with no button.
    public const float GoldPickupRadius = 1.5f;

    // A pile lies this long before it can be picked up, so it is seen to fall even under a player's feet.
    public const float GoldSettleTime = 0.4f;
}

// What a player has earned. Neither is spent on anything yet.
public sealed class Purse
{
    public int Gold { get; private set; }

    public int Souls { get; private set; }

    public void EarnGold(int amount) => Gold += Earned(amount);

    public void EarnSouls(int amount) => Souls += Earned(amount);

    private static int Earned(int amount) =>
        amount >= 0 ? amount : throw new ArgumentOutOfRangeException(nameof(amount), amount, "Earnings cannot be negative");
}
