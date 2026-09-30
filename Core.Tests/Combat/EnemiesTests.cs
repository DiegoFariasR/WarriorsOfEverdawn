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
    public void Looks_up_by_id_and_fails_loudly_on_unknown()
    {
        foreach (var enemy in Enemies.All)
        {
            Assert.Same(enemy, Enemies.ById(enemy.Id));
        }

        Assert.Throws<KeyNotFoundException>(() => Enemies.ById("no-such-enemy"));
    }
}
