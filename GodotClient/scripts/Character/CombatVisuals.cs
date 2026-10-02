using System.Collections.Generic;
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
    };

    private static readonly Dictionary<string, string> ClipBySkill = new()
    {
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
        [Enemies.SkeletonMinion.Id] = new("res://assets/characters/Skeleton_Minion.glb", "res://assets/weapons/Skeleton_Blade.glb"),
        [Enemies.SkeletonWarrior.Id] = new("res://assets/characters/Skeleton_Warrior.glb", "res://assets/weapons/Skeleton_Axe.glb"),

        // Everdawn's bow (item_visuals.json "basic-bow"): bow_withString, left hand, turned 180 deg about Z.
        [Enemies.SkeletonArcher.Id] = new("res://assets/characters/Skeleton_Rogue.glb", "res://assets/weapons/bow_withString.glb")
        {
            LeftHand = true,
            WeaponRotation = new Vector3(0f, 0f, Mathf.Pi),
        },
    };

    public static WeaponLook LookFor(WeaponDefinition weapon) =>
        LookByWeapon.TryGetValue(weapon.Kind, out var look) ? look : throw new KeyNotFoundException($"No model for weapon '{weapon.Kind}'");

    public static string ClipFor(SkillDefinition skill) =>
        ClipBySkill.TryGetValue(skill.Id, out var clip) ? clip : throw new KeyNotFoundException($"No clip for skill '{skill.Id}'");

    // Clips that turn the root bone (the Spin loop turns the whole body) cannot be layered on the upper body.
    public static bool IsFullBody(SkillDefinition skill) => ClipFor(skill) == RigAnimations.SpinLoop;

    public static string? FollowUpFor(SkillDefinition skill) => FollowUpBySkill.GetValueOrDefault(skill.Id);

    public static EnemyLook LookFor(EnemyDefinition enemy) =>
        LookByEnemy.TryGetValue(enemy.Id, out var look) ? look : throw new KeyNotFoundException($"No model for enemy '{enemy.Id}'");
}

// An enemy's model and what it holds, in the right hand unless LeftHand; WeaponRotation turns it in the hand.
public sealed record EnemyLook(string Model, string Weapon)
{
    public bool LeftHand { get; init; }

    public Vector3 WeaponRotation { get; init; }
}

// A weapon's model and the point on it (in its own space) the hand holds (CharacterRig.HoldWeapon). Carried on the
// back (CharacterRig.HoldOnBack), BackGrip is the point on it that goes to BackPosition, turned by BackRotation, all in
// the chest bone's space. Scale draws it bigger or smaller than modelled, wherever it is; HandTurn turns it about its
// own length in the hand, for a head that sticks out to one side.
public sealed record WeaponLook(string Model, Vector3 Grip)
{
    public float Scale { get; init; } = 1f;

    public float HandTurn { get; init; }

    public Vector3 BackGrip { get; init; }

    public Vector3 BackPosition { get; init; }

    public Vector3 BackRotation { get; init; }
}
