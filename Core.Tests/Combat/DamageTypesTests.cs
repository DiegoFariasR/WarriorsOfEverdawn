using System;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class DamageTypesTests
{
    [Fact]
    public void There_are_Everdawns_twelve_types_in_its_three_families()
    {
        Assert.Equal(12, DamageTypes.All.Count);
        Assert.Equal(new[] { DamageType.Pierce, DamageType.Blunt, DamageType.Slash }, DamageTypes.All.Where(t => DamageTypes.FamilyOf(t) == DamageFamily.Physical));
        Assert.Equal(new[] { DamageType.Divine, DamageType.Void, DamageType.Arcane }, DamageTypes.All.Where(t => DamageTypes.FamilyOf(t) == DamageFamily.Astral));
        Assert.Equal(DamageTypes.All.Count - 6, DamageTypes.All.Count(t => DamageTypes.FamilyOf(t) == DamageFamily.Elemental));
    }

    [Fact]
    public void Ice_lightning_and_arcane_are_resisted_through_other_types()
    {
        var through = new[] { DamageType.Ice, DamageType.Lightning, DamageType.Arcane };

        Assert.Equal(DamageTypes.All.Except(through), DamageTypes.Resisted);
        Assert.All(Enum.GetValues<DamageFamily>(), family =>
            Assert.Equal(DamageTypes.Resisted.Where(t => DamageTypes.FamilyOf(t) == family), DamageTypes.ResistedIn(family)));
    }

    [Fact]
    public void Every_element_has_a_type_of_its_own_and_ice_and_lightning_are_resisted_as_water_and_wind()
    {
        var types = Elements.All.Select(DamageTypes.Of).ToList();
        var warded = Resistances.None.With(DamageType.Water, Resistances.Most).With(DamageType.Wind, Resistances.Most);

        Assert.Equal(Elements.All.Count, types.Distinct().Count());
        Assert.Equal(new[] { DamageType.Ice, DamageType.Lightning }, types.Except(DamageTypes.Resisted));
        Assert.All(new[] { Element.Water, Element.Ice, Element.Wind, Element.Lightning }, element =>
            Assert.Equal(Resistances.Most, warded.Against(DamageTypes.Of(element))));
        Assert.All(Elements.All, element => Assert.Equal(Elements.IdOf(element), DamageTypes.NameOf(DamageTypes.Of(element))));
        Assert.All(Elements.All, element =>
            Assert.Equal(Elements.IsAstral(element), DamageTypes.FamilyOf(DamageTypes.Of(element)) == DamageFamily.Astral));
    }

    [Fact]
    public void A_hits_types_travel_as_one_number_and_come_back_the_same()
    {
        Assert.Equal(DamageTypes.NoTypes, DamageTypes.MaskOf(Array.Empty<DamageType>()));
        Assert.Empty(DamageTypes.InMask(DamageTypes.NoTypes));
        Assert.All(DamageTypes.All, type => Assert.Equal(new[] { type }, DamageTypes.InMask(DamageTypes.MaskOf(new[] { type }))));
        Assert.Equal(new[] { DamageType.Slash, DamageType.Void }, DamageTypes.InMask(DamageTypes.MaskOf(new[] { DamageType.Void, DamageType.Slash })));
        Assert.Equal(DamageTypes.All, DamageTypes.InMask(DamageTypes.MaskOf(DamageTypes.All)));
    }

    [Fact]
    public void Every_blow_with_a_weapon_of_steel_or_wood_is_of_the_weapons_one_physical_type()
    {
        foreach (var weapon in Weapons.Arms)
        {
            var type = weapon.Primary.Type;

            Assert.Equal(DamageFamily.Physical, DamageTypes.FamilyOf(type));
            Assert.All(weapon.Skills, skill => Assert.Equal(new[] { (type, 1f) }, skill.Parts));
        }

        Assert.Equal(Enum.GetValues<DamageType>().Where(t => DamageTypes.FamilyOf(t) == DamageFamily.Physical).OrderBy(t => t),
            Weapons.Arms.Select(w => w.Primary.Type).Distinct().OrderBy(t => t));
    }

    [Fact]
    public void A_staffs_and_a_wands_magic_is_all_of_its_elements_type_and_its_poke_is_a_blunt_blow()
    {
        foreach (var magic in Weapons.Staffs.Concat(Weapons.Wands))
        {
            var own = DamageTypes.Of(magic.Element!.Value);

            Assert.NotEqual(DamageFamily.Physical, DamageTypes.FamilyOf(own));
            Assert.All(new[] { magic.Primary, magic.Secondary }, spell => Assert.Equal(new[] { (own, 1f) }, spell.Parts));
            Assert.Equal(new[] { (DamageType.Blunt, 1f) }, magic.Lunge.Parts);
        }
    }

    [Fact]
    public void Everdawns_signature_spells_are_held_by_the_wands_of_their_own_elements()
    {
        Assert.Equal("ice-blizzard", Weapons.Wand(Element.Ice).Secondary.Id);
        Assert.Equal("lightning-storm", Weapons.Wand(Element.Lightning).Secondary.Id);
        Assert.Equal(DamageType.Ice, Weapons.Wand(Element.Ice).Secondary.Type);
        Assert.Equal(DamageType.Lightning, Weapons.Wand(Element.Lightning).Secondary.Type);
    }

    [Fact]
    public void A_wand_of_a_natural_element_and_the_wand_of_its_weaponised_form_hold_different_spells()
    {
        foreach (var (natural, weaponised) in new[] { (Element.Water, Element.Ice), (Element.Wind, Element.Lightning) })
        {
            var mild = Weapons.Wand(natural).Secondary;
            var sharp = Weapons.Wand(weaponised).Secondary;

            Assert.NotEqual(mild.Name, sharp.Name);
            Assert.True(mild.ManaCost < sharp.ManaCost, $"{mild.Name} costs {mild.ManaCost}, {sharp.Name} {sharp.ManaCost}");
            Assert.NotEqual((mild.Damage, mild.Area!.Radius), (sharp.Damage, sharp.Area!.Radius));
        }
    }

    [Fact]
    public void An_enchanted_blow_is_part_its_weapons_type_and_part_its_elements()
    {
        foreach (var plain in Weapons.Arms)
        {
            foreach (var element in Elements.All)
            {
                var enchanted = Weapons.Enchanted(plain, element);

                Assert.All(enchanted.Skills, skill =>
                {
                    Assert.Equal(new[] { (plain.Primary.Type, 1f - Weapons.EnchantedShare), (DamageTypes.Of(element), Weapons.EnchantedShare) }, skill.Parts);
                    Assert.Equal(new[] { plain.Primary.Type, DamageTypes.Of(element) }, skill.Types);
                });
            }
        }
    }

    [Fact]
    public void Every_monsters_attack_is_of_a_physical_type()
    {
        Assert.All(Enemies.All, enemy =>
        {
            var (type, share) = Assert.Single(enemy.Attack.Parts);
            Assert.Equal(1f, share);
            Assert.Equal(DamageFamily.Physical, DamageTypes.FamilyOf(type));
        });
    }
}
