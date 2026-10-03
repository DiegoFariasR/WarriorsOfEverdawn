using System;
using System.Collections.Generic;
using System.Numerics;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class EnemyBrainTests
{
    private static readonly SkillDefinition Swing = new("test-swing", Damage: 5, Range: 1.5f, HalfArc: MathF.PI / 4f, HitTime: 0.5f) { Type = DamageType.Slash };
    private static readonly EnemyDefinition Grunt = new("test-grunt", MaxHp: 10, MoveSpeed: 2f, AggroRange: 10f, AttackCooldown: 1f, Swing);

    private static readonly float EngageDistance = Swing.Range * EnemyBrain.EngageFraction + BodySize.Radius;

    private static readonly SkillDefinition Shot = Swing with { Id = "test-shot", Range = 8f, Projectile = Projectiles.Arrow };
    private static readonly EnemyDefinition Archer = Grunt with { Id = "test-archer", AggroRange = 16f, Attack = Shot, KeepAway = 4f };

    private static EnemyTarget PlayerAt(long id, float distanceAhead) => new(id, Ground.Forward(0f) * distanceAhead);

    // At its post, at the origin.
    private static EnemyDecision Decide(EnemyDefinition enemy, IReadOnlyList<EnemyTarget> players, bool attackReady, bool engaged = false) =>
        EnemyBrain.Decide(enemy, Vector2.Zero, Vector2.Zero, players, attackReady, engaged);

    [Fact]
    public void Idles_with_no_living_players()
    {
        var decision = Decide(Grunt, Array.Empty<EnemyTarget>(), attackReady: true);

        Assert.Equal(EnemyAction.Idle, decision.Action);
    }

    [Fact]
    public void Idles_when_everyone_is_beyond_aggro_range()
    {
        var decision = Decide(Grunt, new[] { PlayerAt(1, Grunt.AggroRange * 1.1f) }, attackReady: true);

        Assert.Equal(EnemyAction.Idle, decision.Action);
    }

    [Fact]
    public void Keeps_after_a_player_out_of_aggro_range_until_it_is_past_the_give_up_range()
    {
        var justOutOfSight = new[] { PlayerAt(1, Grunt.AggroRange * (1f + EnemyBrain.GiveUpFactor) / 2f) };
        var gone = new[] { PlayerAt(1, Grunt.AggroRange * EnemyBrain.GiveUpFactor * 1.1f) };

        Assert.Equal(EnemyAction.Idle, Decide(Grunt, justOutOfSight, attackReady: true).Action);
        Assert.Equal(EnemyAction.Chase, Decide(Grunt, justOutOfSight, attackReady: true, engaged: true).Action);
        Assert.Equal(EnemyAction.Idle, Decide(Grunt, gone, attackReady: true, engaged: true).Action);
    }

    [Fact]
    public void Walks_back_to_its_post_with_no_player_in_sight()
    {
        var post = Ground.Forward(0f) * -EnemyBrain.AtPost * 3f;
        var nearPost = post + Ground.Forward(0f) * EnemyBrain.AtPost * 0.5f;
        var gone = new[] { PlayerAt(1, Grunt.AggroRange * EnemyBrain.GiveUpFactor * 1.1f) };

        var away = EnemyBrain.Decide(Grunt, Vector2.Zero, post, gone, attackReady: true, engaged: true);
        var back = EnemyBrain.Decide(Grunt, nearPost, post, gone, attackReady: true, engaged: false);

        Assert.Equal(EnemyAction.Return, away.Action);
        Assert.Equal(post, away.Target.Position);
        Assert.Equal(EnemyAction.Idle, back.Action);
    }

    [Fact]
    public void Chases_a_player_inside_aggro_but_out_of_reach()
    {
        var decision = Decide(Grunt, new[] { PlayerAt(1, EngageDistance * 1.5f) }, attackReady: true);

        Assert.Equal(EnemyAction.Chase, decision.Action);
        Assert.Equal(1, decision.Target.Id);
    }

    [Fact]
    public void Attacks_in_reach_when_ready_and_holds_when_not()
    {
        var players = new[] { PlayerAt(1, EngageDistance * 0.9f) };

        Assert.Equal(EnemyAction.Attack, Decide(Grunt, players, attackReady: true).Action);
        Assert.Equal(EnemyAction.Hold, Decide(Grunt, players, attackReady: false).Action);
    }

    [Fact]
    public void A_ranged_enemy_backs_away_from_a_close_player_while_it_cannot_shoot()
    {
        var close = new[] { PlayerAt(1, Archer.KeepAway * 0.5f) };
        var inRange = new[] { PlayerAt(1, (Archer.KeepAway + Shot.Range * EnemyBrain.EngageFraction) / 2f) };

        Assert.Equal(EnemyAction.Retreat, Decide(Archer, close, attackReady: false).Action);
        Assert.Equal(EnemyAction.Attack, Decide(Archer, close, attackReady: true).Action);
        Assert.Equal(EnemyAction.Hold, Decide(Archer, inRange, attackReady: false).Action);
    }

    [Fact]
    public void A_melee_enemy_never_backs_away()
    {
        var hugging = new[] { PlayerAt(1, BodySize.Radius) };

        Assert.Equal(EnemyAction.Hold, Decide(Grunt, hugging, attackReady: false).Action);
    }

    [Fact]
    public void Targets_the_nearest_player()
    {
        var players = new[] { PlayerAt(1, EngageDistance * 3f), PlayerAt(2, EngageDistance * 2f), PlayerAt(3, EngageDistance * 4f) };

        var decision = Decide(Grunt, players, attackReady: true);

        Assert.Equal(2, decision.Target.Id);
    }
}
