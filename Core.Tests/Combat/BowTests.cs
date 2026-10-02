using System;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using WarriorsOfEverdawn.Core.Trade;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class BowTests
{
    private static readonly WeaponDefinition Bow = Weapons.Bow;

    [Fact]
    public void The_bow_is_a_weapon_of_wood_like_the_others_of_steel_and_wood()
    {
        Assert.Contains(Bow, Weapons.Arms);
        Assert.Same(Bow, Weapons.ById(Bow.Id));
        Assert.Null(Bow.Element);
        Assert.Null(Bow.Guard.Barrier);
        Assert.Contains(Sellers.Weaponsmith.ItemsFor(new Buyer(WeaponSets.Default, 0)), item => item.Weapon == Bow);
    }

    [Fact]
    public void Its_arrows_never_run_out_and_the_shot_costs_no_mana()
    {
        var shot = Bow.Primary;

        Assert.Equal(Projectiles.Arrow, shot.Projectile);
        Assert.Equal(1, shot.Projectiles);
        Assert.Equal(0, shot.ManaCost);
        Assert.True(shot.Cooldown > 0f);
        Assert.False(shot.Channeled);
        Assert.Equal(Projectiles.Arrow.MaxDistance, shot.Range);
    }

    [Fact]
    public void It_reaches_further_than_any_other_weapon_and_a_shot_deals_less_than_a_greatsword_or_a_staff()
    {
        var others = Weapons.All.Where(w => w != Bow).ToList();

        Assert.True(Bow.Primary.Range > others.Max(w => w.Primary.Range));
        Assert.True(Bow.Primary.Damage < Weapons.Greatsword.Primary.Damage);
        Assert.True(Bow.Primary.Damage < Weapons.Staffs.Min(s => s.Primary.Damage * s.Primary.Projectiles));
    }

    [Fact]
    public void The_volley_looses_several_arrows_for_mana_and_waits_longer()
    {
        var shot = Bow.Primary;
        var volley = Bow.Secondary;

        Assert.Equal(Projectiles.Arrow, volley.Projectile);
        Assert.True(volley.Projectiles > 1);
        Assert.True(volley.VolleyInterval > 0f);
        Assert.Equal(volley.HitTime + (volley.Projectiles - 1) * volley.VolleyInterval, volley.SweepEnd!.Value, 4);
        Assert.True(volley.ManaCost > 0);
        Assert.True(volley.Cooldown > shot.Cooldown);
        Assert.False(volley.Channeled);
        Assert.True(volley.Damage < shot.Damage);
        Assert.True(volley.Damage * volley.Projectiles > shot.Damage);
    }

    [Fact]
    public void Arrows_pierce_and_grow_with_str()
    {
        var strong = new CharacterStats(Str: 10, Wis: 0, Agi: 0);
        var wise = new CharacterStats(Str: 0, Wis: 10, Agi: 0);

        Assert.All(Bow.Skills, skill => Assert.Equal(new[] { (DamageType.Pierce, 1f) }, skill.Parts));
        Assert.Equal((int)MathF.Round(Bow.Primary.Damage * (1f + 10 * StatRules.DamagePerStr)), StatRules.Damage(Bow.Primary, strong));
        Assert.Equal(Bow.Primary.Damage, StatRules.Damage(Bow.Primary, wise));
    }

    [Fact]
    public void Its_lunge_is_a_stab_with_an_arrow_in_the_hand()
    {
        Assert.Null(Bow.Lunge.Projectile);
        Assert.True(Bow.Lunge.Range < Weapons.Arms.Where(w => w != Bow).Min(w => w.Lunge.Range));
        Assert.True(Bow.Lunge.Damage < Weapons.Arms.Where(w => w != Bow).Min(w => w.Lunge.Damage));
    }

    [Fact]
    public void It_is_the_poorest_guard_among_the_weapons_of_steel_and_wood()
    {
        var others = Weapons.Arms.Where(w => w != Bow).ToList();

        Assert.True(Bow.Guard.DamageTaken > others.Max(w => w.Guard.DamageTaken));
        Assert.True(Bow.Guard.HalfArc < others.Min(w => w.Guard.HalfArc));
        Assert.True(Bow.Guard.ParryWindow <= others.Min(w => w.Guard.ParryWindow));
    }

    [Fact]
    public void An_enchanted_bow_looses_arrows_that_are_part_their_element_and_still_arrows()
    {
        foreach (var element in Elements.All)
        {
            var enchanted = Weapons.Enchanted(Bow, element);

            Assert.Same(enchanted, Weapons.ById($"{Bow.Id}~{Elements.IdOf(element)}"));
            Assert.Equal(Bow.Id, enchanted.Kind);
            Assert.All(enchanted.Skills, skill =>
                Assert.Equal(new[] { (DamageType.Pierce, 1f - Weapons.EnchantedShare), (DamageTypes.Of(element), Weapons.EnchantedShare) }, skill.Parts));
            Assert.Equal(Projectiles.Arrow, enchanted.Primary.Projectile);
            Assert.Equal(Bow.Primary.Projectiles, enchanted.Primary.Projectiles);
            Assert.Equal(Bow.Secondary.Projectiles, enchanted.Secondary.Projectiles);
            Assert.Equal(Bow.Primary.Damage, enchanted.Primary.Damage);
            Assert.Equal(DamageType.Pierce, enchanted.Primary.Type);
        }
    }

    [Fact]
    public void It_is_improved_like_any_weapon()
    {
        var better = Weapons.AtLevel(Bow, 3);

        Assert.Equal(Weapons.DamageAtLevel(Bow.Primary.Damage, 3), better.Primary.Damage);
        Assert.Equal(Weapons.DamageAtLevel(Bow.Secondary.Damage, 3), better.Secondary.Damage);
        Assert.Equal(Projectiles.Arrow, better.Primary.Projectile);
    }
}
