using System;
using System.Linq;
using System.Numerics;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class WandsTests
{
    private static readonly AreaDefinition Ahead = new(Distance: 4f, Radius: 2f);

    [Fact]
    public void There_is_a_wand_for_every_element_beside_its_staff()
    {
        Assert.Equal(Elements.All, Weapons.Wands.Select(w => w.Element!.Value));
        foreach (var element in Elements.All)
        {
            var wand = Weapons.Wand(element);

            Assert.Equal(element, wand.Element);
            Assert.Same(wand, Weapons.ById(wand.Id));
            Assert.True(Weapons.IsWand(wand));
            Assert.True(Weapons.IsWand(Weapons.AtLevel(wand, 3)));
            Assert.False(Weapons.IsWand(Weapons.Staff(element)));
            Assert.Contains(wand, Weapons.All);
            Assert.NotEqual(Weapons.Staff(element).Id, wand.Id);
        }

        Assert.All(Weapons.Arms, w => Assert.False(Weapons.IsWand(w)));
    }

    [Fact]
    public void A_wand_throws_the_same_shot_and_raises_the_same_barrier_as_the_staff_of_its_element()
    {
        foreach (var element in Elements.All)
        {
            var wand = Weapons.Wand(element);
            var staff = Weapons.Staff(element);

            Assert.Same(staff.Primary, wand.Primary);
            Assert.Same(staff.Guard, wand.Guard);
            Assert.Same(Weapons.Barrier, wand.Guard);
        }
    }

    [Fact]
    public void Every_wand_holds_a_paid_spell_of_its_element_on_an_area()
    {
        foreach (var wand in Weapons.Wands)
        {
            var spell = wand.Secondary;

            Assert.True(spell.Channeled, wand.Id);
            Assert.NotNull(spell.Area);
            Assert.Null(spell.Projectile);
            Assert.Equal(wand.Element, spell.Element);
            Assert.True(spell.ManaCost > 0, wand.Id);
            Assert.True(spell.Area!.Radius > 0f && spell.Area.Distance >= 0f, wand.Id);
            Assert.Equal(spell.Area.Reach, spell.Range);
        }

        Assert.Equal(Weapons.Wands.Count, Weapons.Wands.Select(w => w.Secondary.Name).Distinct().Count());
        Assert.Equal(0f, Weapons.Wand(Element.Divine).Secondary.Area!.Distance);
    }

    [Fact]
    public void A_wands_lunge_is_a_jab_with_the_wand_and_reaches_less_than_any_blade_or_staff()
    {
        foreach (var wand in Weapons.Wands)
        {
            Assert.Null(wand.Lunge.Element);
            Assert.Null(wand.Lunge.Projectile);
            Assert.True(wand.Lunge.Range < Weapons.All.Where(w => !Weapons.IsWand(w) && w != Weapons.Bow).Min(w => w.Lunge.Range), wand.Id);
        }
    }

    [Fact]
    public void A_wand_is_attuned_to_a_wand_and_a_staff_to_a_staff()
    {
        var wand = Weapons.AtLevel(Weapons.Wand(Element.Fire), 2);
        var staff = Weapons.AtLevel(Weapons.Staff(Element.Fire), 2);

        foreach (var element in Elements.All)
        {
            Assert.Same(Weapons.AtLevel(Weapons.Wand(element), 2), Weapons.Attuned(wand, element));
            Assert.Same(Weapons.AtLevel(Weapons.Staff(element), 2), Weapons.Attuned(staff, element));
        }

        Assert.Throws<ArgumentException>(() => Weapons.Enchanted(Weapons.Wand(Element.Fire), Element.Water));
    }

    [Fact]
    public void An_improved_wand_holds_a_harder_spell_on_the_same_area()
    {
        var wand = Weapons.Wand(Element.Void);
        var better = Weapons.AtLevel(wand, 4);

        Assert.True(better.Secondary.Damage > wand.Secondary.Damage);
        Assert.Equal(wand.Secondary.Area, better.Secondary.Area);
        Assert.True(better.Primary.Damage > wand.Primary.Damage);
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
        var nova = Weapons.Wand(Element.Divine).Secondary;
        var behind = -Ground.Forward(0f) * (nova.Area!.Radius - 0.1f);

        Assert.True(SkillHits.Catches(Vector2.Zero, 0f, nova, behind, 0f));
        Assert.False(SkillHits.Catches(Vector2.Zero, 0f, nova, behind * 2f, 0f));
    }

}
