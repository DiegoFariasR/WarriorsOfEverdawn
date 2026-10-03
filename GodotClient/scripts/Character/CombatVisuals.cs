using System.Collections.Generic;
using System.Linq;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Character;

// Presentation for Core's weapons, skills and enemies. Skill hit times in Core are measured from these clips
// (./dev.sh swing-survey).
public static class CombatVisuals
{
    // In the chest bone's space (+Y up the spine, -Z out of the back). The greatsword takes Everdawn's sheath pose out
    // of battle (CharacterAssembler.SheathHalfPosition / SheathHalfRotation): hilt behind a shoulder, blade running
    // diagonally down the back. Poles are carried by their middle, point up, leaning back off the head (-10 deg) and
    // across the back; the 3.1 spear leans furthest so its butt stays off the ground.
    private static readonly Vector3 SheathPosition = new(0.20f, 0.55f, -0.36f);
    private static readonly Vector3 SheathRotation = new(Mathf.DegToRad(190f), 0f, Mathf.DegToRad(30f));
    private static readonly Vector3 PolePosition = new(0f, 0.25f, -0.36f);

    private static readonly Vector3 ClawInHand = new(Mathf.DegToRad(-90f), Mathf.Pi, 0f);
    private static readonly Vector3 ClawForward = new(0f, 0.05f, 0.05f);

    // Drawn bigger than modelled: at its own size the blades are lost under this camera.
    private const float ClawSize = 1.3f;

    // The claw is modelled lying flat; laying a weapon down turns it a quarter over, which this turns back.
    private static readonly Vector3 ClawOnGround = new(Mathf.DegToRad(-90f), 0f, 0f);

    // The arrow a bow looses, a skeleton's or a player's, and the one a player with a bow holds in the right hand.
    public const string ArrowModel = "res://assets/props/Skeleton_Arrow.glb";

    // Everdawn's shield in the hand (item_visuals.json "basic-shield").
    private static readonly Vector3 ShieldInHand = new(0f, 0f, 0.2f);

    // Every staff is the same staff with its element alight at its head, a ball of it big enough to take in the
    // model's own green gem and the claw that holds it (0.7 to 1.25 up the model, 0.3 out); like any pole it is
    // carried across the back.
    private static readonly WeaponLook Staff = new("res://assets/weapons/staff.glb", Vector3.Zero)
    {
        GlowAt = new Vector3(0.03f, 1f, 0f),
        BackGrip = new Vector3(0f, 0.2f, 0f),
        BackPosition = PolePosition,
        BackRotation = new Vector3(Mathf.DegToRad(-10f), 0f, Mathf.DegToRad(30f)),
    };

    // Every wand is the same wand and open book, a spark of its element at the wand's tip and the element playing
    // over both. One weapon to the rules, as the sword and shield are: the wand in the right hand, the book in the
    // left, and on the back the book flat between the shoulders with the wand across it.
    private static readonly WeaponLook Wand = new("res://assets/weapons/wand.glb", Vector3.Zero)
    {
        OneHanded = true,
        GlowAt = new Vector3(0f, 0.66f, 0f),
        GlowRadius = 0.11f,
        BackPosition = new Vector3(0.12f, 0.42f, -0.36f),
        BackRotation = SheathRotation,
        OffHand = new OffHandLook("res://assets/weapons/spellbook_open.glb")
        {
            HandPosition = new Vector3(0f, 0.05f, 0.12f),
            HandRotation = new Vector3(Mathf.DegToRad(-60f), 0f, 0f),
            BackPosition = new Vector3(0f, 0.3f, -0.3f),
            BackRotation = new Vector3(0f, Mathf.Pi, 0f),
        },
    };

    private static readonly Dictionary<string, WeaponLook> LookByWeapon = new()
    {
        [Weapons.Greatsword.Id] = new("res://assets/weapons/sword_2handed.glb", Vector3.Zero)
        {
            BackPosition = SheathPosition,
            BackRotation = SheathRotation,
        },

        // staff_A is modelled around its middle. A quarterstaff is held a quarter of the way from one end, which puts
        // the far end 1.61 from the hand instead of 1.07.
        [Weapons.Quarterstaff.Id] = new("res://assets/weapons/staff_A.glb", new Vector3(0f, -0.537f, 0f))
        {
            BackGrip = Vector3.Zero,
            BackPosition = PolePosition,
            BackRotation = new Vector3(Mathf.DegToRad(-10f), 0f, Mathf.DegToRad(35f)),
        },
        [Weapons.Spear.Id] = new("res://assets/weapons/spear_A.glb", Vector3.Zero)
        {
            BackGrip = new Vector3(0f, 0.54f, 0f),
            BackPosition = PolePosition,
            BackRotation = new Vector3(Mathf.DegToRad(-10f), 0f, Mathf.DegToRad(-50f)),
        },

        // A war scythe for both hands: the model is a short one, so it is drawn bigger, and held a little below its
        // origin so both hands sit on the shaft with some to spare beneath them. The model's blade sticks out to -X,
        // which in the hand trails behind every swing and points back at the wielder in the stance (swing-survey's
        // head_leads read -0.84); half a turn about the shaft puts it in front.
        [Weapons.Scythe.Id] = new("res://assets/weapons/scythe.glb", new Vector3(0f, -0.3f, 0f))
        {
            Scale = 1.35f,
            HandTurn = Mathf.Pi,
            BackGrip = new Vector3(0f, 0.23f, 0f),
            BackPosition = PolePosition,
            BackRotation = new Vector3(Mathf.DegToRad(-10f), 0f, Mathf.DegToRad(30f)),
        },

        // One weapon to the rules, two models to the eye: the sword in the right hand and the shield on the left
        // arm. On the back the shield hangs flat between the shoulders with the sword sheathed across it.
        [Weapons.SwordAndShield.Id] = new("res://assets/weapons/sword_1handed.glb", Vector3.Zero)
        {
            OneHanded = true,
            BackPosition = SheathPosition,
            BackRotation = SheathRotation,
            OffHand = new OffHandLook("res://assets/weapons/shield_badge_color.glb")
            {
                HandPosition = ShieldInHand,
                BackPosition = new Vector3(0f, 0.3f, -0.3f),
                BackRotation = new Vector3(0f, Mathf.Pi, 0f),
            },
        },

        // One weapon to the rules, two models to the eye, as the sword and shield are: the bow in the left hand, held
        // as the skeleton archer's is (Everdawn's item_visuals "basic-bow": bow_withString turned half round about
        // Z), and an arrow in the right, turned end for end since the model's point is its -Y end. On the back the
        // bow hangs across the shoulders with the arrow under it. The arrow is held a third of the way from its nock,
        // so its point is out in front of the fist for the stab a dash carries.
        [Weapons.Bow.Id] = new(ArrowModel, new Vector3(0f, 0.25f, 0f))
        {
            OneHanded = true,
            HandRotation = new Vector3(Mathf.Pi, 0f, 0f),
            BackPosition = new Vector3(0.12f, 0.42f, -0.36f),
            BackRotation = SheathRotation,
            OffHand = new OffHandLook("res://assets/weapons/bow_withString.glb")
            {
                HandRotation = new Vector3(0f, 0f, Mathf.Pi),

                // The model's length is its Z: slanted across the back, its curve standing off it, and flat on
                // the ground.
                BackPosition = new Vector3(0f, 0.35f, -0.3f),
                BackRotation = new Vector3(Mathf.DegToRad(-55f), Mathf.Pi / 2f, 0f),
                GroundRotation = new Vector3(Mathf.Pi / 2f, 0f, 0f),
            },
        },

        // KayKit's biggest hammer (hammer_D), held at its origin, where the haft has a hand's length and more
        // below it for the second hand. Carried across the back like any pole, head up.
        [Weapons.Warhammer.Id] = new("res://assets/weapons/hammer_D.glb", Vector3.Zero)
        {
            BackGrip = new Vector3(0f, 0.36f, 0f),
            BackPosition = PolePosition,
            BackRotation = new Vector3(Mathf.DegToRad(-10f), 0f, Mathf.DegToRad(30f)),
        },

        // A pair, and one weapon: the same clawed fist weapon (KayKit's fistweapon_B: a grip with three blades) in
        // each hand, turned as Everdawn turns its fist gloves (item_visuals "basic-fist-gloves") so the blades
        // stand out of the knuckles, and set a little forward of the palm. On the back they hang side by side at
        // the shoulders.
        [Weapons.Claws.Id] = new("res://assets/weapons/fistweapon_B.glb", Vector3.Zero)
        {
            OneHanded = true,
            Scale = ClawSize,
            HandRotation = ClawInHand,
            HandPosition = ClawForward,
            BackPosition = new Vector3(0.16f, 0.35f, -0.3f),
            GroundRotation = ClawOnGround,
            OffHand = new OffHandLook("res://assets/weapons/fistweapon_B.glb")
            {
                Scale = ClawSize,
                HandRotation = ClawInHand,
                HandPosition = ClawForward,
                BackPosition = new Vector3(-0.16f, 0.35f, -0.3f),
                GroundRotation = ClawOnGround,
                GroundBeside = 0.45f,
            },
        },
    };

    private static readonly Dictionary<string, string> ClipBySkill = new()
    {
        [Skills.HammerSmash.Id] = "melee/Melee_2H_Attack_Chop",
        [Skills.HammerSpin.Id] = RigAnimations.SpinLoop,
        [Skills.HammerLunge.Id] = RigAnimations.Stab,
        [Skills.ClawRake.Id] = RigAnimations.DualSlice,
        [Skills.ClawSpin.Id] = RigAnimations.SpinLoop,
        [Skills.ClawLunge.Id] = RigAnimations.Punch,
        [Skills.BowShot.Id] = RigAnimations.BowRelease,
        [Skills.BowVolley.Id] = RigAnimations.BowRelease,
        [Skills.BowLunge.Id] = RigAnimations.OneHandedStab,
        [Skills.SwordSlash.Id] = "melee/Melee_1H_Attack_Slice_Diagonal",
        [Skills.SwordSpin.Id] = RigAnimations.SpinLoop,
        [Skills.SwordLunge.Id] = RigAnimations.OneHandedStab,
        [Skills.Slice.Id] = "melee/Melee_2H_Attack_Slice",
        [Skills.Spin.Id] = RigAnimations.SpinLoop,
        [Skills.StaffHit.Id] = "melee/Melee_2H_Attack_Chop",
        [Skills.StaffSpin.Id] = RigAnimations.SpinLoop,
        [Skills.SpearThrust.Id] = RigAnimations.Stab,
        [Skills.SpearSpin.Id] = RigAnimations.SpinLoop,
        [Skills.ScytheSwing.Id] = "melee/Melee_2H_Attack_Slice",
        [Skills.ScytheSpin.Id] = RigAnimations.SpinLoop,
        [Skills.GreatswordLunge.Id] = RigAnimations.Stab,
        [Skills.StaffLunge.Id] = RigAnimations.Stab,
        [Skills.SpearLunge.Id] = RigAnimations.Stab,
        [Skills.ScytheLunge.Id] = RigAnimations.Stab,
        [Skills.MinionChop.Id] = "melee/Melee_1H_Attack_Chop",
        [Skills.WarriorChop.Id] = "melee/Melee_1H_Attack_Chop",
        [Skills.ArcherShot.Id] = RigAnimations.BowDraw,
    };

    // A second clip played straight after the skill's clip; its hit time counts across both.
    private static readonly Dictionary<string, string> FollowUpBySkill = new()
    {
        [Skills.ArcherShot.Id] = RigAnimations.BowRelease,
    };

    private static readonly Dictionary<string, EnemyLook> LookByEnemy = new()
    {
        [Enemies.SkeletonMinion.Id] = new(Skeleton("SkeletonMinion", "Cloak"), "res://assets/weapons/Skeleton_Blade.glb"),
        [Enemies.SkeletonWarrior.Id] = new(Skeleton("SkeletonWarrior", "Helmet", "Cloak"), "res://assets/weapons/Skeleton_Axe.glb"),

        // Everdawn's bow (item_visuals.json "basic-bow"): bow_withString, left hand, turned 180 deg about Z.
        [Enemies.SkeletonArcher.Id] = new(Skeleton("SkeletonRogue", "Hood", "Cape"), "res://assets/weapons/bow_withString.glb")
        {
            LeftHand = true,
            WeaponRotation = new Vector3(0f, 0f, Mathf.Pi),
        },
    };

    public static WeaponLook LookFor(WeaponDefinition weapon) =>
        weapon.Element is { } element ? (Weapons.IsWand(weapon) ? Wand with { Glow = element, Enchant = element } : Staff with { Glow = element })
        : LookByWeapon.TryGetValue(weapon.Kind, out var look) ? look with { Enchant = weapon.Enchantment }
        : throw new KeyNotFoundException($"No model for weapon '{weapon.Kind}'");

    // A spell is cast with its own clips whatever its element: one to throw, one looped to hold a spell on an area.
    // An arrow is no spell, whatever is on the bow.
    public static string ClipFor(SkillDefinition skill) =>
        skill.Projectile != null && DamageTypes.FamilyOf(skill.Type) != DamageFamily.Physical ? RigAnimations.MagicShoot
        : skill.Area != null ? RigAnimations.MagicChannel
        : ClipBySkill.TryGetValue(skill.Id, out var clip) ? clip
        : Weapons.Staffs.Any(s => s.Lunge.Id == skill.Id) ? RigAnimations.Stab
        : Weapons.Wands.Any(w => w.Lunge.Id == skill.Id) ? RigAnimations.OneHandedStab
        : throw new KeyNotFoundException($"No clip for skill '{skill.Id}'");

    // Clips that turn the root bone (the Spin loop turns the whole body) cannot be layered on the upper body.
    public static bool IsFullBody(SkillDefinition skill) => ClipFor(skill) == RigAnimations.SpinLoop;

    public static string? FollowUpFor(SkillDefinition skill) => FollowUpBySkill.GetValueOrDefault(skill.Id);

    public static EnemyLook LookFor(EnemyDefinition enemy) =>
        LookByEnemy.TryGetValue(enemy.Id, out var look) ? look : throw new KeyNotFoundException($"No model for enemy '{enemy.Id}'");

    // A skeleton as KayKit made it: its skull with its eyes and jaw, which are models of their own.
    private static CharacterLook Skeleton(string origin, params string[] accessories) =>
        CharacterLook.Of(origin, accessories) with { Face = $"{origin}_Face" };
}

// An enemy's figure and what it holds, in the right hand unless LeftHand; WeaponRotation turns it in the hand.
public sealed record EnemyLook(CharacterLook Figure, string Weapon)
{
    public bool LeftHand { get; init; }

    public Vector3 WeaponRotation { get; init; }
}

// A weapon's model and the point on it (in its own space) the hand holds (CharacterRig.HoldWeapon). Carried on the
// back (CharacterRig.HoldOnBack), BackGrip is the point on it that goes to BackPosition, turned by BackRotation, all in
// the chest bone's space. Scale draws it bigger or smaller than modelled, wherever it is; HandTurn turns it about its
// own length in the hand, for a head that sticks out to one side; HandRotation and HandPosition turn and move it in
// the hand after that, for a model that is not made standing up from its grip. OneHanded leaves the other hand off
// the weapon;
// OffHand is what that hand holds instead, which goes wherever the weapon goes. Enchant and Glow light an element on
// it.
public sealed record WeaponLook(string Model, Vector3 Grip)
{
    public bool OneHanded { get; init; }

    public WeaponStance Stance => OneHanded ? WeaponStance.OneHanded : WeaponStance.TwoHanded;

    public OffHandLook? OffHand { get; init; }

    // An enchanted weapon: this element plays over the whole of it, and of what its other hand holds.
    public Element? Enchant { get; init; }

    // A magic staff: its element burns at this point on the model, in the model's own space.
    public Element? Glow { get; init; }

    public Vector3 GlowAt { get; init; }

    public float GlowRadius { get; init; } = 0.3f;

    public float Scale { get; init; } = 1f;

    public float HandTurn { get; init; }

    public Vector3 HandRotation { get; init; }

    public Vector3 HandPosition { get; init; }

    public Vector3 BackGrip { get; init; }

    public Vector3 BackPosition { get; init; }

    public Vector3 BackRotation { get; init; }

    // Turns it where it lies on the ground, for a model that is not made standing up: a weapon is laid down by
    // turning it a quarter over.
    public Vector3 GroundRotation { get; init; }
}

// The piece a weapon puts in the left hand (a shield): where it sits in the hand slot, and where it hangs when the
// weapon is on the back, in the chest bone's space.
public sealed record OffHandLook(string Model)
{
    public float Scale { get; init; } = 1f;

    public Vector3 HandPosition { get; init; }

    public Vector3 HandRotation { get; init; }

    public Vector3 BackPosition { get; init; }

    public Vector3 BackRotation { get; init; }

    // How it lies beside the weapon on the ground, in the laid weapon's space: face up for a shield, whose face is
    // its +Z, which laying the weapon down turns to the ground.
    public Vector3 GroundRotation { get; init; } = new(0f, Mathf.Pi, 0f);

    // How far beside the weapon it lies.
    public float GroundBeside { get; init; } = 0.8f;
}
