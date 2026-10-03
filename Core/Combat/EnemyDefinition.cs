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

    // How often it leaves a magic orb beside its gold, from 0 (never) to 1 (always).
    public float OrbChance { get; init; }

    // What it makes of each type of damage: nothing of any, unless said.
    public Resistances Resistances { get; init; } = Resistances.None;
}

public static class Enemies
{
    // Past the furthest a player's shot carries (a bow's), so a skeleton sees whoever can hit it.
    private const float Sight = 16f;

    // Bare bone: whatever cuts, breaks or runs it through does a quarter more, and so do fire and the divine.
    // First pass, and the one use of resistances so far.
    public const int SkeletonWeakness = -25;

    public static readonly Resistances Skeletal = Resistances.None
        .With(DamageFamily.Physical, SkeletonWeakness)
        .With(DamageType.Fire, SkeletonWeakness)
        .With(DamageType.Divine, SkeletonWeakness);

    public static readonly EnemyDefinition SkeletonMinion = new("skeleton-minion", MaxHp: 40, MoveSpeed: 2.6f, AggroRange: Sight, AttackCooldown: 1.6f, Skills.MinionChop)
    {
        Gold = new GoldDrop(2, 4),
        OrbChance = 0.02f,
        Resistances = Skeletal,
    };

    public static readonly EnemyDefinition SkeletonWarrior = new("skeleton-warrior", MaxHp: 70, MoveSpeed: 2.2f, AggroRange: Sight, AttackCooldown: 2.2f, Skills.WarriorChop)
    {
        Gold = new GoldDrop(6, 10),
        OrbChance = 0.06f,
        Resistances = Skeletal,
    };

    // Fragile, so reaching it is the answer; it keeps its distance to make that take effort.
    public static readonly EnemyDefinition SkeletonArcher = new("skeleton-archer", MaxHp: 30, MoveSpeed: 2.4f, AggroRange: Sight, AttackCooldown: 2.6f, Skills.ArcherShot)
    {
        KeepAway = 5f,
        Gold = new GoldDrop(3, 6),
        OrbChance = 0.03f,
        Resistances = Skeletal,
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
    public const float RespawnDelay = 10f;

    // First pass for the two-handed Knight: strength first.
    public static readonly CharacterStats KnightStats = new(Str: 12, Wis: 5, Agi: 8);
}
