using System;
using System.Collections.Generic;
using System.Linq;

namespace WarriorsOfEverdawn.Core.Combat;

// What a player wears: one tier of a single line, each better than the last. DamageTaken is the share of a blow that
// still gets through it.
public sealed record ArmourDefinition(int Tier, string Name, float DamageTaken)
{
    // The share of a blow it stops, as a whole percentage, for showing.
    public int PercentStopped => (int)MathF.Round((1f - DamageTaken) * 100f);
}

public static class Armours
{
    // Each tier stops this much more of every blow.
    public const float StoppedPerTier = 0.1f;

    private static readonly string[] Names = { "Worn clothes", "Leather", "Mail", "Scale", "Plate", "Tempered plate" };

    // Tier 0 is what everyone starts in; the rest are made by the blacksmith, in order.
    public static IReadOnlyList<ArmourDefinition> All { get; } =
        Names.Select((name, tier) => new ArmourDefinition(tier, name, 1f - StoppedPerTier * tier)).ToList();

    public static int MaxTier => All.Count - 1;

    public static ArmourDefinition AtTier(int tier) =>
        tier >= 0 && tier <= MaxTier ? All[tier] : throw new ArgumentOutOfRangeException(nameof(tier), tier, $"Armour comes in tiers 0 to {MaxTier}");
}

// One player's armour taking blows. Damage is whole numbers and monsters' blows are small, so rounding each blow on
// its own would make some tiers no better than the one before against a weak monster. What a blow keeps past the
// armour is worked out to the fraction, the whole part dealt and the rest carried into the next blow: over a fight
// the share that gets through is exact.
public sealed class ArmourWear
{
    private float _carried;

    // What gets through of a blow of this much, after any guard has had its share.
    public int Through(ArmourDefinition armour, int damage)
    {
        if (damage <= 0)
        {
            return 0;
        }

        // A hair over, so a sum that is whole in arithmetic (three blows of a third) does not fall short in floats.
        _carried += damage * armour.DamageTaken + 1e-4f;
        int through = (int)_carried;
        _carried -= through + 1e-4f;
        return through;
    }
}
