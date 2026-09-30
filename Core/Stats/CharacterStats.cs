using System;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Core.Stats;

public readonly record struct CharacterStats(int Str, int Wis, int Agi);

// First-pass stat effects, to be elaborated: STR scales damage, AGI scales attack speed, WIS sets max mana.
public static class StatRules
{
    // Everdawn's GameBalance.MpPerWis.
    public const int ManaPerWis = 10;

    public const float DamagePerStr = 0.025f;
    public const float AttackSpeedPerAgi = 0.0125f;
    public const float ManaRegenPerSecond = 5f;

    public static int MaxMana(CharacterStats stats) => stats.Wis * ManaPerWis;

    public static int Damage(int baseDamage, CharacterStats stats) =>
        (int)MathF.Round(baseDamage * (1f + stats.Str * DamagePerStr));

    public static float AttackSpeed(CharacterStats stats) =>
        CombatTiming.BaseAttackSpeed * (1f + stats.Agi * AttackSpeedPerAgi);
}
