using System.Numerics;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class GuardTests
{
    private const float RaisedAt = 10f;
    private const float FacingYaw = 0f;

    private static readonly GuardDefinition Definition =
        new(HalfArc: 60f * Angles.DegToRad, DamageTaken: 0.25f, MoveSpeedFactor: 0.5f, ParryWindow: 0.2f);

    private static readonly Vector2 Defender = Vector2.Zero;

    private static Vector2 AttackerAt(float yaw) => Defender + Ground.Forward(yaw) * 2f;

    private static Guard Raised()
    {
        var guard = new Guard();
        Assert.True(guard.TryRaise(RaisedAt));
        return guard;
    }

    [Fact]
    public void A_lowered_guard_stops_nothing()
    {
        Assert.Equal(GuardOutcome.Unguarded, new Guard().Resolve(Definition, RaisedAt, Defender, FacingYaw, AttackerAt(FacingYaw)));
    }

    [Fact]
    public void Hits_from_the_front_are_parried_early_and_blocked_after()
    {
        var guard = Raised();
        var front = AttackerAt(FacingYaw);

        Assert.Equal(GuardOutcome.Parried, guard.Resolve(Definition, RaisedAt + Definition.ParryWindow * 0.5f, Defender, FacingYaw, front));
        Assert.Equal(GuardOutcome.Blocked, guard.Resolve(Definition, RaisedAt + Definition.ParryWindow * 1.5f, Defender, FacingYaw, front));
    }

    [Fact]
    public void Only_hits_within_the_arc_are_guarded()
    {
        var guard = Raised();
        float later = RaisedAt + Definition.ParryWindow * 2f;

        Assert.Equal(GuardOutcome.Blocked, guard.Resolve(Definition, later, Defender, FacingYaw, AttackerAt(Definition.HalfArc * 0.9f)));
        Assert.Equal(GuardOutcome.Unguarded, guard.Resolve(Definition, later, Defender, FacingYaw, AttackerAt(Definition.HalfArc * 1.1f)));
        Assert.Equal(GuardOutcome.Unguarded, guard.Resolve(Definition, later, Defender, FacingYaw, AttackerAt(FacingYaw + System.MathF.PI)));
    }

    [Fact]
    public void The_guard_cannot_go_straight_back_up_after_lowering()
    {
        var guard = Raised();
        float lowered = RaisedAt + 1f;
        guard.Lower(lowered);

        Assert.False(guard.TryRaise(lowered + Guard.Recovery * 0.5f));
        Assert.Equal(Guard.Recovery * 0.5f, guard.RecoveryLeft(lowered + Guard.Recovery * 0.5f), precision: 5);
        Assert.True(guard.TryRaise(lowered + Guard.Recovery * 1.01f));
    }

    [Fact]
    public void Damage_through_is_all_unguarded_a_share_blocked_and_none_parried()
    {
        const int damage = 12;

        Assert.Equal(damage, Guard.DamageThrough(Definition, GuardOutcome.Unguarded, damage));
        Assert.Equal((int)System.MathF.Round(damage * Definition.DamageTaken), Guard.DamageThrough(Definition, GuardOutcome.Blocked, damage));
        Assert.Equal(0, Guard.DamageThrough(Definition, GuardOutcome.Parried, damage));
    }
}
