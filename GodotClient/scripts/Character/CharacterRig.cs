using System;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// Pieces every Rig_Medium character shares: collision capsule, facing correction, smaller head, weapon in the
// right hand.
public static class CharacterRig
{
    public const float Height = 2.2f;

    // KayKit models face +Z; Godot's forward is -Z.
    public const float ModelYawOffset = Mathf.Pi;

    public const float HeadScale = 0.75f;
    public const float HeadgearScale = HeadScale * 1.1f;

    private const string HandBone = "handslot.r";

    private static readonly string[] HeadParts = { "_Head", "_Eyes", "_Jaw" };
    private static readonly string[] Headgear = { "Helmet", "Visor", "Hat", "Hood", "Crown" };

    public static CollisionShape3D CreateCapsule() => new()
    {
        Name = "Shape",
        Shape = new CapsuleShape3D { Radius = BodySize.Radius, Height = Height },
        Position = new Vector3(0f, Height / 2f, 0f),
    };

    // Everdawn's head sizing (CharacterAssembler.HeadScale / HeadgearScale): head, face and eye meshes shrink to
    // HeadScale, headgear a little less so it still fits over the head, all around the head bone's rest position so
    // the head stays on the neck. Everdawn picks parts from its catalogue; whole KayKit models are sorted by mesh name.
    public static void ShrinkHead(Node3D body)
    {
        var skeleton = body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath);
        int head = skeleton.FindBone("head");
        if (head < 0)
        {
            throw new InvalidOperationException($"{body.Name} has no head bone to size the head around");
        }

        var neck = skeleton.GetBoneGlobalRest(head).Origin;
        int headParts = 0;
        foreach (var mesh in skeleton.GetChildren().OfType<MeshInstance3D>())
        {
            float scale = HeadScaleOf(mesh.Name);
            if (scale < 1f)
            {
                mesh.Transform = ScaledAround(mesh.Transform, neck, scale);
                headParts += scale == HeadScale ? 1 : 0;
            }
        }

        // Headgear the importer hangs on the head bone sits in the bone's own space, where the bone is the origin.
        foreach (var attachment in skeleton.GetChildren().OfType<BoneAttachment3D>().Where(a => a.BoneName == "head"))
        {
            foreach (var mesh in attachment.GetChildren().OfType<MeshInstance3D>().Where(m => IsHeadgear(m.Name)))
            {
                mesh.Transform = ScaledAround(mesh.Transform, Vector3.Zero, HeadgearScale);
            }
        }

        if (headParts == 0)
        {
            throw new InvalidOperationException($"{body.Name} has no head mesh to shrink (looked for names ending {string.Join(", ", HeadParts)})");
        }
    }

    public static float HeadScaleOf(string meshName) =>
        IsHeadgear(meshName) ? HeadgearScale : HeadParts.Any(meshName.EndsWith) ? HeadScale : 1f;

    private static bool IsHeadgear(string meshName) => Headgear.Any(meshName.Contains);

    private static Transform3D ScaledAround(Transform3D transform, Vector3 pivot, float scale) =>
        new(transform.Basis.Scaled(Vector3.One * scale), pivot * (1f - scale) + transform.Origin * scale);

    public static BoneAttachment3D AttachToHand(Node3D body, string weaponPath)
    {
        var weapon = Assets.Instantiate(weaponPath);

        // Weapon GLBs can carry offsets on nested nodes; the hand slot bone is already the grip point.
        foreach (var node in weapon.FindChildren("*", nameof(Node3D), recursive: true, owned: false))
        {
            ((Node3D)node).Position = Vector3.Zero;
        }

        var hand = new BoneAttachment3D { Name = "RightHand", BoneName = HandBone };
        hand.AddChild(weapon);
        body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath).AddChild(hand);
        return hand;
    }
}
