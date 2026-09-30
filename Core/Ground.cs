using System;
using System.Numerics;

namespace WarriorsOfEverdawn.Core;

// Ground-plane positions: X is world X, Y is world Z. Yaw 0 faces -Z and grows counter-clockwise seen from above,
// which is Godot's convention; the client converts at its edge.
public static class Ground
{
    public static float YawOf(Vector2 direction) => MathF.Atan2(-direction.X, -direction.Y);

    public static Vector2 Forward(float yaw) => new(-MathF.Sin(yaw), -MathF.Cos(yaw));

    // Movement input read relative to a facing (forward = along it, right = its right-hand side).
    public static Vector2 FromFacing(float forward, float right, float yaw) =>
        Forward(yaw) * forward + Forward(yaw - MathF.PI / 2f) * right;
}
