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
    public const string Run = "move/Running_A";
    public const string StrafeLeft = "moveadv/Running_Strafe_Left";
    public const string StrafeRight = "moveadv/Running_Strafe_Right";
    public const string Backpedal = "moveadv/Walking_Backwards";
    public const string PlayerDeath = "general/Death_A";
    public const string HitReact = "general/Hit_A";
    public const string SpinLoop = "melee/Melee_2H_Attack_Spinning";
    public const string DodgeForward = "moveadv/Dodge_Forward";
    public const string DodgeBackward = "moveadv/Dodge_Backward";
    public const string DodgeLeft = "moveadv/Dodge_Left";
    public const string DodgeRight = "moveadv/Dodge_Right";

    public const string SkeletonIdle = "special/Skeletons_Idle";
    public const string SkeletonWalk = "special/Skeletons_Walking";
    public const string SkeletonSpawn = "special/Skeletons_Spawn_Ground";
    public const string SkeletonDeath = "special/Skeletons_Death";

    private static readonly (string Name, string Path)[] Libraries =
    {
        ("move", "res://assets/animations/Rig_Medium_MovementBasic.glb"),
        ("moveadv", "res://assets/animations/Rig_Medium_MovementAdvanced.glb"),
        ("melee", "res://assets/animations/Rig_Medium_CombatMelee.glb"),
        ("general", "res://assets/animations/Rig_Medium_General.glb"),
        ("special", "res://assets/animations/Rig_Medium_Special.glb"),
    };

    private static readonly string[] LoopingClips = { Idle, Run, StrafeLeft, StrafeRight, Backpedal, SkeletonIdle, SkeletonWalk, SpinLoop };

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
