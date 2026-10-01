using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Core.Locomotion;

public static class MoveSpeed
{
    public const float Run = 5.5f;

    // KayKit has no running-backwards clip, so backpedalling uses the walk clip at walk pace.
    public const float Backpedal = 2.5f;

    public static float For(LegDirection direction) => direction == LegDirection.Backward ? Backpedal : Run;

    // A swing that slows its caster sets the speed as a share of run speed in every direction: while a full-body
    // swing like Spin turns the body, facing (and so backpedalling) means nothing.
    public static float For(LegDirection direction, SkillDefinition? activeSkill) =>
        activeSkill is { MoveSpeedFactor: < 1f } slowing ? Run * slowing.MoveSpeedFactor : For(direction);

    // A raised guard slows every direction by its share; backpedalling behind it stays the slowest.
    public static float For(LegDirection direction, SkillDefinition? activeSkill, GuardDefinition? raisedGuard) =>
        raisedGuard is { } guard ? For(direction) * guard.MoveSpeedFactor : For(direction, activeSkill);
}
