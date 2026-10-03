using System;
using System.Collections.Generic;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// Clips shared by every KayKit Rig_Medium character. The rig GLBs are imported as animation libraries.
public static class RigAnimations
{
    public const string SkeletonPath = CharacterBody.SkeletonPath;

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
    public const string DualSlice = "melee/Melee_Dualwield_Attack_Slice";
    public const string Punch = "melee/Melee_Unarmed_Attack_Punch_A";

    // KayKit's only block raises a forearm, as with a shield on it. What is held in both hands is raised across the
    // body instead: the two-handed chop held where its wind-up has the weapon up before the chest and both hands on
    // the grip, made into a clip of its own as the libraries go in (Hold).
    public const string ForearmGuard = "melee/Melee_Blocking";
    public const string TwoHandedGuard = "melee/Melee_2H_Guard";
    private const string TwoHandedGuardFrom = "melee/Melee_2H_Attack_Chop";
    private const double TwoHandedGuardAt = 0.3;
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

    // Sitting on a seat and lying on a bed, each down, held and back up (Core's Seats).
    public const string SitDown = "sim/Sit_Chair_Down";
    public const string SitIdle = "sim/Sit_Chair_Idle";
    public const string SitUp = "sim/Sit_Chair_StandUp";
    public const string LieDown = "sim/Lie_Down";
    public const string LieIdle = "sim/Lie_Idle";
    public const string LieUp = "sim/Lie_StandUp";

    private static readonly (string Name, string Path)[] Libraries =
    {
        ("move", "res://assets/animations/Rig_Medium_MovementBasic.glb"),
        ("moveadv", "res://assets/animations/Rig_Medium_MovementAdvanced.glb"),
        ("melee", "res://assets/animations/Rig_Medium_CombatMelee.glb"),
        ("general", "res://assets/animations/Rig_Medium_General.glb"),
        ("special", "res://assets/animations/Rig_Medium_Special.glb"),
        ("ranged", "res://assets/animations/Rig_Medium_CombatRanged.glb"),
        ("sim", "res://assets/animations/Rig_Medium_Simulation.glb"),
    };

    private static readonly string[] LoopingClips = { Idle, UnarmedIdle, Run, StrafeLeft, StrafeRight, Backpedal, SkeletonIdle, SkeletonWalk, SpinLoop, ForearmGuard, MagicChannel, SitIdle, LieIdle };

    public static string GuardFor(WeaponStance stance) => stance == WeaponStance.TwoHanded ? TwoHandedGuard : ForearmGuard;

    // A clip ("library/name", as the mixers name them) straight from its library, for reading its tracks.
    public static Animation Load(string clip)
    {
        var (library, name) = LibraryOf(clip);
        return library.GetAnimation(name) ?? throw new KeyNotFoundException($"No clip '{clip}' in {library.ResourcePath}");
    }

    private static (AnimationLibrary Library, string Name) LibraryOf(string clip)
    {
        int slash = clip.IndexOf('/');
        string library = slash > 0 ? clip[..slash] : throw new ArgumentException($"Clip '{clip}' has no library prefix");
        foreach (var (name, path) in Libraries)
        {
            if (name == library)
            {
                return (Assets.Load<AnimationLibrary>(path), clip[(slash + 1)..]);
            }
        }

        throw new KeyNotFoundException($"No animation library '{library}' for clip '{clip}'");
    }

    public static void AddTo(AnimationMixer mixer)
    {
        var (melee, guard) = LibraryOf(TwoHandedGuard);
        if (!melee.HasAnimation(guard))
        {
            melee.AddAnimation(guard, Hold(Load(TwoHandedGuardFrom), TwoHandedGuardAt));
        }

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

    // Every bone where the clip has it at that moment, held: a pose that loops on itself.
    private static Animation Hold(Animation clip, double at)
    {
        var held = new Animation { Length = 1f, LoopMode = Animation.LoopModeEnum.Linear };
        for (int i = 0; i < clip.GetTrackCount(); i++)
        {
            var type = clip.TrackGetType(i);
            int track = held.AddTrack(type);
            held.TrackSetPath(track, clip.TrackGetPath(i));
            switch (type)
            {
                case Animation.TrackType.Position3D:
                    held.PositionTrackInsertKey(track, 0, clip.PositionTrackInterpolate(i, at));
                    break;
                case Animation.TrackType.Rotation3D:
                    held.RotationTrackInsertKey(track, 0, clip.RotationTrackInterpolate(i, at));
                    break;
                case Animation.TrackType.Scale3D:
                    held.ScaleTrackInsertKey(track, 0, clip.ScaleTrackInterpolate(i, at));
                    break;
                default:
                    throw new InvalidOperationException($"{clip.ResourceName} has a {type} track; a held pose is made of bone tracks only");
            }
        }

        return held;
    }

    // A player of every clip, under a figure that is not in the tree yet. Its libraries go in before it enters the
    // tree: playing first would crash (Everdawn godot-pitfalls.md).
    public static AnimationPlayer AddPlayerTo(Node3D body)
    {
        var player = new AnimationPlayer { Name = "AnimationPlayer" };
        AddTo(player);
        body.AddChild(player);
        return player;
    }
}
