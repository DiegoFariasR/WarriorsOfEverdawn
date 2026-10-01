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

        return now - _raisedAt <= guard.ParryWindow ? GuardOutcome.Parried : GuardOutcome.Blocked;
    }

    public static int DamageThrough(GuardDefinition guard, GuardOutcome outcome, int damage) => outcome switch
    {
        GuardOutcome.Unguarded => damage,
        GuardOutcome.Blocked => (int)MathF.Round(damage * guard.DamageTaken),
        _ => 0,
    };
}
