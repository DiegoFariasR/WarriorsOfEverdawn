using System;
using System.Linq;
using System.Numerics;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class StaffsTests
{
    private static readonly AreaDefinition Ahead = new(Distance: 4f, Radius: 2f);

    [Fact]
    public void There_is_a_staff_for_each_of_six_elements_and_two_astral_types()
    {
        Assert.Equal(8, Elements.All.Count);
        Assert.Equal(new[] { Element.Fire, Element.Water, Element.Ice, Element.Wind, Element.Lightning, Element.Earth }, Elements.All.Where(e => !Elements.IsAstral(e)));
        Assert.Equal(new[] { Element.Divine, Element.Void }, Elements.All.Where(Elements.IsAstral));
        Assert.Equal(Elements.All, Weapons.Staffs.Select(s => s.Element!.Value));
        foreach (var element in Elements.All)
        {
            var staff = Weapons.Staff(element);

            Assert.Equal(element, staff.Element);
            Assert.Same(staff, Weapons.ById(staff.Id));
            Assert.Contains(staff, Weapons.All);
            Assert.DoesNotContain(staff, Weapons.Arms);
        }

        Assert.All(Weapons.Arms, w => Assert.Null(w.Element));
    }

    [Fact]
    public void Every_staff_throws_its_element_from_afar_as_a_bolt_or_a_volley_for_no_mana()
    {
        foreach (var staff in Weapons.Staffs)
        {
            var thrown = staff.Primary;

            Assert.NotNull(thrown.Projectile);
            Assert.Equal(staff.Element, thrown.Element);
            Assert.Equal(0, thrown.ManaCost);
            Assert.True(thrown.Cooldown > 0f, staff.Id);
            Assert.False(thrown.Channeled, staff.Id);
            Assert.Equal(thrown.Projectile!.MaxDistance, thrown.Range);
            Assert.True(thrown.Range > Weapons.Arms.Where(w => w != Weapons.Bow).Max(w => w.Primary.Range), staff.Id);
            Assert.True(thrown.Projectiles >= 1, staff.Id);
        }
    }

    [Fact]
    public void Fire_earth_and_divine_throw_one_bolt_and_the_other_five_a_volley()
    {
        var bolts = new[] { Element.Fire, Element.Earth, Element.Divine };

        foreach (var staff in Weapons.Staffs)
        {
            var thrown = staff.Primary;
            if (bolts.Contains(staff.Element!.Value))
            {
                Assert.Equal(1, thrown.Projectiles);
                Assert.Null(thrown.SweepEnd);
                Assert.EndsWith("-bolt", thrown.Id);
            }
            else
            {
                Assert.True(thrown.Projectiles > 1, staff.Id);
                Assert.True(thrown.VolleyInterval > 0f, staff.Id);
                Assert.Equal(thrown.HitTime + (thrown.Projectiles - 1) * thrown.VolleyInterval, thrown.SweepEnd!.Value, 4);
                Assert.EndsWith("-volley", thrown.Id);
            }
        }

        Assert.Equal(bolts.Length, Weapons.Staffs.Count(s => s.Primary.Projectiles == 1));
    }

    [Fact]
    public void A_volley_deals_all_told_what_a_bolt_does()
    {
        int bolt = Weapons.Staff(Element.Fire).Primary.Damage;

        foreach (var staff in Weapons.Staffs)
        {
            Assert.Equal(bolt, staff.Primary.Damage * staff.Primary.Projectiles);
        }
    }

    [Fact]
    public void Every_staff_holds_a_paid_spell_of_its_element_on_an_area()
    {
        foreach (var staff in Weapons.Staffs)
        {
            var spell = staff.Secondary;

            Assert.True(spell.Channeled, staff.Id);
            Assert.NotNull(spell.Area);
            Assert.Null(spell.Projectile);
            Assert.Equal(staff.Element, spell.Element);
            Assert.True(spell.ManaCost > 0, staff.Id);
            Assert.True(spell.Area!.Radius > 0f && spell.Area.Distance >= 0f, staff.Id);
            Assert.Equal(spell.Area.Reach, spell.Range);
        }

        Assert.Equal(Weapons.Staffs.Count, Weapons.Staffs.Select(s => s.Secondary.Name).Distinct().Count());
        Assert.Equal(0f, Weapons.Staff(Element.Divine).Secondary.Area!.Distance);
    }

    [Fact]
    public void Every_staff_guards_with_the_same_barrier_all_round()
    {
        foreach (var staff in Weapons.Staffs)
        {
            Assert.Same(Weapons.Barrier, staff.Guard);
        }

        var barrier = Weapons.Barrier;
        Assert.NotNull(barrier.Barrier);
        Assert.Equal(MathF.PI, barrier.HalfArc);
        Assert.Equal(0f, barrier.DamageTaken);
        Assert.Equal(0f, barrier.ParryWindow);
        Assert.True(barrier.MoveSpeedFactor is > 0f and < 1f);
        Assert.True(barrier.Barrier!.Strength > 0 && barrier.Barrier.RechargePerSecond > 0f && barrier.Barrier.RechargeDelay > 0f);
        Assert.All(Weapons.Arms, w => Assert.Null(w.Guard.Barrier));
    }

    [Fact]
    public void A_staffs_lunge_is_a_blow_with_the_staff_and_no_spell()
    {
        foreach (var staff in Weapons.Staffs)
        {
            Assert.Null(staff.Lunge.Element);
            Assert.Null(staff.Lunge.Projectile);
            Assert.Null(staff.Lunge.Area);
            Assert.True(staff.Lunge.Damage <= Weapons.Arms.Where(w => w != Weapons.Bow).Min(w => w.Lunge.Damage), staff.Id);
        }
    }

    [Fact]
    public void An_area_ahead_covers_what_stands_round_its_spot_and_not_the_caster()
    {
        var caster = new Vector2(3f, -2f);
        float aim = 0.7f;
        var spot = SkillArea.Centre(caster, aim, Ahead);

        Assert.Equal(Ahead.Distance, Vector2.Distance(caster, spot), 4);
        Assert.Equal(caster + Ground.Forward(aim) * Ahead.Distance, spot);
        Assert.True(SkillArea.Covers(caster, aim, Ahead, spot, 0f));
        Assert.True(SkillArea.Covers(caster, aim, Ahead, spot + new Vector2(Ahead.Radius, 0f), 0f));
        Assert.False(SkillArea.Covers(caster, aim, Ahead, spot + new Vector2(Ahead.Radius + 0.01f, 0f), 0f));
        Assert.False(SkillArea.Covers(caster, aim, Ahead, caster, 0f));
    }

    [Fact]
    public void A_body_touching_an_area_is_in_it()
    {
        var edge = SkillArea.Centre(Vector2.Zero, 0f, Ahead) + new Vector2(Ahead.Radius + BodySize.Radius, 0f);

        Assert.True(SkillArea.Covers(Vector2.Zero, 0f, Ahead, edge, BodySize.Radius));
        Assert.False(SkillArea.Covers(Vector2.Zero, 0f, Ahead, edge + new Vector2(0.01f, 0f), BodySize.Radius));
    }

    [Fact]
    public void An_area_round_the_caster_catches_what_stands_behind_it()
    {
        var nova = Weapons.Staff(Element.Divine).Secondary;
        var behind = -Ground.Forward(0f) * (nova.Area!.Radius - 0.1f);

        Assert.True(SkillHits.Catches(Vector2.Zero, 0f, nova, behind, 0f));
        Assert.False(SkillHits.Catches(Vector2.Zero, 0f, nova, behind * 2f, 0f));
    }

    [Fact]
    public void A_skill_catches_by_its_area_or_its_arc_and_a_thrown_one_catches_nothing_itself()
    {
        var staff = Weapons.Staff(Element.Fire);
        var inArea = SkillArea.Centre(Vector2.Zero, 0f, staff.Secondary.Area!);
        var close = Ground.Forward(0f) * 1f;

        Assert.True(SkillHits.Catches(Vector2.Zero, 0f, staff.Secondary, inArea, 0f));
        Assert.False(SkillHits.Catches(Vector2.Zero, 0f, staff.Secondary, close, 0f));
        Assert.False(SkillHits.Catches(Vector2.Zero, 0f, staff.Primary, close, BodySize.Radius));
        Assert.True(SkillHits.Catches(Vector2.Zero, 0f, Weapons.Greatsword.Primary, close, BodySize.Radius));
        Assert.Equal(
            MeleeArc.Hits(Vector2.Zero, 0f, staff.Lunge, close, BodySize.Radius),
            SkillHits.Catches(Vector2.Zero, 0f, staff.Lunge, close, BodySize.Radius));
    }

    [Fact]
    public void A_barrier_takes_blows_until_it_is_spent_and_the_rest_gets_through()
    {
        var barrier = new BarrierDefinition(Strength: 40, RechargePerSecond: 8f, RechargeDelay: 2f);
        var pool = new BarrierPool(barrier);

        Assert.Equal(barrier.Strength, pool.Left);
        Assert.Equal(0, pool.Absorb(barrier.Strength - 10));
        Assert.Equal(10, pool.Left);
        Assert.True(pool.Holds);
        Assert.Equal(5, pool.Absorb(15));
        Assert.Equal(0, pool.Left);
        Assert.False(pool.Holds);
        Assert.Equal(7, pool.Absorb(7));
    }

    [Fact]
    public void A_barrier_comes_back_only_once_it_has_been_down_a_while_and_never_past_full()
    {
        var barrier = new BarrierDefinition(Strength: 40, RechargePerSecond: 8f, RechargeDelay: 2f);
        var pool = new BarrierPool(barrier);
        pool.Absorb(barrier.Strength);

        pool.Advance(10f, up: true);
        Assert.Equal(0, pool.Left);

        pool.Advance(barrier.RechargeDelay - 0.1f, up: false);
        Assert.Equal(0, pool.Left);

        pool.Advance(0.1f, up: false);
        pool.Advance(1f, up: false);
        Assert.Equal((int)barrier.RechargePerSecond, pool.Left);

        pool.Advance(100f, up: false);
        Assert.Equal(barrier.Strength, pool.Left);

        // Raising it again starts the wait over.
        pool.Absorb(10);
        pool.Advance(0.5f, up: true);
        pool.Advance(barrier.RechargeDelay - 0.1f, up: false);
        Assert.Equal(barrier.Strength - 10, pool.Left);
    }

    [Fact]
    public void A_barrier_blocks_from_every_side_and_never_parries()
    {
        var guard = new Guard();
        guard.Raise(now: 5f);

        foreach (float from in new[] { 0f, MathF.PI / 2f, MathF.PI, -MathF.PI / 2f })
        {
            Assert.Equal(GuardOutcome.Blocked, guard.Resolve(Weapons.Barrier, now: 5f, Vector2.Zero, facingYaw: 0f, Ground.Forward(from) * 3f));
        }
    }

    [Fact]
    public void A_spell_grows_with_wis_and_a_blow_with_str()
    {
        var wise = new CharacterStats(Str: 0, Wis: 10, Agi: 0);
        var strong = new CharacterStats(Str: 10, Wis: 0, Agi: 0);
        var bolt = Weapons.Staff(Element.Fire).Primary;
        var slice = Weapons.Greatsword.Primary;

        Assert.Equal((int)MathF.Round(bolt.Damage * (1f + 10 * StatRules.DamagePerWis)), StatRules.Damage(bolt, wise));
        Assert.Equal(bolt.Damage, StatRules.Damage(bolt, strong));
        Assert.Equal(StatRules.Damage(slice.Damage, strong), StatRules.Damage(slice, strong));
        Assert.Equal(slice.Damage, StatRules.Damage(slice, wise));
    }

    [Fact]
    public void An_improved_staff_casts_harder_and_is_still_its_element()
    {
        foreach (var staff in Weapons.Staffs)
        {
            var better = Weapons.AtLevel(staff, 3);

            Assert.Equal(staff.Element, better.Element);
            Assert.Same(staff.Guard, better.Guard);
            Assert.True(better.Primary.Damage > staff.Primary.Damage, staff.Id);
            Assert.True(better.Secondary.Damage > staff.Secondary.Damage, staff.Id);
            Assert.Equal(staff.Primary.Projectiles, better.Primary.Projectiles);
            Assert.Equal(staff.Secondary.Area, better.Secondary.Area);
        }
    }
}
