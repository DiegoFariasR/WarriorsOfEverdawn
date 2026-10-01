using System;

namespace WarriorsOfEverdawn.Core.Combat;

// HitTime is seconds into the swing clip, in clip time, when damage lands; see CombatTiming for real time.
public sealed record SkillDefinition(string Id, int Damage, float Range, float HalfArc, float HitTime)
{
    // Shown on the skill bar.
    public string Name { get; init; } = "";

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

    // A ranged attack looses this at HitTime toward its target instead of testing an arc; Range is then how far away
    // the attacker is willing to shoot from.
    public ProjectileDefinition? Projectile { get; init; }
}

public static class Skills
{
    // One revolution of Melee_2H_Attack_Spinning, the loop every weapon's Spin plays: it turns the whole body through
    // its root bone, so each revolution is one cycle, paid for as it starts, with a sweep covering all of it. The
    // caster glides at half speed while spinning.
    private const float SpinRevolution = 0.6667f;
    private const float SpinMoveSpeedFactor = 0.5f;

    // A thrust covers a narrow line where a swing covers an arc, so it hits this many times as hard as the weapon's
    // swing.
    public const int ThrustDamageFactor = 2;

    private const float ThrustHalfArc = 20f * Angles.DegToRad;

    // Melee_2H_Attack_Stab, the clip every thrust plays, at full extension (./dev.sh swing-survey).
    private const float StabExtended = 0.764f;

    // Share of the way to full extension at which a lunge's point starts forward and its hit window opens.
    private const float LungeOpens = 0.4f;

    // The spear has no swing; this is what one would deal.
    private const int SpearSwingDamage = 24;

    // Hit times are the blade's peak speed in the KayKit clips: Melee_2H_Attack_Slice 0.38 s, Melee_1H_Attack_Chop 0.60 s.
    // Ranges are how far the blade tip reaches from the body centre at the hit moment, with the real blade lengths from
    // the weapon meshes (sword_2handed 1.96, Skeleton_Blade 1.17, Skeleton_Axe 1.0), so the hit area ends where the
    // drawn blade does. net-test measures the live tip at every hit test against these. Slice reaches 2.02 in the
    // standing clip but about 1.86 in play, where it often runs on the upper body while moving and twisting.
    public static readonly SkillDefinition Slice = new("slice", Damage: 20, Range: 1.9f, HalfArc: 70f * Angles.DegToRad, HitTime: 0.38f)
    {
        Name = "Slice",
    };

    // Blade tip at a constant 2.28 reach at waist height.
    public static readonly SkillDefinition Spin = SpinOf("spin", damage: 10, range: 2.3f, manaCost: 4);

    // First pass for the three weapons after the sword: the staff is cheap to spin, the spear reaches furthest with
    // the strongest single hit on a narrow line, and the scythe sweeps the widest arc and spins hardest at a higher
    // cost. Hit times are measured with ./dev.sh swing-survey: a swing lands when the weapon moves fastest, the thrust
    // at full extension, since its fastest moment is the wind-up. Ranges follow play, as the sword's does: the weapon's
    // farthest point at the hit, which net-test's reach-check measures live. Swings that lunge through the hips reach
    // less while running, when the legs clip keeps the hips: the staff's Chop 2.07 standing, 1.55 in play (medians of
    // 1.36 to 1.70 across runs); the scythe's Slice 1.98 standing, 1.8 in play; the thrust 2.78 standing, 2.7 in play.
    public static readonly SkillDefinition StaffHit = new("staff-hit", Damage: 16, Range: 1.55f, HalfArc: 45f * Angles.DegToRad, HitTime: 0.825f)
    {
        Name = "Hit",
    };

    public static readonly SkillDefinition StaffSpin = SpinOf("staff-spin", damage: 8, range: 1.95f, manaCost: 3);

    public static readonly SkillDefinition SpearThrust = new("spear-thrust", Damage: SpearSwingDamage * ThrustDamageFactor, Range: 2.7f, HalfArc: ThrustHalfArc, HitTime: StabExtended)
    {
        Name = "Thrust",
    };

    public static readonly SkillDefinition SpearSpin = SpinOf("spear-spin", damage: 10, range: 2.4f, manaCost: 4);

    public static readonly SkillDefinition ScytheSwing = new("scythe-swing", Damage: 18, Range: 1.8f, HalfArc: 90f * Angles.DegToRad, HitTime: 0.40f)
    {
        Name = "Swing",
    };

    public static readonly SkillDefinition ScytheSpin = SpinOf("scythe-spin", damage: 12, range: 2.1f, manaCost: 5);

    // Lunges: a dash with the attack button turns the swing into a thrust thrown on the move, whatever the weapon.
    // The stab plays so that it reaches full extension as the dash ends (CombatTiming.LungeSpeed), with its hit window
    // open from the moment the point starts forward, so whatever the dash carries the point into is run through once.
    // Ranges follow play, as the swings' do: each weapon's reach as the window closes, which net-test's lunge-check
    // measures live. The dash clip leans the hips into the stab, so a lunge reaches further than the survey's
    // hips-at-rest figure (greatsword 2.38, staff 2.05, spear 2.52, scythe 2.17).
    public static readonly SkillDefinition GreatswordLunge = LungeOf("greatsword-lunge", Slice.Damage * ThrustDamageFactor, range: 2.6f);

    public static readonly SkillDefinition StaffLunge = LungeOf("staff-lunge", StaffHit.Damage * ThrustDamageFactor, range: 2.25f);

    public static readonly SkillDefinition SpearLunge = LungeOf("spear-lunge", SpearThrust.Damage, range: 2.75f);

    public static readonly SkillDefinition ScytheLunge = LungeOf("scythe-lunge", ScytheSwing.Damage * ThrustDamageFactor, range: 2.5f);

    public static readonly SkillDefinition MinionChop = new("minion-chop", Damage: 6, Range: 1.65f, HalfArc: 45f * Angles.DegToRad, HitTime: 0.60f)
    {
        Name = "Chop",
    };

    public static readonly SkillDefinition WarriorChop = new("warrior-chop", Damage: 12, Range: 1.5f, HalfArc: 45f * Angles.DegToRad, HitTime: 0.60f)
    {
        Name = "Chop",
    };

    // Ranged_Bow_Draw (1.333 s) then Ranged_Bow_Release; the arrow leaves as the string hand snaps back early in the
    // release (./dev.sh swing-survey). HalfArc is unused: the arrow decides the hit.
    public static readonly SkillDefinition ArcherShot = new("archer-shot", Damage: 8, Range: 10f, HalfArc: 5f * Angles.DegToRad, HitTime: 1.40f)
    {
        Name = "Shot",
        Projectile = Projectiles.Arrow,
    };

    private static SkillDefinition LungeOf(string id, int damage, float range) =>
        new(id, damage, range, ThrustHalfArc, HitTime: StabExtended * LungeOpens)
        {
            Name = "Lunge",
            SweepEnd = StabExtended,
        };

    private static SkillDefinition SpinOf(string id, int damage, float range, int manaCost) =>
        new(id, damage, range, HalfArc: MathF.PI, HitTime: 0f)
        {
            Name = "Spin",
            SweepEnd = SpinRevolution,
            ManaCost = manaCost,
            MoveSpeedFactor = SpinMoveSpeedFactor,
            Channeled = true,
        };
}
