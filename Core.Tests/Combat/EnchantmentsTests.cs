using System;
using System.Collections.Generic;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class EnchantmentsTests
{
    [Fact]
    public void Any_weapon_of_steel_or_wood_takes_any_element_and_is_still_itself()
    {
        foreach (var plain in Weapons.Arms)
        {
            foreach (var element in Elements.All)
            {
                var enchanted = Weapons.Enchanted(plain, element);

                Assert.Equal(element, enchanted.Enchantment);
                Assert.Null(enchanted.Element);
                Assert.Equal(plain.Id, enchanted.Kind);
                Assert.Same(plain, Weapons.Plain(enchanted));
                Assert.Same(plain.Guard, enchanted.Guard);
                Assert.Equal(0, enchanted.Level);
                Assert.Contains(element.ToString(), enchanted.Name);
                Assert.StartsWith(plain.Name, enchanted.Name);
            }
        }

        Assert.All(Weapons.All, w => Assert.Null(w.Enchantment));
    }

    [Fact]
    public void An_enchanted_weapons_blows_are_part_magic_of_its_element_and_otherwise_unchanged()
    {
        var plain = Weapons.Scythe;
        var enchanted = Weapons.Enchanted(plain, Element.Void);

        foreach (var (before, after) in plain.Skills.Zip(enchanted.Skills))
        {
            Assert.Equal(Element.Void, after.Element);
            Assert.Equal(Weapons.EnchantedShare, after.MagicShare);
            Assert.Equal(before, after with { Element = null, MagicShare = before.MagicShare });
        }

        Assert.All(plain.Skills, s => Assert.Null(s.Element));
        Assert.True(Weapons.EnchantedShare is > 0f and < 1f);
    }

    [Fact]
    public void The_magic_part_of_a_blow_grows_with_wis_and_the_rest_with_str()
    {
        const int Points = 10;
        var wise = new CharacterStats(Str: 0, Wis: Points, Agi: 0);
        var strong = new CharacterStats(Str: Points, Wis: 0, Agi: 0);
        var none = new CharacterStats(Str: 0, Wis: 0, Agi: 0);
        var slice = Weapons.Enchanted(Weapons.Greatsword, Element.Fire).Primary;
        float share = Weapons.EnchantedShare;

        Assert.Equal(slice.Damage, StatRules.Damage(slice, none));
        Assert.Equal((int)MathF.Round(slice.Damage * (1f + share * Points * StatRules.DamagePerWis)), StatRules.Damage(slice, wise));
        Assert.Equal((int)MathF.Round(slice.Damage * (1f + (1f - share) * Points * StatRules.DamagePerStr)), StatRules.Damage(slice, strong));
        Assert.True(StatRules.Damage(slice, strong) < StatRules.Damage(Weapons.Greatsword.Primary, strong));
        Assert.True(StatRules.Damage(slice, wise) > StatRules.Damage(Weapons.Greatsword.Primary, wise));
    }

    [Fact]
    public void An_enchantment_and_a_level_are_kept_through_each_other()
    {
        var levelled = Weapons.AtLevel(Weapons.Spear, 4);
        var both = Weapons.Enchanted(levelled, Element.Wind);

        Assert.Equal(4, both.Level);
        Assert.Equal(Element.Wind, both.Enchantment);
        Assert.Equal(levelled.Primary.Damage, both.Primary.Damage);
        Assert.Same(both, Weapons.AtLevel(Weapons.Enchanted(Weapons.Spear, Element.Wind), 4));

        var better = Weapons.AtLevel(both, 5);
        Assert.Equal(Element.Wind, better.Enchantment);
        Assert.Equal(Element.Wind, better.Primary.Element);
        Assert.True(better.Primary.Damage > both.Primary.Damage);

        var other = Weapons.Enchanted(both, Element.Earth);
        Assert.Equal(Element.Earth, other.Enchantment);
        Assert.Equal(4, other.Level);
        Assert.Same(Weapons.Spear, Weapons.AtLevel(Weapons.Plain(both), 0));
    }

    [Fact]
    public void Enchanted_weapons_are_named_and_found_by_id_at_every_level()
    {
        var plain = Weapons.SwordAndShield;
        var enchanted = Weapons.Enchanted(plain, Element.Divine);
        var levelled = Weapons.AtLevel(enchanted, 7);

        Assert.Equal($"{plain.Id}~divine", enchanted.Id);
        Assert.Equal($"{plain.Id}~divine+7", levelled.Id);
        Assert.Equal($"{plain.Name} of Divine", enchanted.Name);
        Assert.Equal($"{plain.Name} of Divine +7", levelled.Name);
        Assert.Same(enchanted, Weapons.ById(enchanted.Id));
        Assert.Same(levelled, Weapons.ById(levelled.Id));
        foreach (var weapon in Weapons.Arms)
        {
            foreach (var element in Elements.All)
            {
                var made = Weapons.AtLevel(Weapons.Enchanted(weapon, element), WeaponDefinition.MaxLevel);
                Assert.Same(made, Weapons.ById(made.Id));
            }
        }
    }

    [Fact]
    public void Ids_that_name_no_element_or_enchant_a_staff_fail_loudly()
    {
        string spear = Weapons.Spear.Id;
        string staff = Weapons.Staff(Element.Fire).Id;

        Assert.Throws<KeyNotFoundException>(() => Weapons.ById($"{spear}~"));
        Assert.Throws<KeyNotFoundException>(() => Weapons.ById($"{spear}~mud"));
        Assert.Throws<KeyNotFoundException>(() => Weapons.ById($"{spear}~Fire"));
        Assert.Throws<KeyNotFoundException>(() => Weapons.ById($"{spear}+2~fire"));
        Assert.Throws<KeyNotFoundException>(() => Weapons.ById($"{staff}~water"));
        Assert.Throws<ArgumentException>(() => Weapons.Enchanted(Weapons.Staff(Element.Fire), Element.Water));
    }

    [Fact]
    public void A_staff_is_attuned_to_another_element_at_the_level_it_had()
    {
        var staff = Weapons.AtLevel(Weapons.Staff(Element.Fire), 3);

        foreach (var element in Elements.All)
        {
            var attuned = Weapons.Attuned(staff, element);

            Assert.Equal(element, attuned.Element);
            Assert.Equal(3, attuned.Level);
            Assert.Same(Weapons.Staff(element), Weapons.Plain(attuned));
            Assert.Null(attuned.Enchantment);
        }

        Assert.Same(staff, Weapons.Attuned(staff, Element.Fire));
        Assert.Throws<ArgumentException>(() => Weapons.Attuned(Weapons.Spear, Element.Fire));
    }

    [Fact]
    public void A_player_carrying_an_enchanted_weapon_swings_its_enchanted_skills()
    {
        var enchanted = Weapons.Enchanted(Weapons.Greatsword, Element.Water);
        var sets = new WeaponSets(enchanted, Weapons.Spear);

        Assert.Equal(enchanted.Primary, sets.SkillById(Weapons.Greatsword.Primary.Id));
        Assert.Equal(Element.Water, sets.SkillById(Weapons.Greatsword.Lunge.Id).Element);
        Assert.Null(sets.SkillById(Weapons.Spear.Primary.Id).Element);
        Assert.Equal(Weapons.Next(Weapons.Greatsword), Weapons.Next(enchanted));
    }
}
