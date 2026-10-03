using Godot;
using WarriorsOfEverdawn.Core;
using WarriorsOfEverdawn.Core.Level;

namespace WarriorsOfEverdawn.Util;

// Converts between Godot vectors and Core's ground plane (see Core.Ground for the yaw convention).
public static class Yaw
{
    public static System.Numerics.Vector2 ToGround(Vector3 position) => new(position.X, position.Z);

    // What of the vector lies along the ground: its height dropped.
    public static Vector3 Flat(Vector3 v) => new(v.X, 0f, v.Z);

    public static float Of(Vector3 direction) => Ground.YawOf(ToGround(direction));

    // How far apart two places are across the ground, for what reaches only along one floor: from another floor,
    // further than any reach.
    public static float AcrossFloor(Vector3 from, Vector3 to) =>
        Floors.SameLevel(from.Y, to.Y) ? ToGround(to - from).Length() : float.PositiveInfinity;

    public static Vector3 Forward(float yaw)
    {
        var forward = Ground.Forward(yaw);
        return new Vector3(forward.X, 0f, forward.Y);
    }

    public static Vector3 FromFacing(float forward, float right, float yaw)
    {
        var move = Ground.FromFacing(forward, right, yaw);
        return new Vector3(move.X, 0f, move.Y);
    }

    public static float Approach(float from, float to, float rate, float delta) =>
        Angles.Wrap(from + Angles.Wrap(to - from) * Easing.Share(rate, delta));
}
