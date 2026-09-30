using System;

namespace WarriorsOfEverdawn.Core.Combat;

// HitTime is seconds into the swing clip, in clip time, when damage lands; see CombatTiming for real time.
public sealed record SkillDefinition(string Id, int Damage, float Range, float HalfArc, float HitTime)
{
    // Clip time a sweep's hit window closes. Unset for a single-moment swing. While the window is open, each
    // target is hit at most once.
    public float? SweepEnd { get; init; }

    public float Cooldown { get; init; }

    public int ManaCost { get; init; }

    // Share of run speed the caster keeps while the swing plays: 1 moves as usual, 0 would root it in place.
    public float MoveSpeedFactor { get; init; } = 1f;

    // Loops for as long as its button is held and the caster can pay ManaCost for each cycle. Each cycle is a fresh
    // hit window: every target can be hit once per cycle.
    public bool Channeled { get; init; }
}

public static class Skills
{
    // Hit times are the blade's peak speed in the KayKit clips: Melee_2H_Attack_Slice 0.38 s, Melee_1H_Attack_Chop 0.60 s.
    // Ranges are how far the blade tip reaches from the body centre at the hit moment, with the real blade lengths from
    // the weapon meshes (sword_2handed 1.96, Skeleton_Blade 1.17, Skeleton_Axe 1.0), so the hit area ends where the
    // drawn blade does. net-test measures the live tip at every hit test against these. Slice reaches 2.02 in the
    // standing clip but about 1.86 in play, where it often runs on the upper body while moving and twisting.
    public static readonly SkillDefinition Slice = new("slice", Damage: 20, Range: 1.9f, HalfArc: 70f * Angles.DegToRad, HitTime: 0.38f);

    // Channeled on Melee_2H_Attack_Spinning: a seamless loop turning the whole body one revolution per 0.667 s of clip
    // time through its root bone, blade tip at a constant 2.28 reach at waist height. Each revolution is one cycle:
    // it costs ManaCost and its sweep covers the whole revolution. The caster glides at half speed while spinning.
    public static readonly SkillDefinition Spin = new("spin", Damage: 10, Range: 2.3f, HalfArc: MathF.PI, HitTime: 0f)
    {
        SweepEnd = 0.6667f,
        ManaCost = 4,
        MoveSpeedFactor = 0.5f,
        Channeled = true,
    };
    public static readonly SkillDefinition MinionChop = new("minion-chop", Damage: 6, Range: 1.65f, HalfArc: 45f * Angles.DegToRad, HitTime: 0.60f);
    public static readonly SkillDefinition WarriorChop = new("warrior-chop", Damage: 12, Range: 1.5f, HalfArc: 45f * Angles.DegToRad, HitTime: 0.60f);
}
