using System;
using System.Numerics;

namespace WarriorsOfEverdawn.Core.Locomotion;

// A short dash of fixed length, like PoE2's dash roll. No cost; charges come back one at a time.
public static class DashRules
{
    public const float Distance = 3.5f;
    public const float Duration = 0.3f;
    public const int Charges = 2;
    public const float RechargeTime = 2f;

    // The attack button turns a dash into a lunge up to this long after the dash starts.
    public const float LungeWithin = 0.1f;

    public static float Speed => Distance / Duration;

    // Movement input picks the direction; with none, the dash goes where the character faces.
    public static Vector2 Direction(Vector2 move, float facingYaw) =>
        move.LengthSquared() > 0.01f ? Vector2.Normalize(move) : Ground.Forward(facingYaw);
}

// Charges regained one at a time, each RechargeTime after the last, starting when one is spent.
public sealed class ChargeCounter
{
    private readonly int _max;
    private readonly float _recharge;
    private float _fullAt = float.NegativeInfinity;

    public ChargeCounter(int max, float recharge)
    {
        if (max < 1 || recharge <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Needs at least one charge and a positive recharge time");
        }

        _max = max;
        _recharge = recharge;
    }

    public int Available(float now) => _max - (int)MathF.Ceiling(MathF.Max(0f, _fullAt - now) / _recharge);

    // Seconds until the next charge comes back; 0 when full.
    public float NextChargeIn(float now)
    {
        float missing = MathF.Max(0f, _fullAt - now);
        return missing <= 0f ? 0f : missing - (MathF.Ceiling(missing / _recharge) - 1f) * _recharge;
    }

    public bool TryUse(float now)
    {
        if (Available(now) < 1)
        {
            return false;
        }

        _fullAt = MathF.Max(_fullAt, now) + _recharge;
        return true;
    }
}
