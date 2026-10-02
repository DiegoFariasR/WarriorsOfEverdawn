using System;
using System.Collections.Generic;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class WeaponSetsTests
{
    [Fact]
    public void Swapping_puts_the_back_weapon_in_hand_and_back_again()
    {
        var sets = new WeaponSets(Weapons.Quarterstaff, Weapons.Scythe);

        var swapped = sets.Swapped();

        Assert.Same(Weapons.Scythe, swapped.Active);
        Assert.Same(Weapons.Quarterstaff, swapped.Stowed);
        Assert.Equal(sets, swapped.Swapped());
    }

    [Fact]
    public void Dropping_empties_the_hand_and_leaves_the_back_alone()
    {
        var sets = new WeaponSets(Weapons.Quarterstaff, Weapons.Scythe).WithHandEmptied();

        Assert.Null(sets.Active);
        Assert.Same(Weapons.Scythe, sets.Stowed);
        Assert.True(sets.HasFreeSlot);
    }

    [Fact]
    public void A_weapon_picked_up_goes_to_the_hand_first_and_then_to_the_back()
    {
        var empty = new WeaponSets(null, null);

        var one = empty.WithPickedUp(Weapons.Spear);
        var two = one.WithPickedUp(Weapons.Scythe);

        Assert.Equal(new WeaponSets(Weapons.Spear, null), one);
        Assert.Equal(new WeaponSets(Weapons.Spear, Weapons.Scythe), two);
        Assert.Equal(new WeaponSets(Weapons.Scythe, Weapons.Spear), new WeaponSets(null, Weapons.Spear).WithPickedUp(Weapons.Scythe));
    }

    [Fact]
    public void Picking_up_with_both_slots_full_fails_loudly()
    {
        var full = new WeaponSets(Weapons.Greatsword, Weapons.Spear);

        Assert.False(full.HasFreeSlot);
        Assert.Throws<InvalidOperationException>(() => full.WithPickedUp(Weapons.Scythe));
    }

    [Fact]
    public void Swapping_works_with_an_empty_slot()
    {
        var sets = new WeaponSets(null, Weapons.Spear).Swapped();

        Assert.Equal(new WeaponSets(Weapons.Spear, null), sets);
    }

    [Fact]
    public void Changing_the_weapon_in_an_empty_hand_takes_one_the_back_does_not_hold()
    {
        foreach (var stowed in Weapons.All)
        {
            var sets = new WeaponSets(null, stowed).WithNextActive();

            Assert.NotNull(sets.Active);
            Assert.NotSame(stowed, sets.Active);
            Assert.Same(stowed, sets.Stowed);
        }
    }

    [Fact]
    public void Changing_the_weapon_in_hand_skips_the_one_on_the_back_and_leaves_the_back_alone()
    {
        foreach (var stowed in Weapons.All)
        {
            var sets = WeaponSets.StartingWith(stowed).Swapped();
            var inHand = new List<WeaponDefinition>();
            for (int i = 0; i < Weapons.All.Count - 1; i++)
            {
                sets = sets.WithNextActive();
                Assert.Same(stowed, sets.Stowed);
                inHand.Add(sets.Active!);
            }

            Assert.Equal(Weapons.All.Where(w => w != stowed).OrderBy(w => w.Id), inHand.OrderBy(w => w.Id));
        }
    }

    [Fact]
    public void Starting_with_any_weapon_carries_a_different_one_on_the_back()
    {
        foreach (var weapon in Weapons.All)
        {
            var sets = WeaponSets.StartingWith(weapon);

            Assert.Same(weapon, sets.Active);
            Assert.NotSame(weapon, sets.Stowed);
        }

        Assert.Equal(WeaponSets.Default, WeaponSets.StartingWith(WeaponSets.Default.Active!));
    }

    [Fact]
    public void A_weapon_bought_fills_the_hand_first_then_the_back_and_puts_nothing_down()
    {
        var emptyHanded = WeaponSets.Default.WithHandEmptied();
        var emptyBacked = new WeaponSets(WeaponSets.Default.Active, null);

        var (intoHand, fromHand) = emptyHanded.WithBought(Weapons.Scythe);
        var (ontoBack, fromBack) = emptyBacked.WithBought(Weapons.Scythe);

        Assert.Equal(new WeaponSets(Weapons.Scythe, WeaponSets.Default.Stowed), intoHand);
        Assert.Equal(new WeaponSets(WeaponSets.Default.Active, Weapons.Scythe), ontoBack);
        Assert.Null(fromHand);
        Assert.Null(fromBack);
    }

    [Fact]
    public void A_weapon_bought_with_both_slots_full_takes_the_hand_and_the_one_it_held_is_put_down()
    {
        var (sets, putDown) = WeaponSets.Default.WithBought(Weapons.Scythe);

        Assert.Equal(new WeaponSets(Weapons.Scythe, WeaponSets.Default.Stowed), sets);
        Assert.Same(WeaponSets.Default.Active, putDown);
    }

    [Fact]
    public void A_weapon_already_carried_can_be_bought_again()
    {
        var (sets, putDown) = WeaponSets.Default.WithBought(WeaponSets.Default.Stowed!);

        Assert.Same(WeaponSets.Default.Stowed, sets.Active);
        Assert.Same(WeaponSets.Default.Stowed, sets.Stowed);
        Assert.Same(WeaponSets.Default.Active, putDown);
    }

    [Fact]
    public void A_skill_is_what_the_weapon_carried_makes_of_it_the_hands_before_the_backs()
    {
        var better = Weapons.AtLevel(Weapons.Greatsword, 4);
        string slice = Weapons.Greatsword.Primary.Id;

        Assert.Equal(better.Primary, new WeaponSets(better, Weapons.Greatsword).SkillById(slice));
        Assert.Equal(Weapons.Greatsword.Primary, new WeaponSets(Weapons.Greatsword, better).SkillById(slice));
        Assert.Equal(better.Primary, new WeaponSets(Weapons.Spear, better).SkillById(slice));
        Assert.True(better.Primary.Damage > Weapons.Greatsword.Primary.Damage);
    }

    [Fact]
    public void A_skill_of_a_weapon_no_longer_carried_is_the_plain_one_and_an_unknown_one_fails_loudly()
    {
        var sets = new WeaponSets(Weapons.Spear, null);

        Assert.Equal(Weapons.Scythe.Primary, sets.SkillById(Weapons.Scythe.Primary.Id));
        Assert.Throws<KeyNotFoundException>(() => sets.SkillById("no-such-skill"));
    }

    [Fact]
    public void A_slot_is_read_and_filled_by_name()
    {
        var sets = WeaponSets.Default.With(WeaponSlot.Back, Weapons.Scythe);

        Assert.Same(WeaponSets.Default.Active, sets.In(WeaponSlot.Hand));
        Assert.Same(Weapons.Scythe, sets.In(WeaponSlot.Back));
        Assert.Equal(new WeaponSets(Weapons.Scythe, WeaponSets.Default.Stowed), WeaponSets.Default.With(WeaponSlot.Hand, Weapons.Scythe));
    }

    [Fact]
    public void The_weapon_key_skips_the_kind_on_the_back_whatever_its_level()
    {
        var back = Weapons.AtLevel(Weapons.Next(Weapons.Greatsword), 2);

        var next = new WeaponSets(Weapons.Greatsword, back).WithNextActive();

        Assert.NotEqual(back.Kind, next.Active!.Kind);
        Assert.Same(back, next.Stowed);
    }
}
