using WarriorsOfEverdawn.Core.Level;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Level;

public class FloorsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(2)]
    public void A_floor_is_on_its_own_level_and_a_little_above_or_below_it_too(int level)
    {
        float floor = Floors.HeightOf(level);

        Assert.Equal(level, Floors.LevelOf(floor));
        Assert.Equal(level, Floors.LevelOf(floor + Floors.Storey / 4f));
        Assert.Equal(level, Floors.LevelOf(floor - Floors.Storey / 4f));
    }

    [Fact]
    public void Up_a_flight_of_stairs_a_body_changes_level_past_half_way()
    {
        Assert.Equal(0, Floors.LevelOf(Floors.Storey / 2f));
        Assert.Equal(1, Floors.LevelOf(Floors.Storey * 0.75f));
        Assert.Equal(-1, Floors.LevelOf(-Floors.Storey / 2f));
    }

    [Fact]
    public void A_thing_high_on_a_wall_is_on_the_floor_under_it()
    {
        Assert.Equal(0, Floors.FloorUnder(Floors.Storey * 0.75f));
        Assert.Equal(1, Floors.FloorUnder(Floors.HeightOf(1) + Floors.Storey * 0.75f));
        Assert.Equal(-1, Floors.FloorUnder(Floors.HeightOf(-1) + 0.05f));
        Assert.Equal(0, Floors.FloorUnder(-0.1f));
    }

    [Fact]
    public void Bodies_are_on_one_floor_when_less_than_half_a_storey_apart()
    {
        Assert.True(Floors.SameLevel(0f, Floors.Storey / 2f - 0.01f));
        Assert.False(Floors.SameLevel(0f, Floors.Storey / 2f));
        Assert.False(Floors.SameLevel(Floors.HeightOf(1), Floors.HeightOf(0)));
        Assert.False(Floors.SameLevel(Floors.HeightOf(-1), Floors.HeightOf(0)));
        Assert.True(Floors.SameLevel(Floors.HeightOf(-1), Floors.HeightOf(-1) + 0.3f));
    }
}
