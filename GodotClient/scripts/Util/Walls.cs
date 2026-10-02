using Godot;

namespace WarriorsOfEverdawn.Util;

// What stands in the way of things that fly and of magic: the walls and whatever else of the level is solid
// (CollisionLayers.World). Bodies are no part of it.
public static class Walls
{
    // Sight between two bodies is read this high over their feet: over the barrels, benches and trunks a spell is
    // cast across, and under the top of every wall.
    public const float SightHeight = 1.6f;

    // Where a straight line from one point to the other first runs into the world; null with a clear way. A line
    // that starts inside a solid does not meet it: start it outside.
    public static Vector3? Hit(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 to)
    {
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to, CollisionLayers.World));
        return hit.Count > 0 ? hit["position"].AsVector3() : null;
    }

    // Whether the world stands between two bodies, by where their feet are.
    public static bool Between(PhysicsDirectSpaceState3D space, Vector3 feet, Vector3 otherFeet) =>
        Hit(space, feet + Vector3.Up * SightHeight, otherFeet + Vector3.Up * SightHeight) != null;
}
