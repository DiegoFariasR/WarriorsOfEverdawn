namespace WarriorsOfEverdawn.Core.Combat;

// Swing clips play attackSpeed times faster than authored: BaseAttackSpeed for skeletons, raised by AGI for players
// (StatRules.AttackSpeed). Skill hit times are measured in clip time, so the real delay before a hit lands is
// HitTime / attackSpeed.
public static class CombatTiming
{
    public const float BaseAttackSpeed = 2f;

    public static float HitDelay(SkillDefinition skill, float attackSpeed) => skill.HitTime / attackSpeed;

    // Real time the hit window closes; the same as HitDelay for a single-moment swing.
    public static float HitWindowEnd(SkillDefinition skill, float attackSpeed) => (skill.SweepEnd ?? skill.HitTime) / attackSpeed;
}
