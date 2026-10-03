using System;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class CryptTests
{
    [Fact]
    public void Every_guard_stands_and_spots_beyond_them_start_the_list_again()
    {
        var first = Enumerable.Range(0, Crypt.Guards.Count).Select(Crypt.GuardFor);

        Assert.Equal(Crypt.Guards, first);
        Assert.Same(Crypt.Guards[0], Crypt.GuardFor(Crypt.Guards.Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => Crypt.GuardFor(-1));
    }

    [Fact]
    public void The_guards_are_the_skeletons_a_wave_brings_and_the_treasure_holds_gold_and_orbs()
    {
        Assert.All(Crypt.Guards, guard => Assert.Contains(guard, Enemies.All));
        Assert.True(Crypt.Treasure.Gold > 0 && Crypt.Treasure.Orbs > 0);
    }

    [Theory]
    [InlineData(6, 0, false, true)]
    [InlineData(6, 1, false, false)]
    [InlineData(6, 0, true, false)]
    [InlineData(0, 0, false, false)]
    public void The_treasure_is_left_once_when_every_risen_guard_has_fallen(int risen, int standing, bool treasureLeft, bool cleared)
    {
        Assert.Equal(cleared, Crypt.Cleared(risen, standing, treasureLeft));
    }
}
