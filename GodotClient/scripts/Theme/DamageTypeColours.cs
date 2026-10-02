using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Theme;

// Each damage type's colours, Everdawn's: the one its damage numbers are written in (ElementPalettes'
// DamageNumberColor) and the one its name is written in on a skill (the text of BattlePresenter's
// EffectTypeBadgeColors). Two tables there too: a number has to be read over the field, a name over a dark panel.
public static class DamageTypeColours
{
    private static readonly Dictionary<DamageType, (Color Number, Color Name)> ByType = new()
    {
        [DamageType.Pierce] = (new Color(0.533f, 0.376f, 0.188f), new Color(0.753f, 0.518f, 0.290f)),
        [DamageType.Blunt] = (new Color(0.894f, 0.580f, 0.251f), new Color(0.894f, 0.580f, 0.251f)),
        [DamageType.Slash] = (new Color(0.878f, 0.471f, 0.533f), new Color(0.878f, 0.471f, 0.533f)),
        [DamageType.Fire] = (new Color(1.000f, 0.333f, 0.133f), new Color(1.000f, 0.400f, 0.200f)),
        [DamageType.Water] = (new Color(0.300f, 0.700f, 0.820f), new Color(0.400f, 0.760f, 0.860f)),
        [DamageType.Ice] = (new Color(0.533f, 0.867f, 1.000f), new Color(0.533f, 0.867f, 1.000f)),
        [DamageType.Wind] = (new Color(0.700f, 0.880f, 1.000f), new Color(0.700f, 0.880f, 1.000f)),
        [DamageType.Lightning] = (new Color(0.961f, 0.847f, 0.000f), new Color(0.961f, 0.847f, 0.000f)),
        [DamageType.Earth] = (new Color(0.784f, 0.573f, 0.165f), new Color(0.784f, 0.573f, 0.165f)),
        [DamageType.Divine] = (new Color(1.000f, 0.980f, 0.920f), new Color(1.000f, 0.973f, 0.878f)),
        [DamageType.Void] = (new Color(0.450f, 0.200f, 0.650f), new Color(0.667f, 0.400f, 0.867f)),
        [DamageType.Arcane] = (new Color(0.200f, 0.700f, 1.000f), new Color(0.200f, 0.700f, 1.000f)),
    };

    public static Color Number(DamageType type) => Of(type).Number;

    public static Color Name(DamageType type) => Of(type).Name;

    // A hit of more than one type is written in the mean of their colours, as in Everdawn.
    public static Color Number(int typesMask)
    {
        var colours = DamageTypes.InMask(typesMask).Select(Number).ToList();
        if (colours.Count == 0)
        {
            throw new ArgumentException("A hit of no damage type has no colour of its own", nameof(typesMask));
        }

        return new Color(colours.Average(c => c.R), colours.Average(c => c.G), colours.Average(c => c.B));
    }

    private static (Color Number, Color Name) Of(DamageType type) =>
        ByType.TryGetValue(type, out var colours) ? colours : throw new KeyNotFoundException($"No colours for the damage type {type}");
}
