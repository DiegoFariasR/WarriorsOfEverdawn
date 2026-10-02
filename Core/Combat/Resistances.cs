using System;
using System.Collections.Generic;
using System.Linq;

namespace WarriorsOfEverdawn.Core.Combat;

// What a body makes of each type of damage, as Everdawn's resistances are: a percentage taken off what reaches it,
// or added to it when below zero (a weakness). 25 stops a quarter of a hit; -25 lets a quarter more through.
public sealed class Resistances
{
    // Everdawn's BattleConstants.ResistanceCap: the most resistant body still takes a tenth.
    public const int Most = 90;

    private readonly IReadOnlyDictionary<DamageType, int> _percent;

    private Resistances(IReadOnlyDictionary<DamageType, int> percent) => _percent = percent;

    public static Resistances None { get; } = new(new Dictionary<DamageType, int>());

    // The types this resists or is weak to, each with its percentage.
    public IEnumerable<(DamageType Type, int Percent)> Named => _percent.Where(p => p.Value != 0).Select(p => (p.Key, p.Value));

    public Resistances With(DamageType type, int percent) =>
        DamageTypes.Resisted.Contains(type)
            ? new Resistances(new Dictionary<DamageType, int>(_percent) { [type] = percent })
            : throw new ArgumentException($"{type} is resisted through other types, not of its own (DamageTypes.Resisted)", nameof(type));

    // The resistance to this type moved by so much: what a status adds to or takes from a body's own.
    public Resistances Raised(DamageType type, int by) => With(type, Own(type) + by);

    // Every type of the family at once: "weak to all that is physical".
    public Resistances With(DamageFamily family, int percent) =>
        DamageTypes.ResistedIn(family).Aggregate(this, (resistances, type) => resistances.With(type, percent));

    public int Against(DamageType type) => type switch
    {
        DamageType.Ice => Own(DamageType.Water),
        DamageType.Lightning => Own(DamageType.Wind),
        DamageType.Arcane => Math.Min(Own(DamageType.Divine), Own(DamageType.Void)),
        _ => Own(type),
    };

    // The share of a hit of this type that gets through: over 1 against a weakness. A point finds the gaps:
    // what resists piercing resists it half as well as it says (Everdawn's armour-cracker), and a weakness to it is
    // no smaller for that.
    public float Taken(DamageType type)
    {
        int percent = Against(type);
        return 1f - Math.Min(Most, type == DamageType.Pierce && percent > 0 ? percent / 2 : percent) / 100f;
    }

    private int Own(DamageType type) => _percent.GetValueOrDefault(type);
}
