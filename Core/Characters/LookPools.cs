using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Characters;

namespace WarriorsOfEverdawn.Core.Characters;

// The pools this game draws random figures from (the kit's LookRandomizer), by the tags the kit gives parts and palettes.
public static class LookPools
{
    // The people of a town: humans in what one wears about a town, from a peasant's clothes to a noble's, with
    // nothing heavier than light armour, and no plate on their arms and legs.
    public static readonly LookPool Townsfolk = new("townsfolk", new[] { "human", "peasant", "noble", "robes", "light-armour" }) { Without = new[] { "full-armour" } };

    public static readonly LookPool Skeletons = new("skeletons", new[] { "skeleton" });

    public static readonly IReadOnlyList<LookPool> All = new[] { Townsfolk, Skeletons };

    public static LookPool ByName(string name) =>
        All.FirstOrDefault(pool => pool.Name == name) ?? throw new KeyNotFoundException($"No pool of looks '{name}' (there are: {string.Join(", ", All.Select(p => p.Name))})");
}
