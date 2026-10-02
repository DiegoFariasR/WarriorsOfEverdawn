using System;
using System.Collections.Generic;
using Godot;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// Clips shared by every KayKit Rig_Medium character. The rig GLBs are imported as animation libraries.
public static class RigAnimations
{
    public const string SkeletonPath = "Rig_Medium/Skeleton3D";

    // KayKit clips feel slow at their authored speed. Idle, spawn, hit and death clips play this much faster;
    // swings play at the character's attack speed (Core CombatTiming / StatRules), and legs at whatever rate matches
    // the ground speed.
    public const float PlaybackSpeed = 2f;

    public const string Idle = "melee/Melee_2H_Idle";
    public const string UnarmedIdle = "melee/Melee_Unarmed_Idle";
    public const string Run = "move/Running_A";
    public const string StrafeLeft = "moveadv/Running_Strafe_Left";
    public const string StrafeRight = "moveadv/Running_Strafe_Right";
    public const string Backpedal = "moveadv/Walking_Backwards";
    public const string PlayerDeath = "general/Death_A";
    public const string HitReact = "general/Hit_A";
    public const string SpinLoop = "melee/Melee_2H_Attack_Spinning";
    public const string Stab = "melee/Melee_2H_Attack_Stab";
    public const string OneHandedStab = "melee/Melee_1H_Attack_Stab";
    public const string Guard = "melee/Melee_Blocking";
    public const string DashForward = "moveadv/Dodge_Forward";
    public const string DashBackward = "moveadv/Dodge_Backward";
    public const string DashLeft = "moveadv/Dodge_Left";
    public const string DashRight = "moveadv/Dodge_Right";

    public const string SkeletonIdle = "special/Skeletons_Idle";
    public const string SkeletonWalk = "special/Skeletons_Walking";
    public const string SkeletonSpawn = "special/Skeletons_Spawn_Ground";
    public const string SkeletonDeath = "special/Skeletons_Death";

    public const string BowDraw = "ranged/Ranged_Bow_Draw";
    public const string BowRelease = "ranged/Ranged_Bow_Release";

    public const string MagicShoot = "ranged/Ranged_Magic_Shoot";
    public const string MagicChannel = "ranged/Ranged_Magic_Spellcasting";

    private static readonly (string Name, string Path)[] Libraries =
    {
        ("move", "res://assets/animations/Rig_Medium_MovementBasic.glb"),
        ("moveadv", "res://assets/animations/Rig_Medium_MovementAdvanced.glb"),
        ("melee", "res://assets/animations/Rig_Medium_CombatMelee.glb"),
        ("general", "res://assets/animations/Rig_Medium_General.glb"),
        ("special", "res://assets/animations/Rig_Medium_Special.glb"),
        ("ranged", "res://assets/animations/Rig_Medium_CombatRanged.glb"),
    };

    private static readonly string[] LoopingClips = { Idle, UnarmedIdle, Run, StrafeLeft, StrafeRight, Backpedal, SkeletonIdle, SkeletonWalk, SpinLoop, Guard, MagicChannel };

    // A clip ("library/name", as the mixers name them) straight from its library, for reading its tracks.
    public static Animation Load(string clip)
    {
        int slash = clip.IndexOf('/');
        string library = slash > 0 ? clip[..slash] : throw new ArgumentException($"Clip '{clip}' has no library prefix");
        foreach (var (name, path) in Libraries)
        {
            if (name == library)
            {
                return Assets.Load<AnimationLibrary>(path).GetAnimation(clip[(slash + 1)..])
                    ?? throw new KeyNotFoundException($"No clip '{clip}' in {path}");
            }
        }

        throw new KeyNotFoundException($"No animation library '{library}' for clip '{clip}'");
    }

    public static void AddTo(AnimationMixer mixer)
    {
        foreach (var (name, path) in Libraries)
        {
            mixer.AddAnimationLibrary(name, Assets.Load<AnimationLibrary>(path));
        }

        // The GLBs import every clip as one-shot. The libraries are shared resources, so this runs once per clip.
        foreach (var clip in LoopingClips)
        {
            mixer.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Linear;
        }
    }
}
