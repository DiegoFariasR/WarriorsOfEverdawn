using System;

namespace WarriorsOfEverdawn.Core.Stats;

// Fractional so regeneration can tick every frame.
public sealed class ManaPool
{
    public ManaPool(int max)
    {
        if (max < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "Max mana cannot be negative");
        }

        Max = max;
        Current = max;
    }

    public int Max { get; private set; }

    public float Current { get; private set; }

    public bool CanAfford(int cost) => Current >= cost;

    public bool TrySpend(int cost)
    {
        if (!CanAfford(cost))
        {
            return false;
        }

        Current -= cost;
        return true;
    }

    public void Regenerate(float amount) => Current = Math.Min(Max, Current + amount);

    // WIS rose: the pool and what is in it grow by as much.
    public void Raise(int max)
    {
        if (max < Max)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, $"Max mana only rises, and it is {Max}");
        }

        Current += max - Max;
        Max = max;
    }
}
