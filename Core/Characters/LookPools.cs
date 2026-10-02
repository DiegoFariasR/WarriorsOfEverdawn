using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Characters;

namespace WarriorsOfEverdawn.Core.Characters;

// The pools this game draws random figures from (the kit's LookRandomizer).
public static class LookPools
{
    // The people of a town: the adventurers and the farmers, in the colours the adventurers come in.
    public static readonly LookPool Townsfolk = new(
        "townsfolk",
        new[] { "Knight", "Barbarian", "Mage", "Rogue", "RogueHooded", "Ranger", "Druid", "Engineer", "Cleric", "Lorekeeper", "Farmer", "Farmer_A", "Farmer_B" },
        new[]
        {
            "knight_texture", "knight_texture_alt_A", "knight_texture_alt_B", "knight_texture_alt_C",
            "barbarian_texture", "barbarian_texture_alt_A", "barbarian_texture_alt_B", "barbarian_texture_alt_C",
            "mage_texture", "mage_texture_alt_A", "mage_texture_alt_B", "mage_texture_alt_C",
            "rogue_texture", "rogue_texture_alt_A", "rogue_texture_alt_B", "rogue_texture_alt_C",
            "ranger_texture", "ranger_texture_alt_A", "ranger_texture_alt_B", "ranger_texture_alt_C",
            "druid_texture", "druid_texture_alt_A", "druid_texture_alt_B", "druid_texture_alt_C",
            "engineer_texture", "engineer_texture_alt_A", "engineer_texture_alt_B", "engineer_texture_alt_C",
        });

    public static readonly LookPool Skeletons = new(
        "skeletons",
        new[] { "SkeletonMinion", "SkeletonWarrior", "SkeletonRogue", "SkeletonMage" },
        new[] { "skeleton_texture_A", "skeleton_texture_B" });

    public static readonly IReadOnlyList<LookPool> All = new[] { Townsfolk, Skeletons };

    public static LookPool ByName(string name) =>
        All.FirstOrDefault(pool => pool.Name == name) ?? throw new KeyNotFoundException($"No pool of looks '{name}' (there are: {string.Join(", ", All.Select(p => p.Name))})");
}
