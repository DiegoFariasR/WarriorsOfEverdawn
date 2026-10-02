using System;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class ResistancesTests
{
    private const int Some = 40;

    private static readonly CharacterStats Hands = new(Str: 12, Wis: 9, Agi: 0);

    [Fact]
    public void A_body_with_no_resistances_takes_every_type_whole()
    {
        Assert.All(DamageTypes.All, type =>
        {
            Assert.Equal(0, Resistances.None.Against(type));
            Assert.Equal(1f, Resistances.None.Taken(type));
        });
        Assert.Empty(Resistances.None.Named);
    }

    [Fact]
    public void A_resistance_stops_its_percentage_and_a_weakness_lets_as_much_more_through()
    {
        var body = Resistances.None.With(DamageType.Fire, Some).With(DamageType.Slash, -Some);

        Assert.Equal(1f - Some / 100f, body.Taken(DamageType.Fire), precision: 5);
        Assert.Equal(1f + Some / 100f, body.Taken(DamageType.Slash), precision: 5);
        Assert.Equal(1f, body.Taken(DamageType.Earth));
        Assert.Equal(new[] { (DamageType.Slash, -Some), (DamageType.Fire, Some) }, body.Named.OrderBy(n => n.Type));
        Assert.Empty(Resistances.None.Named);
    }

    [Fact]
    public void The_most_resistant_body_still_takes_a_share()
    {
        var body = Resistances.None.With(DamageType.Void, Resistances.Most + 50);

        Assert.Equal(1f - Resistances.Most / 100f, body.Taken(DamageType.Void), precision: 5);
    }

    [Fact]
    public void Ice_is_resisted_as_water_lightning_as_wind_and_arcane_as_the_weaker_of_divine_and_void()
    {
        var body = Resistances.None
            .With(DamageType.Water, Some)
            .With(DamageType.Wind, -Some)
            .With(DamageType.Divine, Some)
            .With(DamageType.Void, Some / 2);

        Assert.Equal(body.Against(DamageType.Water), body.Against(DamageType.Ice));
        Assert.Equal(body.Against(DamageType.Wind), body.Against(DamageType.Lightning));
        Assert.Equal(Math.Min(body.Against(DamageType.Divine), body.Against(DamageType.Void)), body.Against(DamageType.Arcane));
        Assert.All(new[] { DamageType.Ice, DamageType.Lightning, DamageType.Arcane }, type =>
            Assert.Throws<ArgumentException>(() => Resistances.None.With(type, Some)));
    }

    [Fact]
    public void A_whole_family_is_resisted_at_once()
    {
        var body = Resistances.None.With(DamageFamily.Physical, Some);

        Assert.All(DamageTypes.All, type =>
            Assert.Equal(DamageTypes.FamilyOf(type) == DamageFamily.Physical ? Some : 0, body.Against(type)));
    }

    [Fact]
    public void A_hit_on_a_body_with_no_resistances_is_what_the_skill_deals()
    {
        foreach (var weapon in Weapons.All.Concat(Weapons.Arms.Select(w => Weapons.Enchanted(w, Element.Earth))))
        {
            Assert.All(weapon.Skills, skill => Assert.Equal(StatRules.Damage(skill, Hands), StatRules.Damage(skill, Hands, Resistances.None)));
        }
    }

    [Fact]
    public void Each_part_of_an_enchanted_blow_is_taken_by_what_the_body_makes_of_its_type()
    {
        var slice = Weapons.Enchanted(Weapons.Greatsword, Element.Fire).Primary;
        float share = Weapons.EnchantedShare;
        float steel = (1f - share) * (1f + Hands.Str * StatRules.DamagePerStr);
        float fire = share * (1f + Hands.Wis * StatRules.DamagePerWis);
        var fireproof = Resistances.None.With(DamageType.Fire, Some);
        var brittle = Resistances.None.With(DamageType.Slash, -Some);

        Assert.Equal((int)MathF.Round(slice.Damage * (steel + fire * (1f - Some / 100f))), StatRules.Damage(slice, Hands, fireproof));
        Assert.Equal((int)MathF.Round(slice.Damage * (steel * (1f + Some / 100f) + fire)), StatRules.Damage(slice, Hands, brittle));
        Assert.True(StatRules.Damage(slice, Hands, fireproof) < StatRules.Damage(slice, Hands));
        Assert.True(StatRules.Damage(slice, Hands, brittle) > StatRules.Damage(slice, Hands));
    }

    [Fact]
    public void Skeletons_are_weak_to_what_is_physical_and_to_fire_and_the_divine()
    {
        var weakTo = DamageTypes.ResistedIn(DamageFamily.Physical).Concat(new[] { DamageType.Fire, DamageType.Divine }).ToList();

        Assert.True(Enemies.SkeletonWeakness < 0);
        Assert.All(Enemies.All, skeleton => Assert.All(DamageTypes.Resisted, type =>
            Assert.Equal(weakTo.Contains(type) ? Enemies.SkeletonWeakness : 0, skeleton.Resistances.Against(type))));
    }

    [Fact]
    public void A_blade_and_a_fire_bolt_do_more_to_a_skeleton_and_a_water_volley_no_more()
    {
        var bones = Enemies.SkeletonWarrior.Resistances;
        float weak = 1f - Enemies.SkeletonWeakness / 100f;
        var slice = Weapons.Greatsword.Primary;
        var bolt = Weapons.Staff(Element.Fire).Primary;
        var volley = Weapons.Staff(Element.Water).Primary;

        Assert.Equal((int)MathF.Round(slice.Damage * (1f + Hands.Str * StatRules.DamagePerStr) * weak), StatRules.Damage(slice, Hands, bones));
        Assert.Equal((int)MathF.Round(bolt.Damage * (1f + Hands.Wis * StatRules.DamagePerWis) * weak), StatRules.Damage(bolt, Hands, bones));
        Assert.Equal(StatRules.Damage(volley, Hands), StatRules.Damage(volley, Hands, bones));
    }
}
