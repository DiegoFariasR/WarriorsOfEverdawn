using System.Collections.Generic;
using System.Linq;
using WarriorsOfEverdawn.Core.Stats;

namespace WarriorsOfEverdawn.Core.Combat;

public sealed record EnemyDefinition(string Id, int MaxHp, float MoveSpeed, float AggroRange, float AttackCooldown, SkillDefinition Attack);

public static class Enemies
{
    public static readonly EnemyDefinition SkeletonMinion = new("skeleton-minion", MaxHp: 40, MoveSpeed: 2.6f, AggroRange: 18f, AttackCooldown: 1.6f, Skills.MinionChop);
    public static readonly EnemyDefinition SkeletonWarrior = new("skeleton-warrior", MaxHp: 70, MoveSpeed: 2.2f, AggroRange: 18f, AttackCooldown: 2.2f, Skills.WarriorChop);

    public static IReadOnlyList<EnemyDefinition> All { get; } = new[] { SkeletonMinion, SkeletonWarrior };

    public static EnemyDefinition ById(string id) =>
        All.FirstOrDefault(e => e.Id == id) ?? throw new KeyNotFoundException($"Unknown enemy '{id}'");
}

public static class PlayerRules
{
    public const int MaxHp = 100;
    public const float RespawnDelay = 4f;

    // First pass for the two-handed Knight: strength first.
    public static readonly CharacterStats KnightStats = new(Str: 12, Wis: 5, Agi: 8);
}
