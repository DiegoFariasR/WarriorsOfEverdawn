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
    public void Both_sets_holding_the_same_weapon_fails_loudly()
    {
        Assert.Throws<ArgumentException>(() => new WeaponSets(Weapons.Spear, Weapons.Spear));
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
                inHand.Add(sets.Active);
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

        Assert.Equal(WeaponSets.Default, WeaponSets.StartingWith(WeaponSets.Default.Active));
    }
}
