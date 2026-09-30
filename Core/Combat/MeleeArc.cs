using System;
using System.Numerics;

namespace WarriorsOfEverdawn.Core.Combat;

public static class MeleeArc
{
    // Range is measured to the target's body edge, not its centre.
    public static bool Hits(Vector2 attacker, float aimYaw, SkillDefinition skill, Vector2 target, float targetRadius)
    {
        var offset = target - attacker;
        float distance = offset.Length();
        if (distance <= targetRadius)
        {
            return true;
        }

        if (distance - targetRadius > skill.Range)
        {
            return false;
        }

        // A body at the edge of the arc is still caught, so its angular width widens the arc.
        float bodyAngle = MathF.Asin(targetRadius / distance);
        return MathF.Abs(Angles.Wrap(Ground.YawOf(offset) - aimYaw)) <= skill.HalfArc + bodyAngle;
    }
}
