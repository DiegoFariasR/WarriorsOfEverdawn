using System;
using System.Collections.Generic;
using EverdawnKit.Magic;
using Godot;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Character;

// What each strike of a spell held on an area looks like.
public enum SpellStrike
{
    // A column standing up out of the ground (Inferno, Geyser).
    Pillar,

    // A spike of rock thrust up from below (Earthquake).
    Spike,

    // A shard falling out of the sky (Blizzard).
    Shard,

    // A bolt from the sky, there and gone (Lightning Storm).
    Flash,

    // A ring spreading from the middle (Divine Nova, Cyclone).
    Ring,

    // A swelling that lingers (Void Corrosion).
    Blob,
}

// How an element looks here: the kit's look of it (EverdawnKit.Magic.MagicLooks, Everdawn's), the colour it leads
// with, and the strike of its area spell (after its skills' visual shapes in Everdawn). Wind, divine and ice lead with
// the second of the kit's two colours: the firsts of the three are all near white, and here they have to be told
// apart at a glance, wind a pale blue, divine gold and ice a full blue.
public sealed record ElementLook(MagicElement Magic, bool SecondFirst, SpellStrike Strike, int StrikesPerCycle)
{
    public MagicLook Kit => MagicLooks.For(Magic);

    public Color Primary => MagicMaterials.ToColor(SecondFirst ? Kit.Secondary : Kit.Primary);

    public Color Secondary => MagicMaterials.ToColor(SecondFirst ? Kit.Primary : Kit.Secondary);
}

public static class ElementLooks
{
    private static readonly Dictionary<Element, ElementLook> ByElement = new()
    {
        [Element.Fire] = new(MagicElement.Fire, SecondFirst: false, SpellStrike.Pillar, 3),
        [Element.Water] = new(MagicElement.Water, SecondFirst: false, SpellStrike.Pillar, 3),
        [Element.Ice] = new(MagicElement.Ice, SecondFirst: true, SpellStrike.Shard, 5),
        [Element.Wind] = new(MagicElement.Wind, SecondFirst: true, SpellStrike.Ring, 1),
        [Element.Lightning] = new(MagicElement.Lightning, SecondFirst: false, SpellStrike.Flash, 3),
        [Element.Earth] = new(MagicElement.Earth, SecondFirst: false, SpellStrike.Spike, 4),
        [Element.Divine] = new(MagicElement.Divine, SecondFirst: true, SpellStrike.Ring, 1),
        [Element.Void] = new(MagicElement.Void, SecondFirst: false, SpellStrike.Blob, 3),
    };

    private static readonly Dictionary<Element, ShaderMaterial> Overlays = new();
    private static readonly Dictionary<Element, ShaderMaterial> Enchantments = new();

    // How much of its shader each element shows on an enchanted weapon, as a share of what a thing made of it shows:
    // enough to tell the element, with the weapon still to be seen under it. Chosen by eye, element by element
    // (./dev.sh weapon-lineup greatsword~fire): fire at full strength made a blade one flat colour, and water, void
    // and divine at fire's share did not show at all. Divine is the least settled: its sparkle barely shows on a thin
    // staff and runs to white over a broad blade, whatever the share. Lightning comes and goes, as its shader does:
    // the blade flashes for a third of a second and is plain steel for the next half.
    private static readonly Dictionary<Element, float> EnchantmentStrength = new()
    {
        [Element.Fire] = 0.8f,
        [Element.Water] = 3f,
        [Element.Ice] = 3f,
        [Element.Wind] = 0.9f,
        [Element.Lightning] = 1.1f,
        [Element.Earth] = 0.8f,
        [Element.Divine] = 1.2f,
        [Element.Void] = 1.2f,
    };

    public static ElementLook For(Element element) =>
        ByElement.TryGetValue(element, out var look) ? look : throw new KeyNotFoundException($"No look for the element {element}");

    // The element's surface shader, its brightest layer alone, to lay over a mesh made of it. Shared.
    public static ShaderMaterial Overlay(Element element)
    {
        if (!Overlays.TryGetValue(element, out var overlay))
        {
            overlay = MagicMaterials.Overlay(For(element).Magic, layers: 1);
            Overlays[element] = overlay;
        }

        return overlay;
    }

    // The element's surface shader as it plays over an enchanted weapon: in the colour the element leads with, and
    // thinner than over a thing made of it. Shared.
    public static ShaderMaterial Enchantment(Element element)
    {
        if (!Enchantments.TryGetValue(element, out var overlay))
        {
            var look = For(element);
            var outermost = look.Kit.Outermost;
            var lead = look.SecondFirst ? look.Kit.Secondary : look.Kit.Primary;
            overlay = MagicMaterials.Layer(look.Kit.Shader, outermost with { Tint = lead, Dissolve = Mathf.Min(1f, outermost.Dissolve * EnchantmentStrength[element]) });
            Enchantments[element] = overlay;
        }

        return overlay;
    }

    // A mesh made of the element, as Everdawn draws a spell: the colour it leads with, see-through and lit from within
    // (MagicMaterials.Spell), with every layer of its shader over it. Its own materials, so each fades on its own.
    // Nothing made of magic casts a shadow.
    public static MeshInstance3D Made(Element element, Mesh mesh)
    {
        var look = For(element);
        return new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = MagicMaterials.Spell(look.Primary),
            MaterialOverlay = MagicMaterials.Overlay(look.Magic),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    // The element's overlay over a mesh of one of the kit's spell effects (EverdawnKit.Magic).
    public static Action<MeshInstance3D> Dress(Element element) => mesh => mesh.MaterialOverlay = MagicMaterials.Overlay(For(element).Magic);

    public static float Jitter(float around, float share) => around * (1f + (GD.Randf() * 2f - 1f) * share);

    // A point in a disc of this radius, any point as likely as another.
    public static Vector3 SpotWithin(float radius)
    {
        float angle = GD.Randf() * Mathf.Tau;
        float distance = radius * MathF.Sqrt(GD.Randf());
        return new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
    }
}
