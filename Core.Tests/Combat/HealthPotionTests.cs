using System;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class HealthPotionTests
{
    [Fact]
    public void It_starts_full()
    {
        var potion = new HealthPotion();

        Assert.Equal(HealthPotion.MaxCharges, potion.Charges);
        Assert.True(potion.IsFull);
    }

    [Fact]
    public void A_drink_heals_its_share_or_what_is_missing_and_spends_a_charge()
    {
        var potion = new HealthPotion();

        Assert.Equal(HealthPotion.Heal, potion.Drink(HealthPotion.Heal * 2));
        Assert.Equal(HealthPotion.Heal / 2, potion.Drink(HealthPotion.Heal / 2));
        Assert.Equal(HealthPotion.MaxCharges - 2, potion.Charges);
    }

    [Fact]
    public void Nothing_is_drunk_with_no_hp_missing_or_no_charge_left()
    {
        var potion = new HealthPotion();

        Assert.Equal(0, potion.Drink(0));
        Assert.True(potion.IsFull);

        for (int i = 0; i < HealthPotion.MaxCharges; i++)
        {
            potion.Drink(HealthPotion.Heal);
        }

        Assert.Equal(0, potion.Drink(HealthPotion.Heal));
        Assert.Equal(0, potion.Charges);
        Assert.Throws<ArgumentOutOfRangeException>(() => potion.Drink(-1));
    }

    [Fact]
    public void Charges_are_added_up_to_full_and_a_refill_fills_it()
    {
        var potion = new HealthPotion();
        Assert.False(potion.AddCharge());

        potion.Drink(HealthPotion.Heal);
        potion.Drink(HealthPotion.Heal);
        Assert.True(potion.AddCharge());
        Assert.Equal(HealthPotion.MaxCharges - 1, potion.Charges);

        potion.Refill();
        Assert.True(potion.IsFull);
    }

    [Fact]
    public void Health_heals_up_to_its_max_and_not_once_dead()
    {
        var health = new Health(100);
        health.TakeDamage(30);

        Assert.Equal(30, health.Heal(50));
        Assert.Equal(health.Max, health.Current);

        health.TakeDamage(health.Max);
        Assert.Equal(0, health.Heal(10));
        Assert.True(health.IsDead);
        Assert.Throws<ArgumentOutOfRangeException>(() => health.Heal(-1));
    }
}
