using System;
using System.Numerics;

namespace WarriorsOfEverdawn.Core.Combat;

// A weapon's guard, its third skill, held on its own button. Hits from within HalfArc of where the defender faces are
// blocked, letting DamageTaken of their damage through; in the first ParryWindow seconds after the guard goes up they
// are parried instead: nothing gets through and a melee attacker is staggered. The defender moves at MoveSpeedFactor
// of its usual speed while guarding.
public sealed record GuardDefinition(float HalfArc, float DamageTaken, float MoveSpeedFactor, float ParryWindow)
{
    public string Name { get; init; } = "Guard";

    // A guard of magic and not of the weapon: what it stops comes off a pool of its own, and when that is spent it
    // stops nothing. Null for a guard held with the weapon.
    public BarrierDefinition? Barrier { get; init; }
}

// How much a barrier takes before it gives (Strength), and how it comes back: RechargePerSecond, once it has been
// down for RechargeDelay.
public sealed record BarrierDefinition(int Strength, float RechargePerSecond, float RechargeDelay);

// One character's barrier over time: what is left of it. It only comes back while it is down.
public sealed class BarrierPool
{
    private float _left;
    private float _down;

    public BarrierPool(BarrierDefinition barrier)
    {
        Definition = barrier;
        _left = barrier.Strength;
    }

    public BarrierDefinition Definition { get; }

    public int Left => (int)_left;

    public bool Holds => Left > 0;

    // Takes what it can of a blow; what it cannot take gets through.
    public int Absorb(int damage)
    {
        int taken = Math.Clamp(damage, 0, Left);
        _left -= taken;
        return damage - taken;
    }

    public void Advance(float delta, bool up)
    {
        _down = up ? 0f : _down + delta;
        if (_down >= Definition.RechargeDelay)
        {
            _left = MathF.Min(Definition.Strength, _left + Definition.RechargePerSecond * delta);
        }
    }
}

public enum GuardOutcome
{
    Unguarded,
    Blocked,
    Parried,
}

// One character's guard over time. Times are passed in.
public sealed class Guard
{
    // After lowering, the guard cannot go up again for this long, so parries cannot be had by tapping the button.
    public const float Recovery = 0.4f;

    // A parried melee attacker is staggered this long, which interrupts its swing.
    public const float ParryStagger = 1f;

    private float _raisedAt = float.NegativeInfinity;
    private float _loweredAt = float.NegativeInfinity;

    public bool IsUp { get; private set; }

    public bool CanRaise(float now) => !IsUp && now - _loweredAt >= Recovery;

    public float RecoveryLeft(float now) => IsUp ? 0f : MathF.Max(0f, Recovery - (now - _loweredAt));

    public bool TryRaise(float now)
    {
        if (!CanRaise(now))
        {
            return false;
        }

        Raise(now);
        return true;
    }

    // Raises it whatever the recovery: for a machine told the guard went up by the one that decided it, whose clock
    // may have run a little differently.
    public void Raise(float now)
    {
        IsUp = true;
        _raisedAt = now;
    }

    public void Lower(float now)
    {
        if (IsUp)
        {
            IsUp = false;
            _loweredAt = now;
        }
    }

    // An attack coming from `from` against a defender at `position` facing `facingYaw`. An attacker standing exactly
    // on the defender counts as in front.
    public GuardOutcome Resolve(GuardDefinition guard, float now, Vector2 position, float facingYaw, Vector2 from)
    {
        if (!IsUp)
        {
            return GuardOutcome.Unguarded;
        }

        var toAttacker = from - position;
        if (toAttacker.LengthSquared() > 0f && MathF.Abs(Angles.Wrap(Ground.YawOf(toAttacker) - facingYaw)) > guard.HalfArc)
        {
            return GuardOutcome.Unguarded;
        }

        return guard.ParryWindow > 0f && now - _raisedAt <= guard.ParryWindow ? GuardOutcome.Parried : GuardOutcome.Blocked;
    }

    public static int DamageThrough(GuardDefinition guard, GuardOutcome outcome, int damage) => outcome switch
    {
        GuardOutcome.Unguarded => damage,
        GuardOutcome.Blocked => (int)MathF.Round(damage * guard.DamageTaken),
        _ => 0,
    };
}
