using System;

namespace WarriorsOfEverdawn.Core;

public static class Angles
{
    public const float DegToRad = MathF.PI / 180f;

    public static float Wrap(float radians)
    {
        float wrapped = MathF.IEEERemainder(radians, MathF.Tau);
        return wrapped <= -MathF.PI ? wrapped + MathF.Tau : wrapped;
    }

    // Turns the short way round, by at most maxStep, without overshooting.
    public static float RotateToward(float from, float to, float maxStep)
    {
        float difference = Wrap(to - from);
        return MathF.Abs(difference) <= maxStep ? Wrap(to) : Wrap(from + MathF.CopySign(maxStep, difference));
    }
}
