namespace WarriorsOfEverdawn.Core.Combat;

// A hit staggers for Duration; after that the target cannot be staggered again for Immunity, so fast repeated hits
// (Spin's revolutions) cannot keep it from ever attacking.
public sealed class Stagger
{
    public const float Duration = 0.3f;
    public const float Immunity = 1f;

    private float _until = float.NegativeInfinity;
    private float _immuneUntil = float.NegativeInfinity;

    public bool IsStaggered(float now) => now < _until;

    // A stagger that ignores immunity, such as a parry's. It never shortens one already running.
    public void Force(float now, float duration)
    {
        _until = System.MathF.Max(_until, now + duration);
        _immuneUntil = _until + Immunity;
    }

    public bool TryApply(float now)
    {
        if (now < _immuneUntil)
        {
            return false;
        }

        _until = now + Duration;
        _immuneUntil = _until + Immunity;
        return true;
    }
}
