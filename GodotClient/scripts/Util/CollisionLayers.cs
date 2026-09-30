namespace WarriorsOfEverdawn.Util;

// Players pass through each other; everything else collides.
public static class CollisionLayers
{
    public const uint World = 1;
    public const uint Players = 2;
    public const uint Enemies = 4;
}
