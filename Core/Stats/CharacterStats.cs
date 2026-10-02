using System;
using System.Collections.Generic;
using System.Linq;
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
    public static int Damage(SkillDefinition skill, CharacterStats stats) => Damage(skill, stats, Resistances.None);

    // The same on a body with resistances: each part of the damage by what the body makes of its type. With the
    // dealer's own bars, by what they make of its damage too: the dizzy hit softer, the blessed harder with the
    // divine.
    public static int Damage(SkillDefinition skill, CharacterStats stats, Resistances against, StatusBars? dealer = null)
    {
        float dealt = 0f;
        foreach (var (type, share) in skill.Parts)
        {
            dealt += share * Grown(type, stats) * (dealer?.Dealt(type) ?? 1f) * against.Taken(type);
        }

        return (int)MathF.Round(skill.Damage * dealt);
    }

    // What each part of a skill deals in these hands, before the body it lands on has its say: what a burn grows
    // by.
    public static IEnumerable<(DamageType Type, float Amount)> DamageByType(SkillDefinition skill, CharacterStats stats, StatusBars? dealer = null)
    {
        foreach (var (type, share) in skill.Parts)
        {
            yield return (type, skill.Damage * share * Grown(type, stats) * (dealer?.Dealt(type) ?? 1f));
        }
    }

    // A hit with the skill lands on a body: each part of it builds the body's bars by its share of the skill's
    // buildup.
    public static void Afflict(StatusBars body, SkillDefinition skill, CharacterStats stats, Resistances resistances, StatusBars? dealer = null)
    {
        foreach (var ((type, share), (_, amount)) in skill.Parts.Zip(DamageByType(skill, stats, dealer)))
        {
            body.Build(type, (int)MathF.Round(skill.Buildup * share), (int)MathF.Round(amount), resistances);
        }
    }

    private static float Grown(DamageType type, CharacterStats stats) =>
        DamageTypes.FamilyOf(type) == DamageFamily.Physical ? 1f + stats.Str * DamagePerStr : 1f + stats.Wis * DamagePerWis;

    public static float AttackSpeed(CharacterStats stats) =>
        CombatTiming.BaseAttackSpeed * (1f + stats.Agi * AttackSpeedPerAgi);
}
