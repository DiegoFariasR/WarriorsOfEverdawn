using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class StaggerTests
{
    private const float HitAt = 10f;

    [Fact]
    public void A_forced_stagger_ignores_immunity_and_never_shortens_one_running()
    {
        var stagger = new Stagger();
        stagger.TryApply(HitAt);

        stagger.Force(HitAt + Stagger.Duration, Guard.ParryStagger);
        Assert.True(stagger.IsStaggered(HitAt + Stagger.Duration + Guard.ParryStagger * 0.9f));

        stagger.Force(HitAt + Stagger.Duration, Stagger.Duration * 0.1f);
        Assert.True(stagger.IsStaggered(HitAt + Stagger.Duration + Guard.ParryStagger * 0.9f));
        Assert.False(stagger.TryApply(HitAt + Stagger.Duration + Guard.ParryStagger));
    }

    [Fact]
    public void A_hit_staggers_for_the_duration()
    {
        var stagger = new Stagger();

        Assert.True(stagger.TryApply(HitAt));
        Assert.True(stagger.IsStaggered(HitAt + Stagger.Duration * 0.5f));
        Assert.False(stagger.IsStaggered(HitAt + Stagger.Duration));
    }

    [Fact]
    public void Hits_during_the_immunity_do_not_stagger_again()
    {
        var stagger = new Stagger();
        stagger.TryApply(HitAt);

        Assert.False(stagger.TryApply(HitAt + Stagger.Duration * 0.5f));
        Assert.False(stagger.TryApply(HitAt + Stagger.Duration + Stagger.Immunity * 0.5f));
        Assert.False(stagger.IsStaggered(HitAt + Stagger.Duration + Stagger.Immunity * 0.5f));
    }

    [Fact]
    public void Staggers_again_once_the_immunity_is_over()
    {
        var stagger = new Stagger();
        stagger.TryApply(HitAt);

        Assert.True(stagger.TryApply(HitAt + Stagger.Duration + Stagger.Immunity));
    }
}
