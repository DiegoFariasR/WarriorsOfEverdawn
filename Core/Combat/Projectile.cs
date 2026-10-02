using System;
using System.Numerics;

namespace WarriorsOfEverdawn.Core.Combat;

// Something a ranged attack looses: it flies straight across the ground at Speed until it hits a body or has gone
// MaxDistance.
public sealed record ProjectileDefinition(float Speed, float MaxDistance, float Radius);

public static class Projectiles
{
    // Aimed at where the target stands as it is loosed, with no lead, so a target that keeps moving sideways can
    // outrun it at long range.
    public static readonly ProjectileDefinition Arrow = new(Speed: 16f, MaxDistance: 14f, Radius: 0.15f);

    // First pass. A staff's bolt is one big slow ball; a volley is three small quick darts.
    public static readonly ProjectileDefinition Bolt = new(Speed: 16f, MaxDistance: 12f, Radius: 0.35f);

    public static readonly ProjectileDefinition Dart = new(Speed: 22f, MaxDistance: 12f, Radius: 0.2f);

    // What a wand's burst is thrown as: bigger and slower than a bolt, since it is what bursts.
    public static readonly ProjectileDefinition Ball = new(Speed: 13f, MaxDistance: 12f, Radius: 0.45f);

    // Whether the projectile touches a body while travelling from one point to the next this step. The whole
    // segment is tested, so a fast projectile cannot step over a body between frames.
    public static bool Hits(Vector2 from, Vector2 to, Vector2 target, float targetRadius, ProjectileDefinition projectile)
    {
        var step = to - from;
        float along = step.LengthSquared() > 0f ? Math.Clamp(Vector2.Dot(target - from, step) / step.LengthSquared(), 0f, 1f) : 0f;
        return Vector2.Distance(from + step * along, target) <= targetRadius + projectile.Radius;
    }

    // Whether a burst of this radius at a spot catches a body: one touching it is in it.
    public static bool Blasts(Vector2 at, float radius, Vector2 target, float targetRadius) =>
        Vector2.Distance(at, target) <= radius + targetRadius;
}
