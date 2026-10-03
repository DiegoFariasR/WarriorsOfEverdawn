using System.Linq;
using System.Numerics;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class SurfacesTests
{
    private const long Caster = 1;
    private const long Other = 2;

    private static readonly SurfaceEffect Fire = new(SurfaceKind.Burning, Radius: 2f, Surfaces.BurningLasts);
    private static readonly SurfaceEffect Ice = new(SurfaceKind.Icy, Radius: 2f, Surfaces.IcyLasts);

    [Fact]
    public void Fire_leaves_burning_ground_and_ice_leaves_ice_and_no_other_element_leaves_anything()
    {
        Assert.Equal(SurfaceKind.Burning, Surfaces.Of(Element.Fire, 1f)!.Kind);
        Assert.Equal(SurfaceKind.Icy, Surfaces.Of(Element.Ice, 1f)!.Kind);
        Assert.All(Elements.All.Where(e => e is not (Element.Fire or Element.Ice)), e => Assert.Null(Surfaces.Of(e, 1f)));
    }

    [Fact]
    public void The_fire_and_ice_staffs_balls_leave_a_patch_where_they_burst_and_their_wands_spells_one_under_their_area()
    {
        foreach (var element in new[] { Element.Fire, Element.Ice })
        {
            var burst = Weapons.Staff(element).Secondary;
            var held = Weapons.Wand(element).Secondary;

            Assert.Equal(Surfaces.BurstPatchRadius, burst.Surface!.Radius);
            Assert.True(burst.Surface.Radius <= burst.BlastRadius);
            Assert.Equal(held.Area!.Radius, held.Surface!.Radius);
            Assert.Equal(burst.Surface.Kind, held.Surface.Kind);
        }

        var others = Elements.All.Where(e => e is not (Element.Fire or Element.Ice));
        Assert.All(others, e => Assert.Null(Weapons.Staff(e).Secondary.Surface));
        Assert.All(others, e => Assert.Null(Weapons.Wand(e).Secondary.Surface));
        Assert.All(Weapons.Arms.SelectMany(w => w.Skills), s => Assert.Null(s.Surface));
        Assert.Equal(Weapons.Staff(Element.Fire).Secondary.Surface, Weapons.AtLevel(Weapons.Staff(Element.Fire), 3).Secondary.Surface);
    }

    [Fact]
    public void Burning_ground_burns_as_fire_and_grows_with_the_casters_wis()
    {
        var hit = Surfaces.BurningHit;

        Assert.Equal(new[] { DamageType.Fire }, hit.Types);
        Assert.True(StatRules.Damage(hit, new CharacterStats(Str: 0, Wis: 10, Agi: 0)) > StatRules.Damage(hit, new CharacterStats(Str: 0, Wis: 0, Agi: 0)));
        Assert.Equal(StatRules.Damage(hit, new CharacterStats(Str: 0, Wis: 5, Agi: 0)), StatRules.Damage(hit, new CharacterStats(Str: 20, Wis: 5, Agi: 0)));
    }

    [Fact]
    public void A_body_that_stays_on_ice_is_chilled_and_in_the_end_frozen()
    {
        var bars = new StatusBars();
        int turns = 0;
        while (!bars.Active.HasFlag(Statuses.Frozen) && turns < 20)
        {
            bars.Build(DamageType.Ice, Surfaces.IcyCold, damage: 0, Resistances.None);
            bars.Advance(StatusRules.Turn, hp: 100, Resistances.None);
            turns++;
            if (turns == StatusRules.ChilledAt / (Surfaces.IcyCold - StatusRules.ColdDecay))
            {
                Assert.True(bars.Active.HasFlag(Statuses.Chilled));
            }
        }

        Assert.True(bars.Lost);
        Assert.True(turns < 20);
    }

    [Fact]
    public void A_patch_touches_a_body_on_its_floor_within_its_radius_and_the_bodys()
    {
        var field = new SurfaceField();
        var patch = field.Lay(Caster, Fire, Vector2.Zero, level: 0, now: 0f).Patch;

        Assert.True(Surfaces.Touches(patch, new Vector2(2.4f, 0f), level: 0, bodyRadius: 0.5f));
        Assert.False(Surfaces.Touches(patch, new Vector2(2.4f, 0f), level: 0, bodyRadius: 0f));
        Assert.False(Surfaces.Touches(patch, Vector2.Zero, level: 1, bodyRadius: 0.5f));
        Assert.Same(patch, field.Under(SurfaceKind.Burning, Vector2.One, level: 0, bodyRadius: 0f));
        Assert.Null(field.Under(SurfaceKind.Icy, Vector2.One, level: 0, bodyRadius: 0f));
    }

    [Fact]
    public void A_patch_laid_near_one_of_the_same_kind_and_caster_renews_it_and_otherwise_lies_beside_it()
    {
        var field = new SurfaceField();
        var first = field.Lay(Caster, Fire, Vector2.Zero, level: 0, now: 0f).Patch;
        var near = new Vector2(Fire.Radius * Surfaces.RenewWithin, 0f);

        var renewed = field.Lay(Caster, Fire, near, level: 0, now: 1f);
        Assert.True(renewed.Renewed);
        Assert.Equal(first.Id, renewed.Patch.Id);
        Assert.Equal(1f + Fire.Lasts, renewed.Patch.Until);
        Assert.Single(field.Patches);

        Assert.False(field.Lay(Other, Fire, near, level: 0, now: 1f).Renewed);
        Assert.False(field.Lay(Caster, Fire, near * 1.1f, level: 0, now: 1f).Renewed);
        Assert.False(field.Lay(Caster, Fire, near, level: 1, now: 1f).Renewed);
        Assert.Equal(4, field.Patches.Count);
    }

    [Fact]
    public void A_caster_keeps_so_many_patches_and_the_next_takes_the_place_of_the_one_nearest_its_end()
    {
        var field = new SurfaceField();
        for (int i = 0; i < Surfaces.MostPerCaster; i++)
        {
            Assert.Empty(field.Lay(Caster, Fire, new Vector2(i * 10f, 0f), level: 0, now: i).Gone);
        }

        field.Lay(Other, Fire, new Vector2(-10f, 0f), level: 0, now: 0f);
        var next = field.Lay(Caster, Fire, new Vector2(100f, 0f), level: 0, now: Surfaces.MostPerCaster);

        Assert.Equal(Vector2.Zero, Assert.Single(next.Gone).Centre);
        Assert.Equal(Surfaces.MostPerCaster, field.Patches.Count(p => p.Caster == Caster));
        Assert.Single(field.Patches, p => p.Caster == Other);
    }

    // Whoever laid either: the newer stays, and the other kind it touches is gone.
    [Fact]
    public void Fire_laid_over_ice_melts_it_and_ice_laid_over_fire_puts_it_out()
    {
        var field = new SurfaceField();
        var ice = field.Lay(Caster, Ice, Vector2.Zero, level: 0, now: 0f).Patch;
        var farIce = field.Lay(Caster, Ice, new Vector2(10f, 0f), level: 0, now: 0f).Patch;
        var upstairs = field.Lay(Caster, Ice, Vector2.Zero, level: 1, now: 0f).Patch;
        var touching = new Vector2(Ice.Radius + Fire.Radius - 0.01f, 0f);

        var fire = field.Lay(Other, Fire, touching, level: 0, now: 1f);

        Assert.Equal(ice, Assert.Single(fire.Gone));
        Assert.Equal(new[] { farIce, upstairs, fire.Patch }, field.Patches);

        var putOut = field.Lay(Caster, Ice, touching, level: 0, now: 2f);

        Assert.Equal(fire.Patch, Assert.Single(putOut.Gone));
        Assert.Equal(new[] { farIce, upstairs, putOut.Patch }, field.Patches);
        Assert.Empty(field.Lay(Other, Fire, new Vector2(10f + Ice.Radius + Fire.Radius, 0f), level: 0, now: 3f).Gone);
    }

    // The town's camp fire: it burns for good, ice laid over it does not put it out, and it is nobody's.
    [Fact]
    public void A_hearths_fire_never_goes_out_and_no_spell_takes_it_off_the_ground()
    {
        var field = new SurfaceField();
        var hearth = field.Keep(SurfaceKind.Burning, Vector2.Zero, level: 0, Surfaces.HearthRadius);

        Assert.Equal(SurfaceField.Level, hearth.Caster);
        Assert.Empty(field.Lay(Caster, Ice, Vector2.Zero, level: 0, now: 0f).Gone);
        Assert.DoesNotContain(hearth, field.Expire(float.MaxValue));
        Assert.Same(hearth, field.Under(SurfaceKind.Burning, Vector2.Zero, level: 0, bodyRadius: 0f));
        for (int i = 0; i < Surfaces.MostPerCaster; i++)
        {
            Assert.DoesNotContain(hearth, field.Lay(Caster, Fire, new Vector2(10f + i * 10f, 0f), level: 0, now: i).Gone);
        }
    }

    [Fact]
    public void A_patch_is_gone_when_its_time_is_up()
    {
        var field = new SurfaceField();
        field.Lay(Caster, Fire, Vector2.Zero, level: 0, now: 0f);
        field.Lay(Caster, Ice, new Vector2(10f, 0f), level: 0, now: 0f);

        Assert.Empty(field.Expire(Fire.Lasts - 0.01f));
        Assert.Equal(SurfaceKind.Burning, Assert.Single(field.Expire(Fire.Lasts)).Kind);
        Assert.Equal(SurfaceKind.Icy, Assert.Single(field.Expire(Ice.Lasts)).Kind);
        Assert.Empty(field.Patches);
    }

    [Fact]
    public void On_ice_a_body_gains_and_loses_speed_no_faster_than_its_grip_and_gets_there_in_the_end()
    {
        const float Step = 0.1f;
        var going = Vector2.Zero;
        var wanted = new Vector2(3f, 0f);

        going = Surfaces.Slide(going, wanted, Step);
        Assert.Equal(Surfaces.IceGrip * Step, going.Length(), precision: 4);

        for (int i = 0; i < 100; i++)
        {
            going = Surfaces.Slide(going, wanted, Step);
        }

        Assert.Equal(wanted, going);

        // Stopping takes as long: it slides on.
        var stopping = Surfaces.Slide(wanted, Vector2.Zero, Step);
        Assert.Equal(wanted.Length() - Surfaces.IceGrip * Step, stopping.Length(), precision: 4);
    }
}
