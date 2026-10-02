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
        purse.EarnOrbs(1);
        purse.EarnOrbs(1);
        purse.EarnOrbs(1);

        Assert.Equal(Drop.Min + Drop.Max, purse.Gold);
        Assert.Equal(2, purse.Souls);
        Assert.Equal(3, purse.Orbs);
        Assert.Throws<ArgumentOutOfRangeException>(() => purse.EarnGold(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => purse.EarnSouls(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => purse.EarnOrbs(-1));
    }

    [Fact]
    public void What_lies_on_the_ground_is_picked_up_from_closer_than_weapons_are_read()
    {
        Assert.True(LootRules.PickupRadius > 0f);
        Assert.True(LootRules.PickupRadius < Pickups.LabelRange);
        Assert.True(LootRules.SettleTime > 0f);
    }

    [Fact]
    public void An_orb_drops_on_the_rolls_below_its_chance()
    {
        const float Chance = 0.25f;

        Assert.True(LootRules.DropsOrb(Chance, 0f));
        Assert.True(LootRules.DropsOrb(Chance, Chance - 0.01f));
        Assert.False(LootRules.DropsOrb(Chance, Chance));
        Assert.False(LootRules.DropsOrb(Chance, 0.99f));
    }

    [Fact]
    public void A_chance_of_none_never_drops_an_orb_and_a_chance_of_one_always_does()
    {
        foreach (float roll in new[] { 0f, 0.5f, 0.9999f })
        {
            Assert.False(LootRules.DropsOrb(0f, roll));
            Assert.True(LootRules.DropsOrb(1f, roll));
        }
    }

    [Fact]
    public void Chances_and_rolls_out_of_range_fail_loudly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LootRules.DropsOrb(1.1f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => LootRules.DropsOrb(-0.1f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => LootRules.DropsOrb(0.5f, 1f));
    }

    [Fact]
    public void Every_monster_can_leave_an_orb_and_seldom_does()
    {
        foreach (var enemy in Enemies.All)
        {
            Assert.True(enemy.OrbChance > 0f, enemy.Id);

            // Rare next to gold, which every monster always leaves.
            Assert.True(enemy.OrbChance < 0.5f, enemy.Id);
        }
    }
}
