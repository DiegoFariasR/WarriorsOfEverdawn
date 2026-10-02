using System;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Core.Stats;

public readonly record struct CharacterStats(int Str, int Wis, int Agi);

// First-pass stat effects, to be elaborated: STR scales a weapon's damage and WIS a spell's, AGI scales attack
// speed, WIS sets max mana.
public static class StatRules
{
    // Everdawn's GameBalance.MpPerWis.
    public const int ManaPerWis = 10;

    public const float DamagePerStr = 0.025f;
    public const float DamagePerWis = 0.05f;
    public const float AttackSpeedPerAgi = 0.0125f;
    public const float ManaRegenPerSecond = 5f;

    public static int MaxMana(CharacterStats stats) => stats.Wis * ManaPerWis;

    public static int Damage(int baseDamage, CharacterStats stats) =>
        (int)MathF.Round(baseDamage * (1f + stats.Str * DamagePerStr));

    // What a skill deals in these hands: the part of it that is magic grows with WIS, the rest with STR.
    public static int Damage(SkillDefinition skill, CharacterStats stats)
    {
        float magic = skill.Element == null ? 0f : skill.MagicShare;
        return (int)MathF.Round(skill.Damage * ((1f - magic) * (1f + stats.Str * DamagePerStr) + magic * (1f + stats.Wis * DamagePerWis)));
    }

    public static float AttackSpeed(CharacterStats stats) =>
        CombatTiming.BaseAttackSpeed * (1f + stats.Agi * AttackSpeedPerAgi);
}
