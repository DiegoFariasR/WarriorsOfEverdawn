using System;

namespace WarriorsOfEverdawn.Core.Combat;

public sealed class Health
{
    public Health(int max)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max), max, "Health needs a positive maximum");
        }

        Max = max;
        Current = max;
    }

    public int Max { get; }

    public int Current { get; private set; }

    public bool IsDead => Current <= 0;

    // Returns the damage actually taken. Nothing once dead, so two hits landing together cannot kill twice.
    public int TakeDamage(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Damage cannot be negative");
        }

        if (IsDead)
        {
            return 0;
        }

        int taken = Math.Min(amount, Current);
        Current -= taken;
        return taken;
    }

    public void RestoreFull() => Current = Max;
}
