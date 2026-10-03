using System.Linq;
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

    [Fact]
    public void Agi_scales_run_speed_from_a_body_of_none()
    {
        Assert.Equal(1f, StatRules.MoveSpeedFactor(Plain));
        Assert.Equal(1f + 8 * StatRules.MoveSpeedPerAgi, StatRules.MoveSpeedFactor(Plain with { Agi = 8 }), precision: 5);
        Assert.Equal(StatRules.MoveSpeedFactor(Plain), StatRules.MoveSpeedFactor(Plain with { Str = 10, Wis = 10 }));
    }

    [Fact]
    public void Wis_scales_magic_damage_and_str_the_rest()
    {
        var fire = Weapons.Staff(Element.Fire).Primary;
        var cut = Weapons.Arms.First().Primary;

        Assert.True(StatRules.Damage(fire, Plain with { Wis = 10 }) > StatRules.Damage(fire, Plain));
        Assert.Equal(StatRules.Damage(fire, Plain), StatRules.Damage(fire, Plain with { Str = 10 }));
        Assert.True(StatRules.Damage(cut, Plain with { Str = 10 }) > StatRules.Damage(cut, Plain));
        Assert.Equal(StatRules.Damage(cut, Plain), StatRules.Damage(cut, Plain with { Wis = 10 }));
    }
}
