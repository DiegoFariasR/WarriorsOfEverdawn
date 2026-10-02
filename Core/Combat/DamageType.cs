using System;
using System.Collections.Generic;
using System.Linq;

namespace WarriorsOfEverdawn.Core.Combat;

// Everdawn's twelve damage types, in its order (its EffectType; Docs/Design/damage-types.md there). What each does
// there beyond damage (bleed, burn, stun, cold and the rest) is not here yet: so far a type decides the colour a hit
// is shown in and what a body's resistances make of it.
public enum DamageType
{
    Pierce,
    Blunt,
    Slash,
    Fire,
    Water,
    Ice,
    Wind,
    Lightning,
    Earth,
    Divine,
    Void,
    Arcane,
}

public enum DamageFamily
{
    Physical,
    Elemental,
    Astral,
}

public static class DamageTypes
{
    public static IReadOnlyList<DamageType> All { get; } = Enum.GetValues<DamageType>();

    // The types a body resists of its own, Everdawn's resistance buckets. The other three are resisted through
    // these: ice as water and lightning as wind (each the weaponised form of its element), and arcane as whichever of
    // divine and void the body resists less.
    public static IReadOnlyList<DamageType> Resisted { get; } = new[]
    {
        DamageType.Pierce, DamageType.Blunt, DamageType.Slash,
        DamageType.Fire, DamageType.Water, DamageType.Wind, DamageType.Earth,
        DamageType.Divine, DamageType.Void,
    };

    public static DamageFamily FamilyOf(DamageType type) => type switch
    {
        DamageType.Pierce or DamageType.Blunt or DamageType.Slash => DamageFamily.Physical,
        DamageType.Fire or DamageType.Water or DamageType.Ice or DamageType.Wind or DamageType.Lightning or DamageType.Earth => DamageFamily.Elemental,
        DamageType.Divine or DamageType.Void or DamageType.Arcane => DamageFamily.Astral,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "A damage type of no family"),
    };

    public static IEnumerable<DamageType> ResistedIn(DamageFamily family) => Resisted.Where(type => FamilyOf(type) == family);

    // The type an element's magic is of.
    public static DamageType Of(Element element) => element switch
    {
        Element.Fire => DamageType.Fire,
        Element.Water => DamageType.Water,
        Element.Ice => DamageType.Ice,
        Element.Wind => DamageType.Wind,
        Element.Lightning => DamageType.Lightning,
        Element.Earth => DamageType.Earth,
        Element.Divine => DamageType.Divine,
        Element.Void => DamageType.Void,
        _ => throw new ArgumentOutOfRangeException(nameof(element), element, "An element of no damage type"),
    };

    // Lower case, as it is written beside a skill: "slash".
    public static string NameOf(DamageType type) => type.ToString().ToLowerInvariant();

    // The mask of a hit of no type: a fall, a blow dealt by the game itself.
    public const int NoTypes = 0;

    // A hit's types as one number, a bit to a type: how they travel between machines.
    public static int MaskOf(IEnumerable<DamageType> types) => types.Aggregate(0, (mask, type) => mask | (1 << (int)type));

    public static IEnumerable<DamageType> InMask(int mask) => All.Where(type => (mask & (1 << (int)type)) != 0);
}
