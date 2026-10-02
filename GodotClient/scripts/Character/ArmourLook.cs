using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// What each tier of armour looks like on a Rig_Medium figure: a body, arms and legs taken from KayKit characters and
// worn in place of the figure's own, the head left as it is. Everdawn's way of dressing a character (its armours'
// appearances and CharacterAssembler): every part is skinned to the same rig, so a part's mesh goes under the
// figure's skeleton as it is and moves with it. As in Everdawn the three need not come from one character: its plate
// armours give the body and legs, its bracers the arms, and its units wear one set's body with another's arms.
public static class ArmourLook
{
    // The figure's own parts: what it wears where a look names no other. For the Knight, plate.
    public const string Own = "own";

    private const string PartsFolder = "res://assets/character_parts/";
    private const string WornPrefix = "Armour_";
    private const string OutfitMeta = "armour_outfit";
    private const string PartMeta = "armour_part";
    private const string Body = "Body";

    private static readonly string[] Slots = { Body, "ArmLeft", "ArmRight", "LegLeft", "LegRight" };

    // A part's skin as it fits the figures of this game, by the skin it came with.
    private static readonly Dictionary<Skin, Skin> Fitted = new();

    // By tier, from what everyone starts in: a medium armour, the Ranger's leathers, with the Knight's plate put on
    // a part at a time (greaves, then bracers, then the cuirass), then the Paladin's plate, then its breastplate
    // over that. An earlier line started in the Hoarder's tunic, which is a round body; another was plate from the
    // first tier, with nothing to grow into.
    private static readonly Look[] ByTier =
    {
        new("Ranger", "Ranger", "Ranger"),
        new("Ranger", "Ranger", Own),
        new("Ranger", Own, Own),
        new(Own, Own, Own),
        new("Paladin", "Paladin", "Paladin"),
        new("Paladin", "Paladin", "Paladin", "Paladin_ChestPlate"),
    };

    // What the tier's look is called: the characters its body, arms and legs come from, and its pieces.
    public static string OutfitFor(int tier) =>
        tier >= 0 && tier < ByTier.Length ? ByTier[tier].Name : throw new ArgumentOutOfRangeException(nameof(tier), tier, $"No look for armour of tier {tier}");

    // The look the figure has on, as OutfitFor names it.
    public static string Worn(Skeleton3D skeleton) => skeleton.GetMeta(OutfitMeta, new Look(Own, Own, Own).Name).AsString();

    // Whether what is on the figure is the tier's look and nothing else: for each of its parts either the figure's
    // own showing, or another's under the skeleton and the figure's own hidden. For the self-tests.
    public static bool IsDressedFor(Skeleton3D skeleton, int tier)
    {
        var look = ByTier[tier];
        var wanted = Slots.Where(slot => look.From(slot) != Own).Select(slot => $"{look.From(slot)}_{slot}").Concat(look.Pieces);
        var worn = skeleton.GetChildren().OfType<MeshInstance3D>()
            .Where(m => m.Visible && m.Name.ToString().StartsWith(WornPrefix, StringComparison.Ordinal))
            .Select(m => m.GetMeta(PartMeta, "").AsString());
        return Worn(skeleton) == look.Name
            && worn.OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(wanted.OrderBy(p => p, StringComparer.Ordinal))
            && Slots.All(slot => OwnPart(skeleton, slot).Visible == (look.From(slot) == Own));
    }

    // Puts the tier's look on the figure in place of whatever it wore.
    public static void Wear(Skeleton3D skeleton, int tier)
    {
        if (ByTier.Length != Armours.MaxTier + 1)
        {
            throw new InvalidOperationException($"Armour has {Armours.MaxTier + 1} tiers and {ByTier.Length} looks");
        }

        Wear(skeleton, ByTier[tier]);
    }

    // Puts one character's body, arms and legs on the figure. For the dev lineup, which tries outfits no tier has.
    public static void WearOutfit(Skeleton3D skeleton, string outfit) => Wear(skeleton, new Look(outfit, outfit, outfit));

    private static void Wear(Skeleton3D skeleton, Look look)
    {
        foreach (var worn in skeleton.GetChildren().Where(c => c.Name.ToString().StartsWith(WornPrefix, StringComparison.Ordinal)).ToList())
        {
            skeleton.RemoveChild(worn);
            worn.QueueFree();
        }

        foreach (string slot in Slots)
        {
            string from = look.From(slot);
            OwnPart(skeleton, slot).Visible = from == Own;
            if (from != Own)
            {
                PutOn(skeleton, $"{from}_{slot}", slot);
            }
        }

        foreach (string piece in look.Pieces)
        {
            PutOn(skeleton, piece, piece);
        }

        skeleton.SetMeta(OutfitMeta, look.Name);
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
        ToonLook.Apply(mesh);
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

    // A look: the character each part comes from (Own for the figure's), and the pieces worn over the body: a
    // breastplate, pauldrons.
    private sealed record Look(string Torso, string Arms, string Legs, params string[] Pieces)
    {
        public string Name => string.Join("+", new[] { Torso, Arms, Legs }.Concat(Pieces));

        public string From(string slot) => slot == Body ? Torso : slot.StartsWith("Arm", StringComparison.Ordinal) ? Arms : Legs;
    }
}
