using System;
using System.Numerics;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests;

public class GroundTests
{
    private const float Precision = 1e-5f;

    [Fact]
    public void Yaw_zero_faces_negative_z_and_positive_yaw_turns_left()
    {
        AssertClose(new Vector2(0f, -1f), Ground.Forward(0f));
        AssertClose(new Vector2(-1f, 0f), Ground.Forward(MathF.PI / 2f));
    }

    [Theory]
    [InlineData(0.3f, -2f)]
    [InlineData(-4f, 1f)]
    [InlineData(1f, 5f)]
    public void Forward_of_yaw_of_a_direction_is_that_direction(float x, float y)
    {
        var direction = new Vector2(x, y);

        AssertClose(Vector2.Normalize(direction), Ground.Forward(Ground.YawOf(direction)));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.7f)]
    [InlineData(-2.5f)]
    public void Facing_relative_input_goes_along_the_facing_and_to_its_right(float yaw)
    {
        var forward = Ground.Forward(yaw);
        var right = Ground.Forward(yaw - MathF.PI / 2f);

        AssertClose(forward, Ground.FromFacing(1f, 0f, yaw));
        AssertClose(-forward, Ground.FromFacing(-1f, 0f, yaw));
        AssertClose(right, Ground.FromFacing(0f, 1f, yaw));
        AssertClose(forward + right, Ground.FromFacing(1f, 1f, yaw));
    }

    [Fact]
    public void Right_of_the_default_facing_is_positive_x()
    {
        AssertClose(new Vector2(1f, 0f), Ground.FromFacing(0f, 1f, 0f));
    }

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, Precision);
        Assert.Equal(expected.Y, actual.Y, Precision);
    }
}
