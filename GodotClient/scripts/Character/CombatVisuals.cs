using System.Collections.Generic;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Character;

// Presentation for Core's skills and enemies. Skill hit times in Core are measured from these clips.
public static class CombatVisuals
{
    private static readonly Dictionary<string, string> ClipBySkill = new()
    {
        [Skills.Slice.Id] = "melee/Melee_2H_Attack_Slice",
        [Skills.Spin.Id] = RigAnimations.SpinLoop,
        [Skills.MinionChop.Id] = "melee/Melee_1H_Attack_Chop",
        [Skills.WarriorChop.Id] = "melee/Melee_1H_Attack_Chop",
    };

    // Swings whose clip turns the root bone (Spin's loop turns the whole body) cannot be layered on the upper body.
    private static readonly HashSet<string> FullBodySkills = new() { Skills.Spin.Id };

    private static readonly Dictionary<string, (string Model, string Weapon)> LookByEnemy = new()
    {
        [Enemies.SkeletonMinion.Id] = ("res://assets/characters/Skeleton_Minion.glb", "res://assets/weapons/Skeleton_Blade.glb"),
        [Enemies.SkeletonWarrior.Id] = ("res://assets/characters/Skeleton_Warrior.glb", "res://assets/weapons/Skeleton_Axe.glb"),
    };

    public static string ClipFor(SkillDefinition skill) =>
        ClipBySkill.TryGetValue(skill.Id, out var clip) ? clip : throw new KeyNotFoundException($"No clip for skill '{skill.Id}'");

    public static bool IsFullBody(SkillDefinition skill) => FullBodySkills.Contains(skill.Id);

    public static (string Model, string Weapon) LookFor(EnemyDefinition enemy) =>
        LookByEnemy.TryGetValue(enemy.Id, out var look) ? look : throw new KeyNotFoundException($"No model for enemy '{enemy.Id}'");
}
