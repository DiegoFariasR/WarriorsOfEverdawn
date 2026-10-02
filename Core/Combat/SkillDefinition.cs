using System;
using System.Collections.Generic;
using System.Linq;

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

    // How many it looses, one every VolleyInterval of clip time from HitTime; each deals Damage.
    public int Projectiles { get; init; } = 1;

    public float VolleyInterval { get; init; }

    // What a thrown skill bursts into where it ends, be that a body, a wall or the end of its flight: everything
    // within this of the spot takes its damage, the body it struck like the rest and no more. 0 for one that hits
    // only what it touches.
    public float BlastRadius { get; init; }

    // A spell lands on this instead of in an arc from the caster; Range is then how far its far edge reaches.
    public AreaDefinition? Area { get; init; }

    // The magic it is made of; none for a blow with a plain weapon. Magic grows with WIS where a blow grows with STR.
    public Element? Element { get; init; }

    // Of a skill with an element, the share of its damage that is that magic: all of a spell's, part of a blow's
    // with an enchanted weapon.
    public float MagicShare { get; init; } = 1f;
}

// What a magic staff casts: its bolt or volley, the spell held on an area, and the thrust its dash carries.
public sealed record StaffSkills(SkillDefinition Primary, SkillDefinition Channel, SkillDefinition Lunge);

// What a wand and its book cast: the same bolt or volley as the staff of their element, a ball that bursts, and the
// thrust a dash carries.
public sealed record WandSkills(SkillDefinition Primary, SkillDefinition Burst, SkillDefinition Lunge);

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

    // Melee_2H_Attack_Stab, the clip a thrust with both hands plays, at full extension (./dev.sh swing-survey).
    private const float StabExtended = 0.764f;

    // Melee_1H_Attack_Stab, the one-handed thrust, at full extension.
    private const float OneHandedStabExtended = 0.476f;

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
    // 1.36 to 1.70 across runs); the scythe's Slice 2.78 standing, 2.7 in play; the thrust 2.78 standing, 2.7 in play.
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

    public static readonly SkillDefinition ScytheSwing = new("scythe-swing", Damage: 18, Range: 2.7f, HalfArc: 90f * Angles.DegToRad, HitTime: 0.40f)
    {
        Name = "Swing",
    };

    public static readonly SkillDefinition ScytheSpin = SpinOf("scythe-spin", damage: 12, range: 3.1f, manaCost: 5);

    // Lunges: a dash with the attack button turns the swing into a thrust thrown on the move, whatever the weapon.
    // The stab plays so that it reaches full extension as the dash ends (CombatTiming.LungeSpeed), with its hit window
    // open from the moment the point starts forward, so whatever the dash carries the point into is run through once.
    // Ranges follow play, as the swings' do: each weapon's reach as the window closes, which net-test's lunge-check
    // measures live. The dash clip leans the hips into the stab, so the straight weapons reach further in a lunge than
    // the survey's hips-at-rest figure (greatsword 2.38, staff 2.05, spear 2.52); the scythe's hooked blade reaches
    // the same either way (2.77).
    public static readonly SkillDefinition GreatswordLunge = LungeOf("greatsword-lunge", Slice.Damage * ThrustDamageFactor, range: 2.6f);

    public static readonly SkillDefinition StaffLunge = LungeOf("staff-lunge", StaffHit.Damage * ThrustDamageFactor, range: 2.25f);

    public static readonly SkillDefinition SpearLunge = LungeOf("spear-lunge", SpearThrust.Damage, range: 2.75f);

    public static readonly SkillDefinition ScytheLunge = LungeOf("scythe-lunge", ScytheSwing.Damage * ThrustDamageFactor, range: 2.8f);

    // The sword of a sword and shield: one-handed and short, so it hits least, in exchange for the shield's guard.
    // Measured as the others are: the slash lands when the blade moves fastest (Melee_1H_Attack_Slice_Diagonal,
    // 0.417 s), reaching 2.04 standing and about 1.7 in play (medians of 1.53 to 1.85 across runs, by how much of
    // a session is fought on the move); the lunge closes at the one-handed stab's full extension, where the blade
    // reaches 2.3 in a dash.
    public static readonly SkillDefinition SwordSlash = new("sword-slash", Damage: 14, Range: 1.7f, HalfArc: 60f * Angles.DegToRad, HitTime: 0.417f)
    {
        Name = "Slash",
    };

    public static readonly SkillDefinition SwordSpin = SpinOf("sword-spin", damage: 7, range: 1.75f, manaCost: 3);

    public static readonly SkillDefinition SwordLunge = LungeOf("sword-lunge", SwordSlash.Damage * ThrustDamageFactor, range: 2.25f, OneHandedStabExtended);

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

    // Staffs, first pass, after Everdawn's spells. Fire, earth and divine throw one Bolt; water, wind and void a
    // Volley of three darts of a third the damage each, loosed 0.12 s of clip time apart. Every staff holds a spell
    // on an area: its damage lands on everything in the area once a cycle, a cycle being one loop of the casting
    // clip (Ranged_Magic_Spellcasting, 0.667 s), paid for in mana as it starts, as a Spin's revolution is. The area
    // lies ahead of the caster, but for the Divine Nova, which bursts round it.
    private const int BoltDamage = 30;

    // Between one throw and the next. With none, a bolt out-dealt every blade from twelve times its reach.
    private const float ThrowCooldown = 1f;
    private const int DartsInVolley = 3;
    private const float VolleyGap = 0.12f;

    // Ranged_Magic_Shoot, the clip a bolt or volley is thrown with: the staff is furthest forward here
    // (./dev.sh swing-survey), and the bolt leaves it.
    private const float CastReleased = 0.3f;

    private const float CastingLoop = 0.6667f;
    private const float ChannelMoveSpeedFactor = 0.5f;
    private const float AreaAhead = 4f;

    // A poke with the butt of the staff: the least of the lunges.
    private const int StaffPokeDamage = 24;
    private const float StaffPokeRange = 2.1f;

    private static readonly Dictionary<Element, StaffSkills> Staffs = new()
    {
        [Element.Fire] = StaffOf(Element.Fire, volley: false, "inferno", "Inferno", damage: 10, manaCost: 6, new AreaDefinition(AreaAhead, Radius: 2.5f)),
        [Element.Water] = StaffOf(Element.Water, volley: true, "blizzard", "Blizzard", damage: 6, manaCost: 5, new AreaDefinition(AreaAhead, Radius: 3.5f)),
        [Element.Wind] = StaffOf(Element.Wind, volley: true, "storm", "Lightning Storm", damage: 8, manaCost: 5, new AreaDefinition(AreaAhead, Radius: 3f)),
        [Element.Earth] = StaffOf(Element.Earth, volley: false, "quake", "Earthquake", damage: 8, manaCost: 5, new AreaDefinition(AreaAhead, Radius: 3f)),
        [Element.Divine] = StaffOf(Element.Divine, volley: false, "nova", "Divine Nova", damage: 8, manaCost: 5, new AreaDefinition(Distance: 0f, Radius: 3.5f)),
        [Element.Void] = StaffOf(Element.Void, volley: true, "corrosion", "Void Corrosion", damage: 7, manaCost: 4, new AreaDefinition(AreaAhead, Radius: 3f)),
    };

    public static StaffSkills StaffOf(Element element) => Staffs[element];

    // Wands, first pass. The secondary is not held: one cast throws one ball, which bursts where it ends and deals
    // its damage to everything within the burst. It costs mana by the cast and cannot be thrown again at once.
    // Thrown with the same clip as a bolt. The same numbers for every element: what an element's burst does of its
    // own is not decided.
    private const int BurstDamage = 25;
    private const float BurstRadius = 2.5f;
    private const int BurstManaCost = 12;
    private const float BurstCooldown = 2.5f;

    // A jab with the wand: it is short, so this reaches least of all.
    private const int WandPokeDamage = 16;
    private const float WandPokeRange = 1.65f;

    private static readonly Dictionary<Element, WandSkills> Wands = Elements.All.ToDictionary(element => element, WandOf);

    public static WandSkills WandFor(Element element) => Wands[element];

    private static WandSkills WandOf(Element element)
    {
        string id = Elements.IdOf(element);
        var burst = new SkillDefinition($"{id}-burst", BurstDamage, Projectiles.Ball.MaxDistance, ThrustHalfArc, CastReleased)
        {
            Name = element == Element.Fire ? "Fireball" : $"{element} Burst",
            Projectile = Projectiles.Ball,
            BlastRadius = BurstRadius,
            ManaCost = BurstManaCost,
            Cooldown = BurstCooldown,
            Element = element,
        };
        return new WandSkills(Staffs[element].Primary, burst, LungeOf($"{id}-wand-lunge", WandPokeDamage, WandPokeRange, OneHandedStabExtended));
    }

    private static StaffSkills StaffOf(Element element, bool volley, string channelId, string channelName, int damage, int manaCost, AreaDefinition area)
    {
        string id = Elements.IdOf(element);
        var projectile = volley ? Projectiles.Dart : Projectiles.Bolt;
        int darts = volley ? DartsInVolley : 1;
        string thrown = volley ? "Volley" : "Bolt";
        var primary = new SkillDefinition($"{id}-{thrown.ToLowerInvariant()}", BoltDamage / darts, projectile.MaxDistance, ThrustHalfArc, CastReleased)
        {
            Name = $"{element} {thrown}",
            Projectile = projectile,
            Projectiles = darts,
            Cooldown = ThrowCooldown,
            VolleyInterval = volley ? VolleyGap : 0f,
            SweepEnd = volley ? CastReleased + (darts - 1) * VolleyGap : null,
            Element = element,
        };
        var channel = new SkillDefinition($"{id}-{channelId}", damage, area.Reach, HalfArc: MathF.PI, HitTime: 0f)
        {
            Name = channelName,
            SweepEnd = CastingLoop,
            ManaCost = manaCost,
            MoveSpeedFactor = ChannelMoveSpeedFactor,
            Channeled = true,
            Area = area,
            Element = element,
        };
        return new StaffSkills(primary, channel, LungeOf($"{id}-staff-lunge", StaffPokeDamage, StaffPokeRange));
    }

    private static SkillDefinition LungeOf(string id, int damage, float range, float extended = StabExtended) =>
        new(id, damage, range, ThrustHalfArc, HitTime: extended * LungeOpens)
        {
            Name = "Lunge",
            SweepEnd = extended,
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
