using System;
using System.Collections.Generic;
using Godot;

namespace WarriorsOfEverdawn.Character;

// Facts read from a clip's own bone tracks, cached per clip. Every Rig_Medium character plays the same clips, so
// one measurement serves them all.
public static class ClipMotion
{
    private const int ChestYawSamples = 32;
    private const int FootSamples = 240;

    // A foot counts as planted within this height of its lowest point in the loop.
    private const float PlantedHeight = 0.03f;

    private static readonly string[] ChestChain = { "root", "hips", "spine", "chest" };
    private static readonly string[] Feet = { "toes.l", "toes.r" };

    private static readonly Dictionary<string, float> ChestYawByClip = new();
    private static readonly Dictionary<string, float> GroundSpeedByClip = new();

    // Mean facing of the chest relative to the character's forward over one loop.
    public static float MeanChestYaw(Skeleton3D skeleton, string clip, Animation animation)
    {
        if (ChestYawByClip.TryGetValue(clip, out float cached))
        {
            return cached;
        }

        float sum = 0f;
        for (int i = 0; i < ChestYawSamples; i++)
        {
            var pose = Pose(skeleton, animation, ChestChain, animation.Length * i / ChestYawSamples);

            // KayKit bones rest facing +Z, and +X is the character's left.
            var forward = pose.Basis * Vector3.Back;
            sum += Mathf.Atan2(forward.X, forward.Z);
        }

        return ChestYawByClip[clip] = sum / ChestYawSamples;
    }

    // Ground speed a looping locomotion clip was authored for: how fast its planted foot slides back under the body
    // along the direction of travel (skeleton space; KayKit faces +Z, and +X is its left). Only that component
    // counts: any sideways drift of the planted foot is in the clip itself and no playback rate removes it.
    // Playing the clip at movement speed / this keeps the feet from sliding.
    public static float GroundSpeed(Skeleton3D skeleton, string clip, Animation animation, Vector3 travel)
    {
        if (GroundSpeedByClip.TryGetValue(clip, out float cached))
        {
            return cached;
        }

        var speeds = new List<float>();
        foreach (var foot in Feet)
        {
            var chain = ChainTo(skeleton, foot);
            var positions = new Vector3[FootSamples + 1];
            float lowest = float.MaxValue;
            for (int i = 0; i <= FootSamples; i++)
            {
                positions[i] = Pose(skeleton, animation, chain, animation.Length * i / FootSamples).Origin;
                lowest = Mathf.Min(lowest, positions[i].Y);
            }

            double step = animation.Length / FootSamples;
            for (int i = 1; i <= FootSamples; i++)
            {
                if (positions[i - 1].Y < lowest + PlantedHeight && positions[i].Y < lowest + PlantedHeight)
                {
                    speeds.Add((float)(-(positions[i] - positions[i - 1]).Dot(travel) / step));
                }
            }
        }

        if (speeds.Count == 0)
        {
            throw new InvalidOperationException($"Clip '{clip}' never plants a foot; it has no ground speed");
        }

        speeds.Sort();
        return GroundSpeedByClip[clip] = speeds[speeds.Count / 2];
    }

    // Skeleton-space pose of a bone at a time in the clip. Bones in atRest keep their rest pose instead, as when another
    // layer drives them.
    public static Transform3D BonePose(Skeleton3D skeleton, Animation animation, string bone, double time, ICollection<string>? atRest = null) =>
        skeleton.FindBone(bone) >= 0
            ? Pose(skeleton, animation, ChainTo(skeleton, bone), time, atRest)
            : throw new KeyNotFoundException($"{skeleton.GetPath()} has no bone '{bone}'");

    private static string[] ChainTo(Skeleton3D skeleton, string bone)
    {
        var chain = new List<string>();
        for (int index = skeleton.FindBone(bone); index >= 0; index = skeleton.GetBoneParent(index))
        {
            chain.Insert(0, skeleton.GetBoneName(index));
        }

        return chain.ToArray();
    }

    // Skeleton-space pose of the last bone in the chain at a time in the clip; untracked bones keep their rest.
    private static Transform3D Pose(Skeleton3D skeleton, Animation animation, string[] chain, double time, ICollection<string>? atRest = null)
    {
        var pose = Transform3D.Identity;
        foreach (var bone in chain)
        {
            var rest = skeleton.GetBoneRest(skeleton.FindBone(bone));
            if (atRest?.Contains(bone) == true)
            {
                pose *= rest;
                continue;
            }

            var path = $"{RigAnimations.SkeletonPath}:{bone}";
            int rotationTrack = animation.FindTrack(path, Animation.TrackType.Rotation3D);
            int positionTrack = animation.FindTrack(path, Animation.TrackType.Position3D);
            var rotation = rotationTrack >= 0 ? animation.RotationTrackInterpolate(rotationTrack, time) : rest.Basis.GetRotationQuaternion();
            var position = positionTrack >= 0 ? animation.PositionTrackInterpolate(positionTrack, time) : rest.Origin;
            pose *= new Transform3D(new Basis(rotation.Normalized()), position);
        }

        return pose;
    }
}
