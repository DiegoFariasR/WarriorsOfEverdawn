using System;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class ArmourTests
{
    [Fact]
    public void Armour_comes_in_a_line_of_four_or_five_tiers_above_what_everyone_starts_in()
    {
        Assert.InRange(Armours.MaxTier, 4, 5);
        Assert.Equal(Enumerable.Range(0, Armours.MaxTier + 1), Armours.All.Select(a => a.Tier));
        Assert.Equal(Armours.All.Count, Armours.All.Select(a => a.Name).Distinct().Count());
        Assert.All(Armours.All, a => Assert.Same(a, Armours.AtTier(a.Tier)));
    }

    [Fact]
    public void What_everyone_starts_in_stops_nothing_and_every_tier_stops_more_than_the_last()
    {
        Assert.Equal(1f, Armours.AtTier(0).DamageTaken);
        Assert.Equal(0, Armours.AtTier(0).PercentStopped);
        for (int tier = 1; tier <= Armours.MaxTier; tier++)
        {
            Assert.True(Armours.AtTier(tier).DamageTaken < Armours.AtTier(tier - 1).DamageTaken, $"tier {tier}");
            Assert.True(Armours.AtTier(tier).PercentStopped > Armours.AtTier(tier - 1).PercentStopped, $"tier {tier}");
        }

        Assert.True(Armours.AtTier(Armours.MaxTier).DamageTaken > 0f);
    }

    [Fact]
    public void A_tier_out_of_the_line_fails_loudly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Armours.AtTier(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Armours.AtTier(Armours.MaxTier + 1));
    }

    [Fact]
    public void Worn_clothes_let_every_blow_through_whole()
    {
        var wear = new ArmourWear();

        Assert.Equal(new[] { 6, 12, 1 }, new[] { 6, 12, 1 }.Select(blow => wear.Through(Armours.AtTier(0), blow)));
        Assert.Equal(0, wear.Through(Armours.AtTier(0), 0));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(12)]
    public void Over_many_small_blows_each_tier_lets_through_exactly_its_share(int blow)
    {
        const int Blows = 100;

        foreach (var armour in Armours.All)
        {
            var wear = new ArmourWear();
            int through = Enumerable.Range(0, Blows).Sum(_ => wear.Through(armour, blow));

            Assert.Equal((int)MathF.Round(blow * Blows * armour.DamageTaken), through);
        }
    }

    [Fact]
    public void Against_the_same_blows_every_tier_takes_less_than_the_one_before()
    {
        const int SmallBlow = 6;
        const int Blows = 20;

        var taken = Armours.All.Select(armour =>
        {
            var wear = new ArmourWear();
            return Enumerable.Range(0, Blows).Sum(_ => wear.Through(armour, SmallBlow));
        }).ToList();

        for (int tier = 1; tier < taken.Count; tier++)
        {
            Assert.True(taken[tier] < taken[tier - 1], $"tier {tier}: {taken[tier]} against {taken[tier - 1]}");
        }
    }

    [Fact]
    public void A_single_blow_never_gains_from_armour_and_loses_at_most_one_more_than_its_share()
    {
        const int Blow = 12;

        foreach (var armour in Armours.All)
        {
            int through = new ArmourWear().Through(armour, Blow);

            Assert.InRange(through, (int)(Blow * armour.DamageTaken), Blow);
        }
    }
}
