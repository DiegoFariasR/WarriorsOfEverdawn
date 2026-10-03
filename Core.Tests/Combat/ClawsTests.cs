using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Trade;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class ClawsTests
{
    private const float AttackSpeed = 2f;

    private static readonly WeaponDefinition Claws = Weapons.Claws;

    [Fact]
    public void The_claws_are_one_weapon_of_steel_like_the_others()
    {
        Assert.Contains(Claws, Weapons.Arms);
        Assert.Same(Claws, Weapons.ById(Claws.Id));
        Assert.Null(Claws.Element);
        Assert.Contains(Sellers.Weaponsmith.ItemsFor(new Buyer(WeaponSets.Default, 0)), item => item.Weapon == Claws);
    }

    [Fact]
    public void Every_blow_is_a_melee_slash()
    {
        Assert.All(Claws.Skills, skill =>
        {
            Assert.Null(skill.Projectile);
            Assert.Null(skill.Area);
            Assert.Equal(new[] { (DamageType.Slash, 1f) }, skill.Parts);
        });
    }

    [Fact]
    public void A_rake_is_the_lightest_and_shortest_swing_and_the_only_quick_one()
    {
        var swings = Weapons.Arms.Where(w => w != Claws && w.Primary.Projectile == null).Select(w => w.Primary).ToList();

        Assert.True(Claws.Primary.Damage < swings.Min(s => s.Damage));
        Assert.True(Claws.Primary.Range < swings.Min(s => s.Range));
        Assert.Equal(Skills.ClawQuickness, Claws.Primary.SwingSpeed);
        Assert.True(Skills.ClawQuickness > 1f);
        // A second form plays at whatever speed keeps its first form's pace, so only first forms say how quick a weapon is.
        Assert.All(Weapons.All.Where(w => w != Claws).SelectMany(w => w.Skills.Where(s => s != w.Alternate)), skill => Assert.True(skill.SwingSpeed <= 1f, skill.Id));
    }

    [Fact]
    public void A_quick_skill_plays_and_lands_that_much_sooner()
    {
        var rake = Claws.Primary;
        var plain = rake with { SwingSpeed = 1f };

        Assert.Equal(AttackSpeed * Skills.ClawQuickness, CombatTiming.SwingSpeed(rake, AttackSpeed));
        Assert.Equal(AttackSpeed, CombatTiming.SwingSpeed(plain, AttackSpeed));
        Assert.Equal(CombatTiming.HitDelay(plain, AttackSpeed) / Skills.ClawQuickness, CombatTiming.HitDelay(rake, CombatTiming.SwingSpeed(rake, AttackSpeed)), precision: 5);
    }

    [Fact]
    public void Blow_for_blow_it_deals_less_than_a_sword_and_over_time_more()
    {
        var slash = Weapons.SwordAndShield.Primary;
        var rake = Claws.Primary;

        Assert.True(rake.Damage < slash.Damage);
        Assert.True(rake.Damage * rake.SwingSpeed > slash.Damage * slash.SwingSpeed);
    }

    [Fact]
    public void Its_spin_is_held_and_turns_as_much_faster()
    {
        var spin = Claws.Secondary;

        Assert.True(spin.Channeled);
        Assert.True(spin.ManaCost > 0);
        Assert.Equal(Skills.ClawQuickness, spin.SwingSpeed);
        Assert.True(spin.Range < Weapons.Arms.Where(w => w != Claws && w.Secondary.Channeled).Min(w => w.Secondary.Range));
    }

    [Fact]
    public void Its_lunge_is_a_punch_that_hits_as_two_rakes()
    {
        Assert.Equal(Claws.Primary.Damage * Skills.ThrustDamageFactor, Claws.Lunge.Damage);
        Assert.True(Claws.Lunge.Range > Claws.Primary.Range);
        Assert.Equal(1f, Claws.Lunge.SwingSpeed);
    }

    [Fact]
    public void Enchanted_claws_are_part_their_element_and_as_quick()
    {
        foreach (var element in Elements.All)
        {
            var enchanted = Weapons.Enchanted(Claws, element);

            Assert.Same(enchanted, Weapons.ById($"{Claws.Id}~{Elements.IdOf(element)}"));
            Assert.Equal(Claws.Id, enchanted.Kind);
            Assert.All(enchanted.Skills, skill =>
                Assert.Equal(new[] { (DamageType.Slash, 1f - Weapons.EnchantedShare), (DamageTypes.Of(element), Weapons.EnchantedShare) }, skill.Parts));
            Assert.Equal(Skills.ClawQuickness, enchanted.Primary.SwingSpeed);
            Assert.Equal(Claws.Primary.Damage, enchanted.Primary.Damage);
        }
    }

    [Fact]
    public void They_are_improved_like_any_weapon()
    {
        var better = Weapons.AtLevel(Claws, 4);

        Assert.Equal(Weapons.DamageAtLevel(Claws.Primary.Damage, 4), better.Primary.Damage);
        Assert.Equal(Skills.ClawQuickness, better.Primary.SwingSpeed);
    }
}
