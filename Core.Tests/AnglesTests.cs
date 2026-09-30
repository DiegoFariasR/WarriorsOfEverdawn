using System;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests;

public class AnglesTests
{
    private const float Precision = 1e-5f;

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(MathF.PI * 1.5f, -MathF.PI * 0.5f)]
    [InlineData(-MathF.PI * 1.5f, MathF.PI * 0.5f)]
    [InlineData(MathF.Tau * 3f + 0.25f, 0.25f)]
    public void Wraps_into_a_half_turn_either_side(float input, float expected)
    {
        Assert.Equal(expected, Angles.Wrap(input), Precision);
    }

    [Fact]
    public void Rotate_toward_moves_by_at_most_the_step()
    {
        const float Step = 0.1f;

        Assert.Equal(Step, Angles.RotateToward(0f, 1f, Step), Precision);
        Assert.Equal(-Step, Angles.RotateToward(0f, -1f, Step), Precision);
    }

    [Fact]
    public void Rotate_toward_lands_on_the_target_without_overshooting()
    {
        Assert.Equal(0.05f, Angles.RotateToward(0f, 0.05f, 0.1f), Precision);
        Assert.Equal(1f, Angles.RotateToward(1f, 1f, 0.1f), Precision);
    }

    [Fact]
    public void Rotate_toward_goes_the_short_way_across_the_half_turn()
    {
        const float Step = 0.1f;
        float from = MathF.PI - 0.05f;
        float to = -MathF.PI + 0.05f;

        Assert.Equal(to, Angles.RotateToward(from, to, Step), Precision);
        Assert.Equal(Angles.Wrap(from + Step), Angles.RotateToward(from, -MathF.PI + 0.5f, Step), Precision);
    }

    [Fact]
    public void A_half_turn_wraps_to_positive()
    {
        Assert.Equal(MathF.PI, Angles.Wrap(-MathF.PI), Precision);
    }
}
