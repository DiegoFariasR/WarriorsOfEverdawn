using System;
using System.Linq;

namespace WarriorsOfEverdawn.Core.Combat;

public enum WeaponSlot
{
    Hand,
    Back,
}

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

    public WeaponDefinition? In(WeaponSlot slot) => slot == WeaponSlot.Hand ? Active : Stowed;

    // The same sets with that slot holding this weapon in place of whatever it held.
    public WeaponSets With(WeaponSlot slot, WeaponDefinition weapon) =>
        slot == WeaponSlot.Hand ? new WeaponSets(weapon, Stowed) : new WeaponSets(Active, weapon);

    public WeaponSets Swapped() => new(Stowed, Active);

    // Changes the weapon in hand to the next kind, skipping the kind on the back. An empty hand takes the first.
    public WeaponSets WithNextActive()
    {
        var next = Active == null ? Weapons.Default : Weapons.Next(Active);
        return new WeaponSets(next.Kind == Stowed?.Kind ? Weapons.Next(next) : next, Stowed);
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

    // A weapon bought goes to the hand if it is empty, else onto an empty back; with neither free it takes the place
    // of the weapon in hand, which is put down (PutDown) and not lost.
    public (WeaponSets Sets, WeaponDefinition? PutDown) WithBought(WeaponDefinition weapon) =>
        HasFreeSlot ? (WithPickedUp(weapon), null) : (new WeaponSets(weapon, Stowed), Active);

    // The skill of that id as these weapons have it, the hand's before the back's: an improved weapon's hits harder
    // than the plain skill the id names. The plain skill when neither weapon has it, as when a swing outlives the
    // change of weapon that followed it.
    public SkillDefinition SkillById(string id) =>
        new[] { Active, Stowed }.Where(w => w != null).SelectMany(w => w!.Skills).FirstOrDefault(s => s.Id == id)
        ?? Weapons.SkillById(id);
}
