using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Core.Stats;
using Xunit;

namespace WarriorsOfEverdawn.Core.Tests.Stats;

public class StatRulesTests
{
    private static readonly CharacterStats Plain = new(Str: 0, Wis: 0, Agi: 0);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void Max_mana_comes_from_wis(int wis)
    {
        Assert.Equal(wis * StatRules.ManaPerWis, StatRules.MaxMana(Plain with { Wis = wis }));
    }

    [Fact]
    public void Str_scales_damage_and_nothing_else()
    {
        const int BaseDamage = 40;
        var strong = Plain with { Str = 10 };

        Assert.Equal(BaseDamage, StatRules.Damage(BaseDamage, Plain));
        Assert.Equal((int)System.MathF.Round(BaseDamage * (1f + 10 * StatRules.DamagePerStr)), StatRules.Damage(BaseDamage, strong));
        Assert.Equal(StatRules.AttackSpeed(Plain), StatRules.AttackSpeed(strong));
        Assert.Equal(StatRules.MaxMana(Plain), StatRules.MaxMana(strong));
    }

    [Fact]
    public void Agi_scales_attack_speed_from_the_base()
    {
        var quick = Plain with { Agi = 8 };

        Assert.Equal(CombatTiming.BaseAttackSpeed, StatRules.AttackSpeed(Plain));
        Assert.Equal(CombatTiming.BaseAttackSpeed * (1f + 8 * StatRules.AttackSpeedPerAgi), StatRules.AttackSpeed(quick), precision: 5);
    }
}
