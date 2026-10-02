using System;
using System.Numerics;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class MeleeArcTests
{
    private const float Radius = 0.5f;
    private static readonly SkillDefinition Swing = new("test-swing", Damage: 10, Range: 2f, HalfArc: MathF.PI / 4f, HitTime: 0.5f) { Type = DamageType.Slash };

    private static bool HitsAt(float yawFromAim, float edgeDistance) =>
        MeleeArc.Hits(Vector2.Zero, aimYaw: 0f, Swing, Ground.Forward(yawFromAim) * (edgeDistance + Radius), Radius);

    [Fact]
    public void Hits_straight_ahead_within_range()
    {
        Assert.True(HitsAt(0f, Swing.Range * 0.5f));
        Assert.True(HitsAt(0f, Swing.Range));
    }

    [Fact]
    public void Misses_past_range()
    {
        Assert.False(HitsAt(0f, Swing.Range * 1.05f));
    }

    [Fact]
    public void Misses_behind()
    {
        Assert.False(HitsAt(MathF.PI, Swing.Range * 0.5f));
    }

    [Fact]
    public void Body_width_widens_the_arc()
    {
        float centreDistance = Swing.Range * 0.5f + Radius;
        float bodyAngle = MathF.Asin(Radius / centreDistance);

        Assert.True(HitsAt(Swing.HalfArc + bodyAngle * 0.9f, Swing.Range * 0.5f));
        Assert.True(HitsAt(-(Swing.HalfArc + bodyAngle * 0.9f), Swing.Range * 0.5f));
        Assert.False(HitsAt(Swing.HalfArc + bodyAngle * 1.1f, Swing.Range * 0.5f));
    }

    [Fact]
    public void An_overlapping_target_is_always_hit()
    {
        Assert.True(MeleeArc.Hits(Vector2.Zero, 0f, Swing, Ground.Forward(MathF.PI) * (Radius * 0.5f), Radius));
    }

    [Fact]
    public void Follows_the_aim_direction()
    {
        float aim = MathF.PI / 2f;
        var target = Ground.Forward(aim) * (Swing.Range * 0.5f + Radius);

        Assert.True(MeleeArc.Hits(Vector2.Zero, aim, Swing, target, Radius));
        Assert.False(MeleeArc.Hits(Vector2.Zero, -aim, Swing, target, Radius));
    }
}
