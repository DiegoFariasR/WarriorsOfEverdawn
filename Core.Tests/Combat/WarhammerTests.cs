using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Trade;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class WarhammerTests
{
    private const float AttackSpeed = 2f;

    private static readonly WeaponDefinition Hammer = Weapons.Warhammer;

    [Fact]
    public void The_warhammer_is_a_weapon_of_steel_and_wood_like_the_others()
    {
        Assert.Contains(Hammer, Weapons.Arms);
        Assert.Same(Hammer, Weapons.ById(Hammer.Id));
        Assert.Null(Hammer.Element);
        Assert.Contains(Sellers.Weaponsmith.ItemsFor(new Buyer(WeaponSets.Default, 0)), item => item.Weapon == Hammer);
    }

    [Fact]
    public void Every_blow_is_a_blunt_one_in_reach_of_the_arm()
    {
        Assert.All(Hammer.Skills, skill =>
        {
            Assert.Null(skill.Projectile);
            Assert.Null(skill.Area);
            Assert.Equal(new[] { (DamageType.Blunt, 1f) }, skill.Parts);
        });
    }

    [Fact]
    public void A_smash_is_the_only_slow_swing_and_the_hardest_on_an_arc()
    {
        var swings = Weapons.Arms.Where(w => w != Hammer && w != Weapons.Spear && w.Primary.Projectile == null).Select(w => w.Primary).ToList();

        Assert.Equal(Skills.HammerWeight, Hammer.Primary.SwingSpeed);
        Assert.True(Skills.HammerWeight < 1f);
        // A second form plays at whatever speed keeps its first form's pace, so only first forms say how quick a weapon is.
        Assert.All(Weapons.All.Where(w => w != Hammer).SelectMany(w => w.Skills.Where(s => s != w.Alternate)), skill => Assert.True(skill.SwingSpeed >= 1f, skill.Id));
        Assert.True(Hammer.Primary.Damage > swings.Max(s => s.Damage));

        // The spear's thrust hits harder still, on a line a third as wide.
        Assert.True(Hammer.Primary.Damage < Weapons.Spear.Primary.Damage);
        Assert.True(Hammer.Primary.HalfArc > Weapons.Spear.Primary.HalfArc);
    }

    [Fact]
    public void A_slow_skill_plays_and_lands_that_much_later()
    {
        var smash = Hammer.Primary;
        var plain = smash with { SwingSpeed = 1f };

        Assert.Equal(AttackSpeed * Skills.HammerWeight, CombatTiming.SwingSpeed(smash, AttackSpeed));
        Assert.Equal(CombatTiming.HitDelay(plain, AttackSpeed) / Skills.HammerWeight, CombatTiming.HitDelay(smash, CombatTiming.SwingSpeed(smash, AttackSpeed)), precision: 5);
    }

    [Fact]
    public void It_builds_more_stun_than_any_other_weapon_blow_for_blow_and_turn_for_turn()
    {
        var others = Weapons.All.Where(w => w != Hammer).ToList();

        Assert.True(Hammer.Primary.Buildup > others.Max(w => w.Primary.Buildup));
        Assert.True(Hammer.Secondary.Buildup > others.Where(w => w.Secondary.Channeled).Max(w => w.Secondary.Buildup));
        Assert.Equal(Hammer.Primary.Buildup, Hammer.Lunge.Buildup);
    }

    [Fact]
    public void Three_smashes_stun_a_body_that_resists_nothing()
    {
        var bars = new StatusBars();
        for (int blow = 0; blow < 2; blow++)
        {
            bars.Build(DamageType.Blunt, Hammer.Primary.Buildup, damage: Hammer.Primary.Damage, Resistances.None);
        }

        Assert.True(bars.Active.HasFlag(Statuses.Dizzy));
        Assert.False(bars.Lost);
        bars.Build(DamageType.Blunt, Hammer.Primary.Buildup, damage: Hammer.Primary.Damage, Resistances.None);
        Assert.True(bars.Lost);
        Assert.True(bars.Active.HasFlag(Statuses.Stunned));
    }

    [Fact]
    public void Its_spin_is_held_turns_slowly_and_costs_as_the_dearest_does()
    {
        var spin = Hammer.Secondary;
        var spins = Weapons.Arms.Where(w => w != Hammer && w.Secondary.Channeled).Select(w => w.Secondary).ToList();

        Assert.True(spin.Channeled);
        Assert.Equal(Skills.HammerWeight, spin.SwingSpeed);
        Assert.True(spin.Damage > spins.Max(s => s.Damage));
        Assert.Equal(spins.Max(s => s.ManaCost), spin.ManaCost);
    }

    [Fact]
    public void Its_lunge_hits_as_one_smash()
    {
        Assert.Equal(Hammer.Primary.Damage, Hammer.Lunge.Damage);
        Assert.True(Hammer.Lunge.Range > Hammer.Primary.Range);
        Assert.Equal(1f, Hammer.Lunge.SwingSpeed);
    }

    [Fact]
    public void Its_guard_stops_nearly_everything_and_slows_most()
    {
        var others = Weapons.Arms.Where(w => w != Hammer).ToList();

        Assert.True(Hammer.Guard.MoveSpeedFactor < others.Min(w => w.Guard.MoveSpeedFactor));
        Assert.True(Hammer.Guard.DamageTaken > 0f && Hammer.Guard.DamageTaken < others.Where(w => w.Guard.DamageTaken > 0f).Min(w => w.Guard.DamageTaken));
    }

    [Fact]
    public void An_enchanted_warhammer_is_part_its_element_and_as_slow()
    {
        foreach (var element in Elements.All)
        {
            var enchanted = Weapons.Enchanted(Hammer, element);

            Assert.Same(enchanted, Weapons.ById($"{Hammer.Id}~{Elements.IdOf(element)}"));
            Assert.Equal(Hammer.Id, enchanted.Kind);
            Assert.All(enchanted.Skills, skill =>
                Assert.Equal(new[] { (DamageType.Blunt, 1f - Weapons.EnchantedShare), (DamageTypes.Of(element), Weapons.EnchantedShare) }, skill.Parts));
            Assert.Equal(Skills.HammerWeight, enchanted.Primary.SwingSpeed);
            Assert.Equal(Hammer.Primary.Buildup, enchanted.Primary.Buildup);
        }
    }

    [Fact]
    public void It_is_improved_like_any_weapon()
    {
        var better = Weapons.AtLevel(Hammer, 5);

        Assert.Equal(Weapons.DamageAtLevel(Hammer.Primary.Damage, 5), better.Primary.Damage);
        Assert.Equal(Skills.HammerWeight, better.Primary.SwingSpeed);
    }
}
