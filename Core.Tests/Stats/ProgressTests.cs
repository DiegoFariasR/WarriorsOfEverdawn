using System;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Stats;

public class ProgressTests
{
    private static readonly CharacterStats Start = PlayerRules.KnightStats;

    [Fact]
    public void Each_level_asks_the_growth_times_the_xp_of_the_one_before()
    {
        Assert.Equal(LevelRules.FirstLevelXp, LevelRules.ToNext(LevelRules.FirstLevel));
        for (int level = LevelRules.FirstLevel + 1; level <= 10; level++)
        {
            Assert.Equal(LevelRules.ToNext(level - 1) * LevelRules.XpGrowth, LevelRules.ToNext(level), tolerance: 1f);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => LevelRules.ToNext(LevelRules.FirstLevel - 1));
    }

    [Fact]
    public void Reaching_a_level_takes_every_level_below_it()
    {
        Assert.Equal(0, LevelRules.Reaching(LevelRules.FirstLevel));
        Assert.Equal(
            Enumerable.Range(LevelRules.FirstLevel, 4).Sum(LevelRules.ToNext),
            LevelRules.Reaching(LevelRules.FirstLevel + 4));
    }

    [Fact]
    public void So_far_up_that_the_xp_no_longer_fits_it_says_so()
    {
        Assert.Throws<OverflowException>(() => LevelRules.ToNext(100));
    }

    [Fact]
    public void Starts_at_the_first_level_with_nothing_earned_and_its_stats()
    {
        var progress = new Progress(Start);

        Assert.Equal(LevelRules.FirstLevel, progress.Level);
        Assert.Equal(0, progress.Xp);
        Assert.Equal(0, progress.Points);
        Assert.Equal(Start, progress.Stats);
    }

    [Fact]
    public void Xp_short_of_the_next_level_is_kept_toward_it()
    {
        var progress = new Progress(Start);

        Assert.Equal(0, progress.Earn(LevelRules.FirstLevelXp - 1));
        Assert.Equal(LevelRules.FirstLevel, progress.Level);
        Assert.Equal(LevelRules.FirstLevelXp - 1, progress.Xp);
    }

    [Fact]
    public void Reaching_a_level_gives_its_point_and_carries_what_is_over_into_the_next()
    {
        var progress = new Progress(Start);

        Assert.Equal(1, progress.Earn(LevelRules.FirstLevelXp + 3));
        Assert.Equal(LevelRules.FirstLevel + 1, progress.Level);
        Assert.Equal(3, progress.Xp);
        Assert.Equal(LevelRules.PointsPerLevel, progress.Points);
    }

    [Fact]
    public void Xp_enough_for_several_levels_gives_them_all_at_once()
    {
        var progress = new Progress(Start);
        int target = LevelRules.FirstLevel + 3;

        Assert.Equal(3, progress.Earn(LevelRules.Reaching(target) + 1));
        Assert.Equal(target, progress.Level);
        Assert.Equal(1, progress.Xp);
        Assert.Equal(3 * LevelRules.PointsPerLevel, progress.Points);
        Assert.Equal(LevelRules.Reaching(target) + 1, LevelRules.Reaching(progress.Level) + progress.Xp);
    }

    [Fact]
    public void Xp_is_only_ever_earned()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Progress(Start).Earn(-1));
    }

    [Theory]
    [InlineData(Stat.Str)]
    [InlineData(Stat.Wis)]
    [InlineData(Stat.Agi)]
    public void A_point_raises_the_stat_it_goes_into_by_one_and_no_other(Stat stat)
    {
        var progress = new Progress(Start);
        progress.Earn(LevelRules.FirstLevelXp);

        Assert.True(progress.Spend(stat));
        Assert.Equal(0, progress.Points);
        Assert.Equal(Start.Total + 1, progress.Stats.Total);
        Assert.Equal(Start.Raised(stat), progress.Stats);
        Assert.Equal(stat == Stat.Str ? Start.Str + 1 : Start.Str, progress.Stats.Str);
        Assert.Equal(stat == Stat.Wis ? Start.Wis + 1 : Start.Wis, progress.Stats.Wis);
        Assert.Equal(stat == Stat.Agi ? Start.Agi + 1 : Start.Agi, progress.Stats.Agi);
    }

    [Fact]
    public void With_no_point_to_spend_nothing_is_raised()
    {
        var progress = new Progress(Start);

        Assert.False(progress.Spend(Stat.Str));
        Assert.Equal(Start, progress.Stats);
    }

    [Fact]
    public void Every_monster_gives_xp()
    {
        Assert.All(Enemies.All, enemy => Assert.True(enemy.Xp > 0, enemy.Id));
    }
}
