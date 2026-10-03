namespace WarriorsOfEverdawn.Util;

// Players pass through each other; everything else collides.
public static class CollisionLayers
{
    public const uint World = 1;
    public const uint Players = 2;
    public const uint Enemies = 4;

    // Only skeletons run into it: the barrier across the allied town's gate.
    public const uint Ward = 8;

    // What is struck as a body and is no fighter: the training dummies and the crates and barrels that break (IStruck).
    // Bodies run into them and the ways go round them, but missiles and sight pass them by, so a bolt meets one as a
    // target and not as a wall.
    public const uint Struck = 16;

    // What of the level a body runs into, and the ways are worked out round.
    public const uint Solid = World | Struck;
}
