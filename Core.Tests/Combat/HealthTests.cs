using System;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class HealthTests
{
    private const int Max = 50;

    [Fact]
    public void Starts_full()
    {
        var health = new Health(Max);

        Assert.Equal(Max, health.Current);
        Assert.False(health.IsDead);
    }

    [Fact]
    public void Damage_reduces_current_and_reports_what_was_taken()
    {
        var health = new Health(Max);

        int taken = health.TakeDamage(Max / 5);

        Assert.Equal(Max / 5, taken);
        Assert.Equal(Max - Max / 5, health.Current);
    }

    [Fact]
    public void Overkill_takes_only_what_was_left()
    {
        var health = new Health(Max);
        health.TakeDamage(Max - 1);

        int taken = health.TakeDamage(Max);

        Assert.Equal(1, taken);
        Assert.Equal(0, health.Current);
        Assert.True(health.IsDead);
    }

    [Fact]
    public void A_dead_target_takes_nothing_more()
    {
        var health = new Health(Max);
        health.TakeDamage(Max);

        Assert.Equal(0, health.TakeDamage(Max));
        Assert.Equal(0, health.Current);
    }

    [Fact]
    public void Restore_brings_back_to_full()
    {
        var health = new Health(Max);
        health.TakeDamage(Max);

        health.RestoreFull();

        Assert.Equal(Max, health.Current);
        Assert.False(health.IsDead);
    }

    [Fact]
    public void Rejects_negative_damage_and_non_positive_maximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Health(Max).TakeDamage(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Health(0));
    }
}
