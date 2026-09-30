using Godot;

namespace WarriorsOfEverdawn.Character;

// Turns spine and chest about the vertical so the upper body faces the aim while the legs face the movement.
// Split across both bones so the torso bends instead of kinking at one joint.
public partial class TorsoTwistModifier : SkeletonModifier3D
{
    // Beyond this the torso corkscrews; the body turn catches up within a few frames anyway.
    public const float MaxTwist = 80f * Mathf.Pi / 180f;

    private int _hips = -1;
    private int _spine = -1;
    private int _chest = -1;

    public float Twist { get; set; }

    public float AppliedTwist { get; private set; }

    public int ChestBone => _chest;

    public override void _ProcessModificationWithDelta(double delta)
    {
        var skeleton = GetSkeleton();
        if (_chest < 0 && !FindBones(skeleton))
        {
            return;
        }

        AppliedTwist = Mathf.Clamp(Twist, -MaxTwist, MaxTwist);
        var turn = new Quaternion(Vector3.Up, AppliedTwist * 0.5f);

        // Both parents are read before either bone changes: the chest's turn is expressed in the spine's
        // pre-twist frame, and the spine's own turn then carries it the rest of the way.
        var hips = skeleton.GetBoneGlobalPose(_hips).Basis.GetRotationQuaternion();
        var spine = skeleton.GetBoneGlobalPose(_spine).Basis.GetRotationQuaternion();
        TurnInParentFrame(skeleton, _spine, hips, turn);
        TurnInParentFrame(skeleton, _chest, spine, turn);
    }

    private static void TurnInParentFrame(Skeleton3D skeleton, int bone, Quaternion parentGlobal, Quaternion turn)
    {
        var local = skeleton.GetBonePoseRotation(bone);
        skeleton.SetBonePoseRotation(bone, parentGlobal.Inverse() * turn * parentGlobal * local);
    }

    private bool FindBones(Skeleton3D skeleton)
    {
        _hips = skeleton.FindBone("hips");
        _spine = skeleton.FindBone("spine");
        _chest = skeleton.FindBone("chest");
        if (_hips >= 0 && _spine >= 0 && _chest >= 0)
        {
            return true;
        }

        GD.PushError($"[TorsoTwist] {skeleton.GetPath()} is missing hips/spine/chest; torso twist disabled");
        _chest = -1;
        Active = false;
        return false;
    }
}
