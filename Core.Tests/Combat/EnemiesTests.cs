using System;
using System.Collections.Generic;
using System.Linq;
using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class EnemiesTests
{
    [Fact]
    public void Every_enemy_has_a_unique_id_and_usable_stats()
    {
        Assert.NotEmpty(Enemies.All);
        Assert.Equal(Enemies.All.Count, Enemies.All.Select(e => e.Id).Distinct().Count());
        foreach (var enemy in Enemies.All)
        {
            Assert.True(enemy.MaxHp > 0, enemy.Id);
            Assert.True(enemy.MoveSpeed > 0f, enemy.Id);
            Assert.True(enemy.AttackCooldown > 0f, enemy.Id);
            Assert.True(enemy.AggroRange > enemy.Attack.Range, enemy.Id);
            Assert.True(enemy.Attack.Damage > 0, enemy.Id);
        }
    }

    [Fact]
    public void Every_enemy_sees_further_than_any_weapon_reaches()
    {
        float furthest = Weapons.All.SelectMany(w => w.Skills).Max(s => Math.Max(s.Range, s.Projectile?.MaxDistance ?? 0f) + s.BlastRadius);

        Assert.All(Enemies.All, enemy => Assert.True(enemy.AggroRange > furthest, enemy.Id));
    }

    [Fact]
    public void Looks_up_by_id_and_fails_loudly_on_unknown()
    {
        foreach (var enemy in Enemies.All)
        {
            Assert.Same(enemy, Enemies.ById(enemy.Id));
        }

        Assert.Throws<KeyNotFoundException>(() => Enemies.ById("no-such-enemy"));
    }

    [Fact]
    public void Looks_up_attacks_by_id_and_fails_loudly_on_unknown()
    {
        foreach (var enemy in Enemies.All)
        {
            Assert.Same(enemy.Attack, Enemies.AttackById(enemy.Attack.Id));
        }

        Assert.Throws<KeyNotFoundException>(() => Enemies.AttackById(Skills.Slice.Id));
    }

    [Fact]
    public void Ranged_enemies_keep_away_from_inside_their_shooting_range_and_melee_ones_do_not()
    {
        foreach (var enemy in Enemies.All)
        {
            if (enemy.Attack.Projectile is { } projectile)
            {
                Assert.True(enemy.KeepAway > 0f && enemy.KeepAway < enemy.Attack.Range * EnemyBrain.EngageFraction, enemy.Id);
                Assert.True(projectile.MaxDistance >= enemy.Attack.Range, enemy.Id);
            }
            else
            {
                Assert.Equal(0f, enemy.KeepAway);
            }
        }
    }
}
