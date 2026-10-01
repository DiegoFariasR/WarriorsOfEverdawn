using System.Collections.Generic;
using System.Linq;
using WarriorsOfEverdawn.Core.Loot;
using WarriorsOfEverdawn.Core.Stats;

namespace WarriorsOfEverdawn.Core.Combat;

public sealed record EnemyDefinition(string Id, int MaxHp, float MoveSpeed, float AggroRange, float AttackCooldown, SkillDefinition Attack)
{
    // A ranged enemy backs away from a player closer than this while it cannot shoot yet. 0 for melee enemies.
    public float KeepAway { get; init; }

    // Left on the ground where it dies.
    public GoldDrop Gold { get; init; } = new(0, 0);

    // Given to every player as it dies.
    public int Souls { get; init; } = 1;
}

public static class Enemies
{
    // Further than the arena is long: a wave marches from its fortress on the players wherever they are.
    private const float MarchRange = 100f;

    public static readonly EnemyDefinition SkeletonMinion = new("skeleton-minion", MaxHp: 40, MoveSpeed: 2.6f, AggroRange: MarchRange, AttackCooldown: 1.6f, Skills.MinionChop)
    {
        Gold = new GoldDrop(2, 4),
    };

    public static readonly EnemyDefinition SkeletonWarrior = new("skeleton-warrior", MaxHp: 70, MoveSpeed: 2.2f, AggroRange: MarchRange, AttackCooldown: 2.2f, Skills.WarriorChop)
    {
        Gold = new GoldDrop(6, 10),
    };

    // Fragile, so reaching it is the answer; it keeps its distance to make that take effort.
    public static readonly EnemyDefinition SkeletonArcher = new("skeleton-archer", MaxHp: 30, MoveSpeed: 2.4f, AggroRange: MarchRange, AttackCooldown: 2.6f, Skills.ArcherShot)
    {
        KeepAway = 5f,
        Gold = new GoldDrop(3, 6),
    };

    public static IReadOnlyList<EnemyDefinition> All { get; } = new[] { SkeletonMinion, SkeletonWarrior, SkeletonArcher };

    public static EnemyDefinition ById(string id) =>
        All.FirstOrDefault(e => e.Id == id) ?? throw new KeyNotFoundException($"Unknown enemy '{id}'");

    // Projectiles travel between machines by the id of the attack that loosed them.
    public static SkillDefinition AttackById(string id) =>
        All.Select(e => e.Attack).FirstOrDefault(s => s.Id == id) ?? throw new KeyNotFoundException($"No enemy attacks with '{id}'");
}

public static class PlayerRules
{
    public const int MaxHp = 100;
    public const float RespawnDelay = 4f;

    // First pass for the two-handed Knight: strength first.
    public static readonly CharacterStats KnightStats = new(Str: 12, Wis: 5, Agi: 8);
}
