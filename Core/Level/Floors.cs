using System;

namespace WarriorsOfEverdawn.Core.Level;

// The map's floors, one above the other: the ground is level 0, a storey above it level 1, a crypt under it level -1.
// A level is told from a height alone, so anything standing anywhere has one.
public static class Floors
{
    // One storey: the dungeon kit's walls are this high, and a floor above stands on them.
    public const float Storey = 4f;

    // A level begins this far below its own floor, so a body on the stairs belongs to the floor it is nearer, and
    // one just stepped off the top of a flight already to the floor it reached.
    private const float Below = Storey * 0.375f;

    // A piece may be set a little into its floor.
    private const float Sunk = 0.25f;

    public static int LevelOf(float height) => (int)MathF.Floor((height + Below) / Storey);

    // The floor a thing stands on or hangs above, by a height anywhere up it: a torch's flame high on a wall is on
    // the floor under it, where a body that high would be on the stairs to the next.
    public static int FloorUnder(float height) => (int)MathF.Floor((height + Sunk) / Storey);

    // Where a level's floor is.
    public static float HeightOf(int level) => level * Storey;

    // Two bodies are on one floor when they are less than half a storey apart: what one does reaches the other only
    // then. A ceiling or a floor lies between any two further apart.
    public static bool SameLevel(float height, float otherHeight) => MathF.Abs(height - otherHeight) < Storey / 2f;
}
