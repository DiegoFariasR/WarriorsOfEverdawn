using System;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Loot;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Loot;

public class LootTests
{
    private static readonly GoldDrop Drop = new(3, 6);

    [Fact]
    public void A_gold_drop_rolls_every_amount_from_its_least_to_its_most()
    {
        int amounts = Drop.Max - Drop.Min + 1;
        var rolled = Enumerable.Range(0, amounts).Select(i => Drop.Roll((i + 0.5f) / amounts)).ToList();

        Assert.Equal(Enumerable.Range(Drop.Min, amounts), rolled);
        Assert.Equal(Drop.Min, Drop.Roll(0f));
        Assert.Equal(Drop.Max, Drop.Roll(0.9999f));
    }

    [Fact]
    public void A_fixed_drop_always_gives_its_amount()
    {
        var always = new GoldDrop(Drop.Min, Drop.Min);

        Assert.Equal(Drop.Min, always.Roll(0f));
        Assert.Equal(Drop.Min, always.Roll(0.99f));
    }

    [Fact]
    public void Rolls_outside_zero_to_one_and_inverted_drops_fail_loudly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Drop.Roll(1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Drop.Roll(-0.1f));
        Assert.Throws<InvalidOperationException>(() => new GoldDrop(Drop.Max, Drop.Min).Roll(0f));
    }

    [Fact]
    public void Every_monster_drops_gold_and_gives_one_soul()
    {
        Assert.NotEmpty(Enemies.All);
        foreach (var enemy in Enemies.All)
        {
            Assert.True(enemy.Gold.Min >= 1, enemy.Id);
            Assert.True(enemy.Gold.Max >= enemy.Gold.Min, enemy.Id);
            Assert.Equal(1, enemy.Souls);
        }
    }

    [Fact]
    public void A_purse_adds_up_what_is_earned_and_refuses_negative_earnings()
    {
        var purse = new Purse();

        purse.EarnGold(Drop.Min);
        purse.EarnGold(Drop.Max);
        purse.EarnSouls(1);
        purse.EarnSouls(1);

        Assert.Equal(Drop.Min + Drop.Max, purse.Gold);
        Assert.Equal(2, purse.Souls);
        Assert.Throws<ArgumentOutOfRangeException>(() => purse.EarnGold(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => purse.EarnSouls(-1));
    }

    [Fact]
    public void Gold_is_picked_up_from_closer_than_weapons_are_read()
    {
        Assert.True(LootRules.GoldPickupRadius > 0f);
        Assert.True(LootRules.GoldPickupRadius < Pickups.LabelRange);
        Assert.True(LootRules.GoldSettleTime > 0f);
    }
}
