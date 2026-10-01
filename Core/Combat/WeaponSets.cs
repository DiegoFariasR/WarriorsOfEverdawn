using System;

namespace WarriorsOfEverdawn.Core.Combat;

// The two weapon sets a player carries: the one in hand, whose skills are on the buttons, and the one on the back.
public sealed record WeaponSets
{
    public WeaponSets(WeaponDefinition active, WeaponDefinition stowed)
    {
        if (active == stowed)
        {
            throw new ArgumentException($"Both weapon sets hold the {active.Name}; they must differ");
        }

        Active = active;
        Stowed = stowed;
    }

    public static WeaponSets Default { get; } = new(Weapons.Greatsword, Weapons.Spear);

    public WeaponDefinition Active { get; }

    public WeaponDefinition Stowed { get; }

    // The sets as a player starting with this weapon in hand carries them: the default back weapon, unless that is
    // the one in hand.
    public static WeaponSets StartingWith(WeaponDefinition active) =>
        new(active, active == Default.Stowed ? Default.Active : Default.Stowed);

    public WeaponSets Swapped() => new(Stowed, Active);

    // Changes the weapon in hand to the next one, skipping the one on the back.
    public WeaponSets WithNextActive()
    {
        var next = Weapons.Next(Active);
        return new WeaponSets(next == Stowed ? Weapons.Next(next) : next, Stowed);
    }
}
