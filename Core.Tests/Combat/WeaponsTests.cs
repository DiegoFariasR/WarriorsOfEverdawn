using System;
using System.Collections.Generic;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class WeaponsTests
{
    private static IEnumerable<SkillDefinition> AllSkills => Weapons.All.SelectMany(w => new[] { w.Primary, w.Secondary, w.Lunge });

    [Fact]
    public void Every_weapon_pairs_a_free_primary_with_a_secondary_paid_in_mana()
    {
        Assert.NotEmpty(Weapons.All);
        foreach (var weapon in Weapons.All)
        {
            Assert.False(weapon.Primary.Channeled, weapon.Id);
            Assert.Equal(0, weapon.Primary.ManaCost);
            Assert.True(weapon.Secondary.ManaCost > 0, weapon.Id);
        }
    }

    [Fact]
    public void Every_secondary_but_a_staffs_and_the_bows_is_held_cycle_after_cycle_and_slows_its_caster()
    {
        foreach (var weapon in Weapons.All.Where(w => !Weapons.Staffs.Contains(w) && w != Weapons.Bow))
        {
            Assert.True(weapon.Secondary.Channeled, weapon.Id);
            Assert.NotNull(weapon.Secondary.SweepEnd);
            Assert.True(weapon.Secondary.MoveSpeedFactor is > 0f and < 1f, weapon.Id);
        }
    }

    [Fact]
    public void Every_weapon_of_steel_but_the_bow_strikes_its_primary_in_two_forms_in_turn()
    {
        foreach (var weapon in Weapons.Arms.Where(w => w != Weapons.Bow))
        {
            var second = weapon.Alternate;

            Assert.NotNull(second);
            Assert.NotEqual(weapon.Primary.Id, second!.Id);
            Assert.Equal(weapon.Primary.Name, second.Name);
            Assert.Equal(weapon.Primary.Damage, second.Damage);
            Assert.Equal(weapon.Primary.Type, second.Type);
            Assert.Equal(weapon.Primary.Buildup, second.Buildup);
            Assert.Equal(weapon.Primary.HalfArc, second.HalfArc);
            Assert.Contains(second, weapon.Skills);
            Assert.Equal(new[] { weapon.Primary, second, weapon.Primary, second }, Enumerable.Range(0, 4).Select(weapon.PrimaryFor));
        }
    }

    // A thrust is drawn as one (rings along its line); a swing, a Spin, a spell or a shot is not.
    [Fact]
    public void The_thrusts_are_the_spears_thrust_in_both_forms_and_every_weapons_lunge()
    {
        foreach (var weapon in Weapons.All)
        {
            var made = Weapons.AtLevel(weapon, 2);
            Assert.True(weapon.Lunge.Thrust, weapon.Id);
            Assert.True(made.Lunge.Thrust, made.Id);
            Assert.False(weapon.Secondary.Thrust, weapon.Id);
            Assert.Equal(weapon == Weapons.Spear, weapon.Primary.Thrust);
            Assert.Equal(weapon == Weapons.Spear, weapon.Alternate?.Thrust ?? false);
        }

        Assert.All(Weapons.All.SelectMany(w => w.Skills).Where(s => s.Thrust), s => Assert.Null(s.Projectile));
    }

    [Fact]
    public void Magic_and_the_bow_strike_their_primary_in_one_form()
    {
        foreach (var weapon in Weapons.All.Where(w => w.Element != null).Append(Weapons.Bow))
        {
            Assert.Null(weapon.Alternate);
            Assert.All(Enumerable.Range(0, 4), swing => Assert.Same(weapon.Primary, weapon.PrimaryFor(swing)));
        }
    }

    [Fact]
    public void An_improved_and_enchanted_weapons_second_form_hits_as_hard_as_its_first()
    {
        var made = Weapons.Enchanted(Weapons.AtLevel(Weapons.Greatsword, 3), Element.Fire);
        var second = made.Alternate!;

        Assert.Equal(made.Primary.Damage, second.Damage);
        Assert.True(second.Damage > Weapons.Greatsword.Alternate!.Damage);
        Assert.Equal(Element.Fire, second.Element);
        Assert.Equal(made.Primary.MagicShare, second.MagicShare);
        Assert.Same(second, new WeaponSets(made, null).SkillById(second.Id));
    }

    [Fact]
    public void Every_weapon_lunges_with_a_free_thrust_along_a_narrow_line()
    {
        foreach (var weapon in Weapons.All)
        {
            var lunge = weapon.Lunge;
            Assert.False(lunge.Channeled, weapon.Id);
            Assert.Equal(0, lunge.ManaCost);
            Assert.True(lunge.HalfArc <= weapon.Primary.HalfArc, weapon.Id);
            Assert.True(lunge.SweepEnd > lunge.HitTime, weapon.Id);
        }
    }

    [Fact]
    public void A_thrust_hits_as_hard_as_the_thrust_factor_times_the_swing()
    {
        // The spear's primary is already a thrust, so its lunge matches it; the others double their swing. A staff's
        // lunge is a poke with the staff, nothing to do with what it casts (StaffsTests), the bow's a stab with
        // an arrow, nothing to do with what it looses (BowTests), and the warhammer's hits as one Smash
        // (WarhammerTests).
        Assert.Equal(Weapons.Spear.Primary.Damage, Weapons.Spear.Lunge.Damage);
        Assert.Equal(Weapons.Spear.Primary.HalfArc, Weapons.Spear.Lunge.HalfArc);
        foreach (var weapon in Weapons.Arms.Where(w => w != Weapons.Spear && w != Weapons.Bow && w != Weapons.Warhammer))
        {
            Assert.Equal(weapon.Primary.Damage * Skills.ThrustDamageFactor, weapon.Lunge.Damage);
        }
    }

    [Fact]
    public void Every_skill_is_named_hits_and_reaches()
    {
        foreach (var skill in AllSkills)
        {
            Assert.False(string.IsNullOrWhiteSpace(skill.Name), skill.Id);
            Assert.True(skill.Damage > 0, skill.Id);
            Assert.True(skill.Range > 0f, skill.Id);
            Assert.True(skill.HalfArc is > 0f and <= MathF.PI, skill.Id);
            Assert.True(skill.HitTime >= 0f, skill.Id);
        }
    }

    [Fact]
    public void Every_weapon_of_steel_or_wood_guards_a_front_arc_slows_and_parries_briefly()
    {
        foreach (var weapon in Weapons.Arms)
        {
            var guard = weapon.Guard;
            Assert.True(guard.HalfArc is > 0f and < MathF.PI, weapon.Id);
            Assert.True(guard.DamageTaken is >= 0f and < 1f, weapon.Id);
            Assert.True(guard.MoveSpeedFactor is > 0f and < 1f, weapon.Id);
            Assert.True(guard.ParryWindow is > 0f and < Guard.Recovery, weapon.Id);
            Assert.False(string.IsNullOrWhiteSpace(guard.Name), weapon.Id);
        }
    }

    [Fact]
    public void The_sword_and_shield_guards_best_of_all_and_hits_least_but_for_the_claws()
    {
        var pair = Weapons.SwordAndShield;
        var others = Weapons.Arms.Where(w => w != pair).ToList();

        Assert.NotEmpty(others);
        Assert.All(others, w => Assert.True(pair.Guard.HalfArc > w.Guard.HalfArc, w.Id));
        Assert.All(others, w => Assert.True(pair.Guard.DamageTaken <= w.Guard.DamageTaken, w.Id));
        Assert.All(others, w => Assert.True(pair.Guard.MoveSpeedFactor > w.Guard.MoveSpeedFactor, w.Id));
        Assert.All(others, w => Assert.True(pair.Guard.ParryWindow > w.Guard.ParryWindow, w.Id));
        // The claws hit lighter still, blow for blow, and land more of them (ClawsTests).
        Assert.All(others.Where(w => w != Weapons.Claws), w => Assert.True(pair.Primary.Damage < w.Primary.Damage, w.Id));
        Assert.All(others.Where(w => w != Weapons.Claws), w => Assert.True(pair.Secondary.Damage < w.Secondary.Damage, w.Id));
    }

    [Fact]
    public void Weapon_and_skill_ids_are_unique()
    {
        Assert.Equal(Weapons.All.Count, Weapons.All.Select(w => w.Id).Distinct().Count());
        // A wand throws the very shot the staff of its element does: one skill under one id, in two weapons.
        Assert.Equal(AllSkills.Distinct().Count(), AllSkills.Select(s => s.Id).Distinct().Count());
        Assert.All(AllSkills.GroupBy(s => s.Id), sameId => Assert.Single(sameId.Distinct()));
    }

    [Fact]
    public void Buttons_map_to_the_primary_and_secondary_skills()
    {
        foreach (var weapon in Weapons.All)
        {
            Assert.Same(weapon.Primary, weapon.Skill(WeaponDefinition.PrimaryButton));
            Assert.Same(weapon.Secondary, weapon.Skill(WeaponDefinition.SecondaryButton));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => Weapons.Default.Skill(2));
    }

    [Fact]
    public void Looks_up_weapons_and_skills_by_id_and_fails_loudly_on_unknown()
    {
        foreach (var weapon in Weapons.All)
        {
            Assert.Same(weapon, Weapons.ById(weapon.Id));
            Assert.Same(weapon.Primary, Weapons.SkillById(weapon.Primary.Id));
            Assert.Same(weapon.Secondary, Weapons.SkillById(weapon.Secondary.Id));
            Assert.Same(weapon.Lunge, Weapons.SkillById(weapon.Lunge.Id));
        }

        Assert.Throws<KeyNotFoundException>(() => Weapons.ById("no-such-weapon"));
        Assert.Throws<KeyNotFoundException>(() => Weapons.SkillById(Skills.MinionChop.Id));
    }

    [Fact]
    public void Next_visits_every_weapon_once_and_wraps_around()
    {
        var seen = new List<WeaponDefinition>();
        var weapon = Weapons.Default;
        for (int i = 0; i < Weapons.All.Count; i++)
        {
            seen.Add(weapon);
            weapon = Weapons.Next(weapon);
        }

        Assert.Same(Weapons.Default, weapon);
        Assert.Equal(Weapons.All.OrderBy(w => w.Id), seen.OrderBy(w => w.Id));
        Assert.Throws<KeyNotFoundException>(() => Weapons.Next(Weapons.Default with { Kind = "copy" }));
    }

    [Fact]
    public void A_plain_weapon_is_level_nothing_and_its_own_kind()
    {
        foreach (var weapon in Weapons.All)
        {
            Assert.Equal(0, weapon.Level);
            Assert.Equal(weapon.Id, weapon.Kind);
            Assert.Same(weapon, Weapons.AtLevel(weapon, 0));
            Assert.Same(weapon, Weapons.Plain(weapon));
        }
    }

    [Fact]
    public void Every_level_of_every_weapon_hits_harder_than_the_one_before_with_every_skill()
    {
        foreach (var plain in Weapons.All)
        {
            for (int level = 1; level <= WeaponDefinition.MaxLevel; level++)
            {
                var before = Weapons.AtLevel(plain, level - 1).Skills.ToList();
                var after = Weapons.AtLevel(plain, level).Skills.ToList();

                for (int i = 0; i < after.Count; i++)
                {
                    Assert.True(after[i].Damage > before[i].Damage, $"{plain.Id} +{level}: {after[i].Id}");
                }
            }
        }
    }

    [Fact]
    public void A_level_adds_its_share_of_the_plain_damage_and_never_less_than_one()
    {
        const int Big = 40;
        const int Small = 3;

        Assert.Equal(Big + (int)(Big * Weapons.DamagePerLevel) * 3, Weapons.DamageAtLevel(Big, 3));
        Assert.Equal(Small + 3, Weapons.DamageAtLevel(Small, 3));
        Assert.Equal(Big, Weapons.DamageAtLevel(Big, 0));
        Assert.Equal(0, Weapons.DamageAtLevel(0, WeaponDefinition.MaxLevel));
    }

    [Fact]
    public void An_improved_weapon_is_the_same_weapon_in_all_but_damage_level_and_name()
    {
        foreach (var plain in Weapons.All)
        {
            var improved = Weapons.AtLevel(plain, 3);

            Assert.Equal(3, improved.Level);
            Assert.Equal(plain.Id, improved.Kind);
            Assert.NotEqual(plain.Id, improved.Id);
            Assert.Contains(plain.Name, improved.Name);
            Assert.Contains("3", improved.Name);
            Assert.Equal(plain.Guard, improved.Guard);
            Assert.Equal(plain.Skills.Select(s => s with { Damage = 0 }), improved.Skills.Select(s => s with { Damage = 0 }));
            Assert.Same(plain, Weapons.Plain(improved));
        }
    }

    [Fact]
    public void A_weapon_is_found_by_its_id_at_every_level()
    {
        foreach (var plain in Weapons.All)
        {
            for (int level = 0; level <= WeaponDefinition.MaxLevel; level++)
            {
                var weapon = Weapons.AtLevel(plain, level);

                Assert.Same(weapon, Weapons.ById(weapon.Id));
            }
        }
    }

    [Fact]
    public void Levels_past_the_best_and_ids_that_name_none_fail_loudly()
    {
        string best = Weapons.AtLevel(Weapons.Spear, WeaponDefinition.MaxLevel).Id;
        string pastBest = best.Replace(WeaponDefinition.MaxLevel.ToString(), (WeaponDefinition.MaxLevel + 1).ToString());

        Assert.Throws<ArgumentOutOfRangeException>(() => Weapons.AtLevel(Weapons.Spear, WeaponDefinition.MaxLevel + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Weapons.AtLevel(Weapons.Spear, -1));
        Assert.Throws<KeyNotFoundException>(() => Weapons.ById(pastBest));
        Assert.Throws<KeyNotFoundException>(() => Weapons.ById(Weapons.Spear.Id + "+"));
        Assert.Throws<KeyNotFoundException>(() => Weapons.ById(Weapons.Spear.Id + "+0"));
        Assert.Throws<KeyNotFoundException>(() => Weapons.ById("ladle+2"));
    }

    [Fact]
    public void The_weapon_after_an_improved_one_is_the_plain_weapon_after_its_kind()
    {
        foreach (var plain in Weapons.All)
        {
            Assert.Same(Weapons.Next(plain), Weapons.Next(Weapons.AtLevel(plain, 5)));
        }
    }
}
