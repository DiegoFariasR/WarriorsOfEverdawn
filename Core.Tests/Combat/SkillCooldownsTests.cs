using WarriorsOfEverdawn.Core.Combat;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Combat;

public class SkillCooldownsTests
{
    private static readonly SkillDefinition Quick = new("quick", Damage: 1, Range: 1f, HalfArc: 1f, HitTime: 0.1f) { Type = DamageType.Slash };
    private static readonly SkillDefinition Slow = Quick with { Id = "slow", Cooldown = 4f };

    [Fact]
    public void Every_skill_starts_ready()
    {
        var cooldowns = new SkillCooldowns(2);

        Assert.True(cooldowns.IsReady(0, now: 0f));
        Assert.True(cooldowns.IsReady(1, now: 0f));
    }

    [Fact]
    public void A_used_skill_waits_its_cooldown_and_leaves_the_others_alone()
    {
        var cooldowns = new SkillCooldowns(2);
        const float UsedAt = 10f;

        cooldowns.Start(1, Slow, UsedAt);

        Assert.False(cooldowns.IsReady(1, UsedAt + Slow.Cooldown * 0.5f));
        Assert.Equal(Slow.Cooldown * 0.5f, cooldowns.Remaining(1, UsedAt + Slow.Cooldown * 0.5f), precision: 5);
        Assert.True(cooldowns.IsReady(1, UsedAt + Slow.Cooldown));
        Assert.Equal(0f, cooldowns.Remaining(1, UsedAt + Slow.Cooldown * 2f));
        Assert.True(cooldowns.IsReady(0, UsedAt));
    }

    [Fact]
    public void A_skill_without_cooldown_is_ready_again_at_once()
    {
        var cooldowns = new SkillCooldowns(1);

        cooldowns.Start(0, Quick, now: 5f);

        Assert.True(cooldowns.IsReady(0, now: 5f));
    }
}
