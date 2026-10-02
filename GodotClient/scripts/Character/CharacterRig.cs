using System;
using System.Collections.Generic;
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

    // Everdawn's (CharacterAssembler.HeadScale), so a figure here has a figure there's proportions. It was 0.55 for
    // a while, on the reasoning that this camera looks down on helmets over a foreshortened body; that left a small
    // head on a wide body, which read as squat beside Everdawn's.
    public const float HeadScale = 0.75f;
    public const float HeadgearScale = HeadScale * 1.1f;

    public const string HandBone = "handslot.r";

    public const string LeftHandBone = "handslot.l";

    public const string BackBone = "chest";

    // Two points within this distance of the grip count as equally far.
    private const float TipTie = 0.01f;

    private static readonly string[] HeadParts = { "_Head", "_Eyes", "_Jaw", "_Mask" };
    private static readonly string[] Headgear = { "Helmet", "Visor", "Hat", "Hood", "Crown" };

    public static CollisionShape3D CreateCapsule() => new()
    {
        Name = "Shape",
        Shape = new CapsuleShape3D { Radius = BodySize.Radius, Height = Height },
        Position = new Vector3(0f, Height / 2f, 0f),
    };

    // Everdawn's head sizing (CharacterAssembler): head, face and eye meshes shrink to HeadScale, headgear a little
    // less so it still fits over the head, all around the head bone's rest position so the head stays on the neck.
    // Everdawn picks parts from its catalogue; whole KayKit models are sorted by mesh name.
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

    // By the part's own name, after the character's: "RogueHooded_Body" is no hood.
    private static bool IsHeadgear(string meshName) => Headgear.Any(meshName[(meshName.LastIndexOf('_') + 1)..].Contains);

    private static Transform3D ScaledAround(Transform3D transform, Vector3 pivot, float scale) =>
        new(transform.Basis.Scaled(Vector3.One * scale), pivot * (1f - scale) + transform.Origin * scale);

    // A weapon modelled to be held at its origin, as it is.
    public static BoneAttachment3D AttachToHand(Node3D body, string weaponPath)
    {
        var hand = NewHand(body);
        HoldWeapon(hand, weaponPath, Vector3.Zero);
        return hand;
    }

    // The look's weapon in the right hand; an empty hand, with no look.
    public static BoneAttachment3D AttachToHand(Node3D body, WeaponLook? look)
    {
        var hand = NewHand(body);
        HoldWeapon(hand, look);
        return hand;
    }

    private static BoneAttachment3D NewHand(Node3D body)
    {
        var hand = new BoneAttachment3D { Name = "RightHand", BoneName = HandBone };
        body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath).AddChild(hand);
        return hand;
    }

    // The left hand, holding the look's off-hand piece when it has one.
    public static BoneAttachment3D AttachOffHand(Node3D body, WeaponLook? look)
    {
        var hand = new BoneAttachment3D { Name = "OffHand", BoneName = LeftHandBone };
        HoldOffHand(hand, look);
        body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath).AddChild(hand);
        return hand;
    }

    // Replaces whatever the off hand holds with the look's piece for it; an empty hand, when the look has none.
    public static void HoldOffHand(BoneAttachment3D offHand, WeaponLook? look)
    {
        Empty(offHand);
        if (look?.OffHand is { } piece)
        {
            offHand.AddChild(Adorned(Placed(piece.Model, piece.HandPosition, piece.HandRotation), look with { Glow = null }));
        }
    }

    public static BoneAttachment3D AttachToLeftHand(Node3D body, string weaponPath, Vector3 rotation)
    {
        var hand = new BoneAttachment3D { Name = "LeftHand", BoneName = LeftHandBone };
        HoldWeapon(hand, weaponPath, Vector3.Zero).Rotation = rotation;
        body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath).AddChild(hand);
        return hand;
    }

    public static BoneAttachment3D AttachToBack(Node3D body, WeaponLook look)
    {
        var back = new BoneAttachment3D { Name = "Back", BoneName = BackBone };
        HoldOnBack(back, look);
        body.GetNode<Skeleton3D>(RigAnimations.SkeletonPath).AddChild(back);
        return back;
    }

    // Replaces whatever is carried on the back, placed as its look says; nothing, with no look.
    public static void HoldOnBack(BoneAttachment3D back, WeaponLook? look)
    {
        if (look == null)
        {
            Empty(back);
            return;
        }

        var weapon = Adorned(HoldWeapon(back, look.Model, look.BackGrip, look.Scale), look);
        weapon.Transform = new Transform3D(Basis.FromEuler(look.BackRotation), look.BackPosition) * weapon.Transform;
        if (look.OffHand is { } piece)
        {
            back.AddChild(Adorned(Placed(piece.Model, piece.BackPosition, piece.BackRotation), look with { Glow = null }));
        }
    }

    private static Node3D Placed(string modelPath, Vector3 position, Vector3 rotation)
    {
        var model = Assets.InstantiateAtOrigin(modelPath);
        ToonLook.ApplyToWeapon(model);
        model.Transform = new Transform3D(Basis.FromEuler(rotation), position);
        return model;
    }

    // Replaces whatever the hand holds with the look's weapon, at its grip; an empty hand, with no look.
    public static void HoldWeapon(BoneAttachment3D hand, WeaponLook? look)
    {
        if (look == null)
        {
            Empty(hand);
        }
        else
        {
            Adorned(HoldWeapon(hand, look.Model, look.Grip, look.Scale, look.HandTurn), look);
        }
    }

    // The weapon with what its look adds to its model: an enchantment's element over all of it (Everdawn's elemental
    // overlay), a staff's element alight at its head.
    public static Node3D Adorned(Node3D weapon, WeaponLook look)
    {
        if (look.Enchant is { } enchantment)
        {
            foreach (var mesh in weapon.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false).OfType<MeshInstance3D>())
            {
                mesh.MaterialOverlay = ElementLooks.Enchantment(enchantment);
            }

            if (weapon is MeshInstance3D whole)
            {
                whole.MaterialOverlay = ElementLooks.Enchantment(enchantment);
            }
        }

        if (look.Glow is { } element)
        {
            var orb = ElementLooks.Made(element, new SphereMesh { Radius = look.GlowRadius, Height = look.GlowRadius * 2f, RadialSegments = 12, Rings = 6 });
            orb.Name = "Glow";
            orb.Position = look.GlowAt;
            weapon.AddChild(orb);
        }

        return weapon;
    }

    // Replaces whatever the hand holds. The grip is the point on the weapon, in its own space, that the hand closes
    // on: the origin for weapons modelled to be held there. The weapon is drawn `scale` times its modelled size and
    // turned `turn` about its own length.
    public static Node3D HoldWeapon(BoneAttachment3D hand, string weaponPath, Vector3 grip, float scale = 1f, float turn = 0f)
    {
        Empty(hand);

        // The hand slot bone is already the grip point.
        var weapon = Assets.InstantiateAtOrigin(weaponPath);
        ToonLook.ApplyToWeapon(weapon);
        weapon.Basis = Basis.FromEuler(new Vector3(0f, turn, 0f)).Scaled(Vector3.One * scale);
        weapon.Position = -(weapon.Basis * grip);
        hand.AddChild(weapon);
        return weapon;
    }

    private static void Empty(BoneAttachment3D slot)
    {
        foreach (var held in slot.GetChildren())
        {
            slot.RemoveChild(held);
            held.QueueFree();
        }
    }

    // Every vertex of the held weapon, in the hand's space.
    public static Vector3[] WeaponPoints(BoneAttachment3D hand)
    {
        var points = new List<Vector3>();
        foreach (var mesh in hand.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false).OfType<MeshInstance3D>())
        {
            var toHand = RelativeTransform(mesh, hand);
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                points.AddRange(mesh.Mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array().Select(v => toHand * v));
            }
        }

        return points.Count > 0 ? points.ToArray() : throw new InvalidOperationException($"{hand.GetPath()} holds no mesh");
    }

    // The weapon's striking point: the vertex farthest from the grip. That is the blade tip on a sword or spear, the
    // far end of a staff, and the point of a scythe's blade. On a tie the upper end wins.
    public static Vector3 WeaponTip(IEnumerable<Vector3> points)
    {
        var tip = Vector3.Zero;
        foreach (var point in points)
        {
            float farther = point.Length() - tip.Length();
            if (farther > TipTie || (farther > -TipTie && point.Y > tip.Y))
            {
                tip = point;
            }
        }

        return tip;
    }

    // How far the weapon reaches across the ground from a centre: its farthest point, whichever part that is. A
    // scythe's blade curves back towards the wielder, so its point is not always what reaches furthest.
    public static float WeaponReach(IEnumerable<Vector3> points, Transform3D hand, Vector3 centre) =>
        points.Max(p =>
        {
            var offset = hand * p - centre;
            return new Vector2(offset.X, offset.Z).Length();
        });

    private static Transform3D RelativeTransform(Node3D node, Node3D ancestor)
    {
        var transform = Transform3D.Identity;
        for (var current = node; current != ancestor; current = current.GetParent() as Node3D
            ?? throw new InvalidOperationException($"{node.Name} is not under {ancestor.Name}"))
        {
            transform = current.Transform * transform;
        }

        return transform;
    }
}
