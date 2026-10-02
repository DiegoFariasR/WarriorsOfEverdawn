using System.Numerics;

namespace WarriorsOfEverdawn.Core.Combat;

// Where a spell lands when it lands on the ground and not in an arc from the caster: everything within Radius of the
// spot Distance ahead of where the caster faces. Distance 0 is a spell centred on the caster.
public sealed record AreaDefinition(float Distance, float Radius)
{
    // How far from the caster its far edge is.
    public float Reach => Distance + Radius;
}

public static class SkillArea
{
    public static Vector2 Centre(Vector2 caster, float aimYaw, AreaDefinition area) => caster + Ground.Forward(aimYaw) * area.Distance;

    // A body touching the area is in it.
    public static bool Covers(Vector2 caster, float aimYaw, AreaDefinition area, Vector2 target, float targetRadius) =>
        Vector2.Distance(Centre(caster, aimYaw, area), target) <= area.Radius + targetRadius;
}

public static class SkillHits
{
    // Whether a skill used by a caster facing aimYaw catches a body: by its area, when it has one, else by its arc.
    // A skill that looses projectiles catches nothing itself; its projectiles do.
    public static bool Catches(Vector2 caster, float aimYaw, SkillDefinition skill, Vector2 target, float targetRadius) =>
        skill.Projectile == null
        && (skill.Area is { } area
            ? SkillArea.Covers(caster, aimYaw, area, target, targetRadius)
            : MeleeArc.Hits(caster, aimYaw, skill, target, targetRadius));
}
