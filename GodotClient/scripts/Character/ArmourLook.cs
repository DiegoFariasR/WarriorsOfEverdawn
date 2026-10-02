using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// What each tier of armour looks like on a Rig_Medium figure: the body, arms and legs of one of the KayKit
// characters, worn in place of the figure's own, the head left as it is. Everdawn's way of dressing a character (its
// armours' appearances and CharacterAssembler): every part is skinned to the same rig, so a part's mesh goes under
// the figure's skeleton as it is and moves with it.
public static class ArmourLook
{
    // The figure's own body, arms and legs: what it wears when a tier names no other.
    public const string Own = "own";

    private const string PartsFolder = "res://assets/character_parts/";
    private const string WornPrefix = "Armour_";
    private const string OutfitMeta = "armour_outfit";
    private const string PartMeta = "armour_part";

    private static readonly string[] Slots = { "Body", "ArmLeft", "ArmRight", "LegLeft", "LegRight" };

    // A part's skin as it fits the figures of this game, by the skin it came with.
    private static readonly Dictionary<Skin, Skin> Fitted = new();

    // By tier, from what everyone starts in. Pieces are worn over the outfit's body: a breastplate, pauldrons.
    private static readonly (string Outfit, string[] Pieces)[] ByTier =
    {
        ("Hoarder", Array.Empty<string>()),
        ("Rogue", Array.Empty<string>()),
        ("Cleric", Array.Empty<string>()),
        ("Engineer", Array.Empty<string>()),
        (Own, Array.Empty<string>()),
        ("Paladin", new[] { "Paladin_ChestPlate" }),
    };

    public static string OutfitFor(int tier) =>
        tier >= 0 && tier < ByTier.Length ? ByTier[tier].Outfit : throw new ArgumentOutOfRangeException(nameof(tier), tier, $"No look for armour of tier {tier}");

    // The outfit the figure has on.
    public static string Worn(Skeleton3D skeleton) => skeleton.GetMeta(OutfitMeta, Own).AsString();

    // Whether what is on the figure is the tier's look and nothing else: its parts under the skeleton and the figure's
    // own hidden, or the figure's own showing and no others. For the self-tests.
    public static bool IsDressedFor(Skeleton3D skeleton, int tier)
    {
        string outfit = OutfitFor(tier);
        var wanted = (outfit == Own ? Enumerable.Empty<string>() : Slots.Select(slot => $"{outfit}_{slot}")).Concat(ByTier[tier].Pieces);
        var worn = skeleton.GetChildren().OfType<MeshInstance3D>()
            .Where(m => m.Visible && m.Name.ToString().StartsWith(WornPrefix, StringComparison.Ordinal))
            .Select(m => m.GetMeta(PartMeta, "").AsString());
        return Worn(skeleton) == outfit
            && worn.OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(wanted.OrderBy(p => p, StringComparer.Ordinal))
            && Slots.All(slot => OwnPart(skeleton, slot).Visible == (outfit == Own));
    }

    // Puts the tier's outfit on the figure in place of whatever it wore.
    public static void Wear(Skeleton3D skeleton, int tier)
    {
        if (ByTier.Length != Armours.MaxTier + 1)
        {
            throw new InvalidOperationException($"Armour has {Armours.MaxTier + 1} tiers and {ByTier.Length} looks");
        }

        WearOutfit(skeleton, OutfitFor(tier), ByTier[tier].Pieces);
    }

    // Puts a named outfit on the figure, with pieces worn over it. Public for the dev lineup, which tries outfits no
    // tier has.
    public static void WearOutfit(Skeleton3D skeleton, string outfit, IReadOnlyList<string> pieces)
    {
        foreach (var worn in skeleton.GetChildren().Where(c => c.Name.ToString().StartsWith(WornPrefix, StringComparison.Ordinal)).ToList())
        {
            skeleton.RemoveChild(worn);
            worn.QueueFree();
        }

        foreach (string slot in Slots)
        {
            OwnPart(skeleton, slot).Visible = outfit == Own;
            if (outfit != Own)
            {
                PutOn(skeleton, $"{outfit}_{slot}", slot);
            }
        }

        foreach (string piece in pieces)
        {
            PutOn(skeleton, piece, piece);
        }

        skeleton.SetMeta(OutfitMeta, outfit);
    }

    private static MeshInstance3D OwnPart(Skeleton3D skeleton, string slot) =>
        skeleton.GetChildren().OfType<MeshInstance3D>()
            .FirstOrDefault(m => !m.Name.ToString().StartsWith(WornPrefix, StringComparison.Ordinal) && m.Name.ToString().EndsWith($"_{slot}", StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"{skeleton.GetPath()} has no part of its own for the slot {slot}");

    private static void PutOn(Skeleton3D skeleton, string part, string name)
    {
        var scene = Assets.Instantiate($"{PartsFolder}{part}.glb");
        var meshes = scene.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false).OfType<MeshInstance3D>().ToList();
        if (meshes.Count != 1 || meshes[0].Skin == null)
        {
            scene.Free();
            throw new InvalidOperationException($"{part} should hold one skinned mesh, and holds {meshes.Count}");
        }

        // The part's own skeleton stands where the figure's does, so the mesh keeps its place under the new one.
        var mesh = meshes[0];
        var placed = mesh.Transform;
        mesh.GetParent().RemoveChild(mesh);
        mesh.Owner = null;
        mesh.Name = WornPrefix + name;
        mesh.SetMeta(PartMeta, part);
        skeleton.AddChild(mesh);
        mesh.Transform = placed;
        mesh.Skeleton = new NodePath("..");
        mesh.Skin = FittedTo(skeleton, mesh.Skin);
        scene.Free();
    }

    // Everdawn's parts were exported with their rig's control bones (IK targets, foot rolls), which the figures here
    // do not have and no vertex is weighted to. Their binds are pointed at the figure's first bone, so the skin
    // resolves; the binds stay in place, since vertices name them by position.
    private static Skin FittedTo(Skeleton3D skeleton, Skin skin)
    {
        if (!Fitted.TryGetValue(skin, out var fitted))
        {
            fitted = (Skin)skin.Duplicate();
            for (int bind = 0; bind < fitted.GetBindCount(); bind++)
            {
                if (skeleton.FindBone(fitted.GetBindName(bind)) < 0)
                {
                    fitted.SetBindName(bind, skeleton.GetBoneName(0));
                }
            }

            Fitted[skin] = fitted;
        }

        return fitted;
    }
}
