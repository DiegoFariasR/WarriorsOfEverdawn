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
    // A player starts with one weapon, in hand, and gold to buy more (Purse.StartingGold).
    public static WeaponSets Default { get; } = new(Weapons.Greatsword, null);

    public bool HasFreeSlot => Active == null || Stowed == null;

    // The sets as a player starting with this weapon in hand carries them: it alone, as the default.
    public static WeaponSets StartingWith(WeaponDefinition active) => new(active, null);

    public WeaponDefinition? In(WeaponSlot slot) => slot == WeaponSlot.Hand ? Active : Stowed;

    // The same sets with that slot holding this weapon in place of whatever it held.
    public WeaponSets With(WeaponSlot slot, WeaponDefinition weapon) =>
        slot == WeaponSlot.Hand ? new WeaponSets(weapon, Stowed) : new WeaponSets(Active, weapon);

    public WeaponSets Swapped() => new(Stowed, Active);

    // Changes the weapon in hand to the next kind, skipping the kind on the back. An empty hand stays empty: a
    // weapon out of nothing could be sold, and another had the same way.
    public WeaponSets WithNextActive()
    {
        if (Active == null)
        {
            return this;
        }

        var next = Weapons.Next(Active);
        return new WeaponSets(next.Kind == Stowed?.Kind ? Weapons.Next(next) : next, Stowed);
    }

    // That slot lets go of its weapon; the other keeps its own.
    public WeaponSets Without(WeaponSlot slot) => slot == WeaponSlot.Hand ? new WeaponSets(null, Stowed) : new WeaponSets(Active, null);

    public WeaponSets WithHandEmptied() => Without(WeaponSlot.Hand);

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
