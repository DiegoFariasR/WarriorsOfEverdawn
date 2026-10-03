using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Theme;

// What each status is called over a body and on the player's frame, and the colour it is written in: that of the
// damage type that brings it on. The ones that stop a body come first.
public static class StatusLooks
{
    private static readonly (Statuses Status, string Name, DamageType Of)[] Told =
    {
        (Statuses.Frozen, "Frozen", DamageType.Ice),
        (Statuses.Stunned, "Stunned", DamageType.Blunt),
        (Statuses.Burning, "Burning", DamageType.Fire),
        (Statuses.Bleeding, "Bleeding", DamageType.Slash),
        (Statuses.Chilled, "Chilled", DamageType.Ice),
        (Statuses.Dizzy, "Dizzy", DamageType.Blunt),
        (Statuses.Exalted, "Exalted", DamageType.Divine),
        (Statuses.Blessed, "Blessed", DamageType.Divine),
        (Statuses.Illuminated, "Illuminated", DamageType.Divine),
        (Statuses.Forsaken, "Forsaken", DamageType.Void),
        (Statuses.Defiled, "Defiled", DamageType.Void),
        (Statuses.Tainted, "Tainted", DamageType.Void),
    };

    public static IEnumerable<(string Name, Color Colour)> Of(Statuses statuses) =>
        Told.Where(told => statuses.HasFlag(told.Status)).Select(told => (told.Name, DamageTypeColours.Name(told.Of)));
}
