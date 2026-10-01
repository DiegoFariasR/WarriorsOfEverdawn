using System;

namespace WarriorsOfEverdawn.Core.Combat;

// The two weapon slots a player carries: the hand, whose weapon puts its skills on the buttons, and the back. Either
// can be empty: a weapon dropped leaves its slot free until another is picked up.
public sealed record WeaponSets(WeaponDefinition? Active, WeaponDefinition? Stowed)
{
    public static WeaponSets Default { get; } = new(Weapons.Greatsword, Weapons.Spear);

    public bool HasFreeSlot => Active == null || Stowed == null;

    // The sets as a player starting with this weapon in hand carries them: the default back weapon, unless that is
    // the one in hand.
    public static WeaponSets StartingWith(WeaponDefinition active) =>
        new(active, active == Default.Stowed ? Default.Active : Default.Stowed);

    public WeaponSets Swapped() => new(Stowed, Active);

    // Changes the weapon in hand to the next one, skipping the one on the back. An empty hand takes the first.
    public WeaponSets WithNextActive()
    {
        var next = Active == null ? Weapons.Default : Weapons.Next(Active);
        return new WeaponSets(next == Stowed ? Weapons.Next(next) : next, Stowed);
    }

    // The hand lets go of its weapon; the back keeps its own.
    public WeaponSets WithHandEmptied() => new(null, Stowed);

    // A weapon taken up goes to the hand if it is empty, else onto the back.
    public WeaponSets WithPickedUp(WeaponDefinition weapon)
    {
        if (Active == null)
        {
            return new WeaponSets(weapon, Stowed);
        }

        return Stowed == null
            ? new WeaponSets(Active, weapon)
            : throw new InvalidOperationException($"No free slot for the {weapon.Name}: the {Active.Name} is in hand and the {Stowed.Name} on the back");
    }
}
