using System;

namespace WarriorsOfEverdawn.Core.Combat;

// The health potion every player carries, first pass: a few charges, each healing at once as it is drunk. A player
// starts with it full; charges drop now and then as monsters die (LootKind.PotionCharge), and the innkeeper fills it.
public sealed class HealthPotion
{
    public const int MaxCharges = 3;

    // What a charge heals: two fifths of a player's HP.
    public const int Heal = 40;

    // Between one drink and the next, so three cannot be drunk in one breath.
    public const float Cooldown = 1f;

    public int Charges { get; private set; } = MaxCharges;

    public bool IsFull => Charges == MaxCharges;

    // Drinks a charge if there is one and it would heal: what it heals of the `missing` HP. Nothing, and no charge
    // spent, with none left or none missing.
    public int Drink(int missing)
    {
        if (missing < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(missing), missing, "Missing HP cannot be negative");
        }

        if (Charges == 0 || missing == 0)
        {
            return 0;
        }

        Charges--;
        return Math.Min(Heal, missing);
    }

    // One more charge, unless it is full. Whether it took one.
    public bool AddCharge()
    {
        if (IsFull)
        {
            return false;
        }

        Charges++;
        return true;
    }

    public void Refill() => Charges = MaxCharges;
}
