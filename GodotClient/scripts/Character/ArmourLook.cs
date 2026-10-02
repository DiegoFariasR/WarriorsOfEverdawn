using System;
using System.Linq;
using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Character;

// What each tier of armour looks like on a figure: a body, arms and legs taken from KayKit characters and worn in
// place of the figure's own, the head and what is on it left as they are. As in Everdawn the three need not come
// from one character: its plate armours give the body and legs, its bracers the arms, and its units wear one set's
// body with another's arms. A tier's look is the figure's own look with those slots changed (CharacterBody).
public static class ArmourLook
{
    // The figure's own parts: what it wears where an outfit names no other. For the Knight, plate.
    public const string Own = "own";

    private const string OutfitMeta = "armour_outfit";

    // By tier, from what everyone starts in: a medium armour, the Ranger's leathers, with the Knight's plate put on
    // a part at a time (greaves, then bracers, then the cuirass), then the Paladin's plate, then its breastplate
    // over that. An earlier line started in the Hoarder's tunic, which is a round body; another was plate from the
    // first tier, with nothing to grow into.
    private static readonly Outfit[] ByTier =
    {
        new("Ranger", "Ranger", "Ranger"),
        new("Ranger", "Ranger", Own),
        new("Ranger", Own, Own),
        new(Own, Own, Own),
        new("Paladin", "Paladin", "Paladin"),
        new("Paladin", "Paladin", "Paladin", "Paladin_ChestPlate"),
    };

    // What the tier's outfit is called: the characters its body, arms and legs come from, and its pieces.
    public static string OutfitFor(int tier) => Of(tier).Name;

    // The outfit the figure has on, as OutfitFor names it.
    public static string Worn(Skeleton3D skeleton) => skeleton.GetMeta(OutfitMeta, new Outfit(Own, Own, Own).Name).AsString();

    // The figure's look in the tier's armour.
    public static CharacterLook Dressed(CharacterLook own, int tier) => Of(tier).On(own);

    // The figure's look in one character's body, arms and legs. For the dev lineup, which tries outfits no tier has.
    public static CharacterLook DressedAs(CharacterLook own, string outfit) => new Outfit(outfit, outfit, outfit).On(own);

    // A figure in the tier's armour.
    public static Node3D Build(CharacterLook own, int tier)
    {
        var body = CharacterBody.Build(Dressed(own, tier));
        CharacterBody.SkeletonOf(body).SetMeta(OutfitMeta, OutfitFor(tier));
        return body;
    }

    // Puts the tier's armour on the figure in place of whatever it wore.
    public static void Wear(Skeleton3D skeleton, CharacterLook own, int tier)
    {
        CharacterBody.Dress(skeleton, Dressed(own, tier));
        skeleton.SetMeta(OutfitMeta, OutfitFor(tier));
    }

    // Whether what is on the figure is the tier's look and nothing else, by the parts that are under its skeleton.
    // For the self-tests.
    public static bool IsDressedFor(Skeleton3D skeleton, CharacterLook own, int tier)
    {
        var look = Dressed(own, tier);
        var wanted = look.Parts().SelectMany(p => CharacterBody.Catalog.Pieces(p.Part)).Select(p => p.Stem);
        var worn = CharacterBody.PartsOn(skeleton).Where(p => p.Mesh.Visible).Select(p => p.Part);
        return Worn(skeleton) == OutfitFor(tier)
            && CharacterBody.Worn(skeleton) == look.Key
            && worn.OrderBy(p => p, StringComparer.Ordinal).SequenceEqual(wanted.OrderBy(p => p, StringComparer.Ordinal));
    }

    private static Outfit Of(int tier)
    {
        if (ByTier.Length != Armours.MaxTier + 1)
        {
            throw new InvalidOperationException($"Armour has {Armours.MaxTier + 1} tiers and {ByTier.Length} looks");
        }

        return tier >= 0 && tier < ByTier.Length ? ByTier[tier] : throw new ArgumentOutOfRangeException(nameof(tier), tier, $"No look for armour of tier {tier}");
    }

    // An outfit: the character each part comes from (Own for the figure's), and the pieces worn over the body: a
    // breastplate, pauldrons.
    private sealed record Outfit(string Torso, string Arms, string Legs, params string[] Pieces)
    {
        public string Name => string.Join("+", new[] { Torso, Arms, Legs }.Concat(Pieces));

        public CharacterLook On(CharacterLook own) =>
            own.Wearing(Torso == Own ? null : Torso, Arms == Own ? null : Arms, Legs == Own ? null : Legs).Adding(Pieces);
    }
}
