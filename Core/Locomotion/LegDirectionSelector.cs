using System;

namespace WarriorsOfEverdawn.Core.Locomotion;

public readonly record struct LegPose(LegDirection Direction, float BodyYawOffset);

// Yaw is counter-clockwise seen from above, so a positive angle from the aim is to the character's left.
public static class LegDirectionSelector
{
    public const float QuadrantHalfWidth = MathF.PI / 4f;

    // Leaving a quadrant takes this much past its edge, so moving along a boundary does not flicker between clips.
    public const float Hysteresis = 10f * Angles.DegToRad;

    public static LegPose Select(float moveYawFromAim, LegDirection current)
    {
        float angle = Angles.Wrap(moveYawFromAim);
        var direction = current;
        if (MathF.Abs(Angles.Wrap(angle - CentreOf(current))) > QuadrantHalfWidth + Hysteresis)
        {
            direction = Nearest(angle);
        }

        return new LegPose(direction, Angles.Wrap(angle - CentreOf(direction)));
    }

    public static float CentreOf(LegDirection direction) => direction switch
    {
        LegDirection.Forward => 0f,
        LegDirection.Left => MathF.PI / 2f,
        LegDirection.Backward => MathF.PI,
        LegDirection.Right => -MathF.PI / 2f,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    // The quadrant a direction falls in relative to the aim, with no hysteresis (a dash picks its clip once).
    public static LegDirection Nearest(float angle) => (int)MathF.Round(Angles.Wrap(angle) / (MathF.PI / 2f)) switch
    {
        0 => LegDirection.Forward,
        1 => LegDirection.Left,
        -1 => LegDirection.Right,
        _ => LegDirection.Backward,
    };
}
