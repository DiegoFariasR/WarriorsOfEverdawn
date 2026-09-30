using System;
using System.Collections.Generic;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Locomotion;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Locomotion;

public class LegDirectionSelectorTests
{
    private const float Precision = 1e-5f;
    private const float QuarterTurn = MathF.PI / 2f;
    private const float InsideHysteresisBand = LegDirectionSelector.QuadrantHalfWidth + LegDirectionSelector.Hysteresis / 2f;
    private const float PastHysteresisBand = LegDirectionSelector.QuadrantHalfWidth + LegDirectionSelector.Hysteresis * 1.5f;

    public static IEnumerable<object[]> AllDirections() =>
        Enum.GetValues<LegDirection>().Select(d => new object[] { d });

    [Theory]
    [InlineData(0f, LegDirection.Forward)]
    [InlineData(QuarterTurn, LegDirection.Left)]
    [InlineData(-QuarterTurn, LegDirection.Right)]
    [InlineData(MathF.PI, LegDirection.Backward)]
    public void Picks_the_quadrant_the_movement_points_into(float moveYawFromAim, LegDirection expected)
    {
        foreach (var current in Enum.GetValues<LegDirection>())
        {
            var pose = LegDirectionSelector.Select(moveYawFromAim, current);

            Assert.Equal(expected, pose.Direction);
            Assert.Equal(0f, pose.BodyYawOffset, Precision);
        }
    }

    [Fact]
    public void Keeps_the_current_clip_inside_the_hysteresis_band()
    {
        var pose = LegDirectionSelector.Select(InsideHysteresisBand, LegDirection.Forward);

        Assert.Equal(LegDirection.Forward, pose.Direction);
        Assert.Equal(InsideHysteresisBand, pose.BodyYawOffset, Precision);
    }

    [Fact]
    public void Switches_clip_past_the_hysteresis_band()
    {
        var pose = LegDirectionSelector.Select(PastHysteresisBand, LegDirection.Forward);

        Assert.Equal(LegDirection.Left, pose.Direction);
        Assert.Equal(PastHysteresisBand - QuarterTurn, pose.BodyYawOffset, Precision);
    }

    [Fact]
    public void Backward_holds_across_the_wrap_point()
    {
        float justPastHalfTurn = -MathF.PI + LegDirectionSelector.Hysteresis;

        var pose = LegDirectionSelector.Select(justPastHalfTurn, LegDirection.Backward);

        Assert.Equal(LegDirection.Backward, pose.Direction);
        Assert.Equal(LegDirectionSelector.Hysteresis, pose.BodyYawOffset, Precision);
    }

    [Theory]
    [MemberData(nameof(AllDirections))]
    public void Offset_stays_within_the_band_and_reconstructs_the_movement(LegDirection current)
    {
        const int Steps = 720;
        for (int i = 0; i < Steps; i++)
        {
            float angle = Angles.Wrap(i * MathF.Tau / Steps);

            var pose = LegDirectionSelector.Select(angle, current);

            Assert.InRange(MathF.Abs(pose.BodyYawOffset), 0f, LegDirectionSelector.QuadrantHalfWidth + LegDirectionSelector.Hysteresis + Precision);
            Assert.Equal(0f, Angles.Wrap(LegDirectionSelector.CentreOf(pose.Direction) + pose.BodyYawOffset - angle), Precision);
        }
    }

    [Fact]
    public void A_slowing_swing_sets_its_share_of_run_speed_in_every_direction()
    {
        var slowing = new SkillDefinition("test-slow", Damage: 1, Range: 1f, HalfArc: 1f, HitTime: 0f) { MoveSpeedFactor = 0.5f };
        var normal = slowing with { MoveSpeedFactor = 1f };

        foreach (var direction in Enum.GetValues<LegDirection>())
        {
            Assert.Equal(MoveSpeed.Run * slowing.MoveSpeedFactor, MoveSpeed.For(direction, slowing));
            Assert.Equal(MoveSpeed.For(direction), MoveSpeed.For(direction, normal));
            Assert.Equal(MoveSpeed.For(direction), MoveSpeed.For(direction, null));
        }
    }

    [Fact]
    public void Backpedal_uses_its_own_speed()
    {
        Assert.Equal(MoveSpeed.Backpedal, MoveSpeed.For(LegDirection.Backward));
        Assert.Equal(MoveSpeed.Run, MoveSpeed.For(LegDirection.Forward));
        Assert.Equal(MoveSpeed.Run, MoveSpeed.For(LegDirection.Left));
        Assert.Equal(MoveSpeed.Run, MoveSpeed.For(LegDirection.Right));
    }
}
