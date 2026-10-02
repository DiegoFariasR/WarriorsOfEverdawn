using System;
using System.Linq;
using System.Numerics;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class WandsTests
{
    [Fact]
    public void There_is_a_wand_for_every_element_beside_its_staff()
    {
        Assert.Equal(Elements.All, Weapons.Wands.Select(w => w.Element!.Value));
        foreach (var element in Elements.All)
        {
            var wand = Weapons.Wand(element);

            Assert.Equal(element, wand.Element);
            Assert.Same(wand, Weapons.ById(wand.Id));
            Assert.True(Weapons.IsWand(wand));
            Assert.True(Weapons.IsWand(Weapons.AtLevel(wand, 3)));
            Assert.False(Weapons.IsWand(Weapons.Staff(element)));
            Assert.Contains(wand, Weapons.All);
            Assert.NotEqual(Weapons.Staff(element).Id, wand.Id);
        }

        Assert.All(Weapons.Arms, w => Assert.False(Weapons.IsWand(w)));
    }

    [Fact]
    public void A_wand_throws_the_same_shot_and_raises_the_same_barrier_as_the_staff_of_its_element()
    {
        foreach (var element in Elements.All)
        {
            var wand = Weapons.Wand(element);
            var staff = Weapons.Staff(element);

            Assert.Same(staff.Primary, wand.Primary);
            Assert.Same(staff.Guard, wand.Guard);
            Assert.Same(Weapons.Barrier, wand.Guard);
        }
    }

    [Fact]
    public void A_wands_secondary_is_one_ball_thrown_for_mana_that_bursts_and_not_a_spell_held()
    {
        foreach (var wand in Weapons.Wands)
        {
            var burst = wand.Secondary;

            Assert.False(burst.Channeled, wand.Id);
            Assert.Null(burst.Area);
            Assert.NotNull(burst.Projectile);
            Assert.Equal(1, burst.Projectiles);
            Assert.True(burst.BlastRadius > burst.Projectile!.Radius, wand.Id);
            Assert.True(burst.ManaCost > 0, wand.Id);
            Assert.True(burst.Cooldown > 0f, wand.Id);
            Assert.Equal(wand.Element, burst.Element);
            Assert.Equal(burst.Projectile.MaxDistance, burst.Range);
            Assert.False(string.IsNullOrWhiteSpace(burst.Name));
        }

        Assert.Equal("Fireball", Weapons.Wand(Element.Fire).Secondary.Name);
        Assert.Equal(Weapons.Wands.Count, Weapons.Wands.Select(w => w.Secondary.Id).Distinct().Count());
    }

    [Fact]
    public void Only_a_wands_ball_bursts()
    {
        var bursting = Weapons.All.SelectMany(w => w.Skills).Where(s => s.BlastRadius > 0f).Distinct().ToList();

        Assert.Equal(Weapons.Wands.Select(w => w.Secondary), bursting);
        Assert.All(Weapons.Staffs, s => Assert.Equal(0f, s.Primary.BlastRadius));
    }

    [Fact]
    public void A_burst_catches_every_body_it_touches_and_none_beyond()
    {
        var at = new Vector2(4f, -1f);
        const float Radius = 2.5f;

        Assert.True(Projectiles.Blasts(at, Radius, at, 0f));
        Assert.True(Projectiles.Blasts(at, Radius, at + new Vector2(Radius, 0f), 0f));
        Assert.False(Projectiles.Blasts(at, Radius, at + new Vector2(Radius + 0.01f, 0f), 0f));
        Assert.True(Projectiles.Blasts(at, Radius, at + new Vector2(0f, Radius + BodySize.Radius), BodySize.Radius));
        Assert.False(Projectiles.Blasts(at, Radius, at + new Vector2(0f, Radius + BodySize.Radius + 0.01f), BodySize.Radius));
    }

    [Fact]
    public void A_wands_lunge_is_a_jab_with_the_wand_and_reaches_less_than_any_blade_or_staff()
    {
        foreach (var wand in Weapons.Wands)
        {
            Assert.Null(wand.Lunge.Element);
            Assert.Null(wand.Lunge.Projectile);
            Assert.True(wand.Lunge.Range < Weapons.All.Where(w => !Weapons.IsWand(w) && w != Weapons.Bow).Min(w => w.Lunge.Range), wand.Id);
        }
    }

    [Fact]
    public void A_wand_is_attuned_to_a_wand_and_a_staff_to_a_staff()
    {
        var wand = Weapons.AtLevel(Weapons.Wand(Element.Fire), 2);
        var staff = Weapons.AtLevel(Weapons.Staff(Element.Fire), 2);

        foreach (var element in Elements.All)
        {
            Assert.Same(Weapons.AtLevel(Weapons.Wand(element), 2), Weapons.Attuned(wand, element));
            Assert.Same(Weapons.AtLevel(Weapons.Staff(element), 2), Weapons.Attuned(staff, element));
        }

        Assert.Throws<ArgumentException>(() => Weapons.Enchanted(Weapons.Wand(Element.Fire), Element.Water));
    }

    [Fact]
    public void An_improved_wand_bursts_harder()
    {
        var wand = Weapons.Wand(Element.Void);
        var better = Weapons.AtLevel(wand, 4);

        Assert.True(better.Secondary.Damage > wand.Secondary.Damage);
        Assert.Equal(wand.Secondary.BlastRadius, better.Secondary.BlastRadius);
        Assert.True(better.Primary.Damage > wand.Primary.Damage);
    }
}
