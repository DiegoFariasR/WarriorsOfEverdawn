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
    public void Every_weapon_pairs_a_swing_with_a_paid_channel()
    {
        Assert.NotEmpty(Weapons.All);
        foreach (var weapon in Weapons.All)
        {
            Assert.False(weapon.Primary.Channeled, weapon.Id);
            Assert.True(weapon.Secondary.Channeled, weapon.Id);
            Assert.True(weapon.Secondary.ManaCost > 0, weapon.Id);
            Assert.NotNull(weapon.Secondary.SweepEnd);
            Assert.True(weapon.Secondary.MoveSpeedFactor is > 0f and < 1f, weapon.Id);
        }
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
        // The spear's primary is already a thrust, so its lunge matches it; the others double their swing.
        Assert.Equal(Weapons.Spear.Primary.Damage, Weapons.Spear.Lunge.Damage);
        Assert.Equal(Weapons.Spear.Primary.HalfArc, Weapons.Spear.Lunge.HalfArc);
        foreach (var weapon in Weapons.All.Where(w => w != Weapons.Spear))
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
    public void Every_weapon_guards_a_front_arc_slows_and_parries_briefly()
    {
        foreach (var weapon in Weapons.All)
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
    public void Weapon_and_skill_ids_are_unique()
    {
        Assert.Equal(Weapons.All.Count, Weapons.All.Select(w => w.Id).Distinct().Count());
        Assert.Equal(AllSkills.Count(), AllSkills.Select(s => s.Id).Distinct().Count());
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
        Assert.Throws<KeyNotFoundException>(() => Weapons.Next(Weapons.Default with { Id = "copy" }));
    }
}
