using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class CombatTimingTests
{
    private const float Speed = 2.5f;
    private static readonly SkillDefinition Swing = new("test-swing", Damage: 1, Range: 1f, HalfArc: 1f, HitTime: 0.6f);

    [Fact]
    public void A_single_moment_swing_closes_its_window_when_it_opens()
    {
        Assert.Equal(CombatTiming.HitDelay(Swing, Speed), CombatTiming.HitWindowEnd(Swing, Speed));
    }

    [Fact]
    public void A_sweep_window_runs_to_its_end_in_real_time()
    {
        var sweep = Swing with { SweepEnd = Swing.HitTime * 2f };

        Assert.Equal(sweep.SweepEnd!.Value / Speed, CombatTiming.HitWindowEnd(sweep, Speed), precision: 5);
        Assert.True(CombatTiming.HitWindowEnd(sweep, Speed) > CombatTiming.HitDelay(sweep, Speed));
    }

    [Fact]
    public void Hits_land_at_the_clip_hit_time_scaled_by_the_attack_speed()
    {
        Assert.Equal(Swing.HitTime / Speed, CombatTiming.HitDelay(Swing, Speed), precision: 5);
    }
}
