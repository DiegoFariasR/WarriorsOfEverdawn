using System;
using System.Collections.Generic;
using WarriorsOfEverdawn.Core.Loot;

namespace WarriorsOfEverdawn.Core.Combat;

// The crypt under the enemy fortress: the dead that guard it, risen once as a session starts and never again, and
// what is left at its treasure when the last of them falls. They keep to the crypt (a skeleton only goes for a
// player on its own floor), and the waves above go on whether they stand or not.
public static class Crypt
{
    public static readonly IReadOnlyList<EnemyDefinition> Guards = new[]
    {
        Enemies.SkeletonWarrior, Enemies.SkeletonWarrior,
        Enemies.SkeletonMinion, Enemies.SkeletonMinion,
        Enemies.SkeletonArcher, Enemies.SkeletonArcher,
    };

    // More than a wave's worth: going down is the harder fight, and it is fought once.
    public static readonly Cost Treasure = new(Gold: 40, Orbs: 2);

    // The guard to stand on each of the crypt's guard spots, in turn: every guard stands, more than one to a spot
    // when there are fewer spots.
    public static EnemyDefinition GuardFor(int spot) =>
        spot >= 0 ? Guards[spot % Guards.Count] : throw new ArgumentOutOfRangeException(nameof(spot), spot, "A spot's number is not negative");

    // Whether the treasure is to be left: every guard that rose has fallen, and it has not been left already.
    public static bool Cleared(int risen, int standing, bool treasureLeft) => risen > 0 && standing == 0 && !treasureLeft;
}
