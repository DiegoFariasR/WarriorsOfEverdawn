using System.Numerics;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class ProjectilesTests
{
    private static readonly ProjectileDefinition Arrow = Projectiles.Arrow;
    private static readonly float Touch = BodySize.Radius + Arrow.Radius;

    [Fact]
    public void Hits_a_body_the_step_passes_through_even_when_both_ends_are_clear_of_it()
    {
        var target = new Vector2(0f, 5f);

        Assert.True(Projectiles.Hits(new Vector2(0f, 4f), new Vector2(0f, 6f), target, BodySize.Radius, Arrow));
        Assert.True(Projectiles.Hits(new Vector2(0f, 0f), new Vector2(0f, 10f), target, BodySize.Radius, Arrow));
    }

    [Fact]
    public void Grazes_count_and_near_misses_do_not()
    {
        var from = new Vector2(-3f, 0f);
        var to = new Vector2(3f, 0f);

        Assert.True(Projectiles.Hits(from, to, new Vector2(0f, Touch * 0.99f), BodySize.Radius, Arrow));
        Assert.False(Projectiles.Hits(from, to, new Vector2(0f, Touch * 1.01f), BodySize.Radius, Arrow));
    }

    [Fact]
    public void A_body_behind_the_start_or_beyond_the_end_is_not_hit()
    {
        var from = new Vector2(0f, 0f);
        var to = new Vector2(0f, 2f);

        Assert.False(Projectiles.Hits(from, to, new Vector2(0f, -Touch * 1.5f), BodySize.Radius, Arrow));
        Assert.False(Projectiles.Hits(from, to, new Vector2(0f, 2f + Touch * 1.5f), BodySize.Radius, Arrow));
    }

    [Fact]
    public void A_standing_projectile_only_hits_a_body_it_is_inside()
    {
        var at = new Vector2(1f, 1f);

        Assert.True(Projectiles.Hits(at, at, at + new Vector2(Touch * 0.5f, 0f), BodySize.Radius, Arrow));
        Assert.False(Projectiles.Hits(at, at, at + new Vector2(Touch * 2f, 0f), BodySize.Radius, Arrow));
    }
}
