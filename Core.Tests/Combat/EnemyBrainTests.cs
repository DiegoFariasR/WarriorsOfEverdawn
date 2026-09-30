using System;
using System.Numerics;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class EnemyBrainTests
{
    private static readonly SkillDefinition Swing = new("test-swing", Damage: 5, Range: 1.5f, HalfArc: MathF.PI / 4f, HitTime: 0.5f);
    private static readonly EnemyDefinition Grunt = new("test-grunt", MaxHp: 10, MoveSpeed: 2f, AggroRange: 10f, AttackCooldown: 1f, Swing);

    private static readonly float EngageDistance = Swing.Range * EnemyBrain.EngageFraction + BodySize.Radius;

    private static EnemyTarget PlayerAt(long id, float distanceAhead) => new(id, Ground.Forward(0f) * distanceAhead);

    [Fact]
    public void Idles_with_no_living_players()
    {
        var decision = EnemyBrain.Decide(Grunt, Vector2.Zero, Array.Empty<EnemyTarget>(), attackReady: true);

        Assert.Equal(EnemyAction.Idle, decision.Action);
    }

    [Fact]
    public void Idles_when_everyone_is_beyond_aggro_range()
    {
        var decision = EnemyBrain.Decide(Grunt, Vector2.Zero, new[] { PlayerAt(1, Grunt.AggroRange * 1.1f) }, attackReady: true);

        Assert.Equal(EnemyAction.Idle, decision.Action);
    }

    [Fact]
    public void Chases_a_player_inside_aggro_but_out_of_reach()
    {
        var decision = EnemyBrain.Decide(Grunt, Vector2.Zero, new[] { PlayerAt(1, EngageDistance * 1.5f) }, attackReady: true);

        Assert.Equal(EnemyAction.Chase, decision.Action);
        Assert.Equal(1, decision.Target.Id);
    }

    [Fact]
    public void Attacks_in_reach_when_ready_and_holds_when_not()
    {
        var players = new[] { PlayerAt(1, EngageDistance * 0.9f) };

        Assert.Equal(EnemyAction.Attack, EnemyBrain.Decide(Grunt, Vector2.Zero, players, attackReady: true).Action);
        Assert.Equal(EnemyAction.Hold, EnemyBrain.Decide(Grunt, Vector2.Zero, players, attackReady: false).Action);
    }

    [Fact]
    public void Targets_the_nearest_player()
    {
        var players = new[] { PlayerAt(1, EngageDistance * 3f), PlayerAt(2, EngageDistance * 2f), PlayerAt(3, EngageDistance * 4f) };

        var decision = EnemyBrain.Decide(Grunt, Vector2.Zero, players, attackReady: true);

        Assert.Equal(2, decision.Target.Id);
    }
}
