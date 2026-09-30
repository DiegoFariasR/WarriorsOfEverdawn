using System;
using WarriorsOfEverdawn.Core.Stats;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Stats;

public class ManaPoolTests
{
    private const int Max = 50;
    private const int Cost = 20;

    [Fact]
    public void Starts_full()
    {
        Assert.Equal(Max, new ManaPool(Max).Current);
    }

    [Fact]
    public void Spends_only_what_it_has()
    {
        var pool = new ManaPool(Max);

        Assert.True(pool.TrySpend(Cost));
        Assert.True(pool.TrySpend(Cost));
        Assert.False(pool.TrySpend(Cost));
        Assert.Equal(Max - 2 * Cost, pool.Current);
    }

    [Fact]
    public void Regeneration_refills_up_to_max()
    {
        var pool = new ManaPool(Max);
        pool.TrySpend(Cost);

        pool.Regenerate(Cost / 2f);
        Assert.Equal(Max - Cost / 2f, pool.Current);

        pool.Regenerate(Max);
        Assert.Equal(Max, pool.Current);
    }

    [Fact]
    public void Rejects_a_negative_maximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ManaPool(-1));
    }
}
