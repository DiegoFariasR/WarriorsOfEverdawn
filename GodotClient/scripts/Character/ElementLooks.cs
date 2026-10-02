using System;
using System.Collections.Generic;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// What each strike of a spell held on an area looks like.
public enum SpellStrike
{
    // A column standing up out of the ground (Inferno).
    Pillar,

    // A spike of rock thrust up from below (Earthquake).
    Spike,

    // A shard falling out of the sky (Blizzard).
    Shard,

    // A bolt from the sky, there and gone (Lightning Storm).
    Flash,

    // A ring spreading from the middle (Divine Nova).
    Ring,

    // A swelling that lingers (Void Corrosion).
    Blob,
}

// How an element looks, after Everdawn: its two colours (ElementPalettes), the surface shader laid over anything made
// of it with that shader's brightest layer (EffectVisualLibrary), and the strike of its area spell (its skills'
// visual shapes). Wind and divine take Everdawn's second colour first: its firsts for the two are both near white,
// and here they have to be told apart at a glance, wind a pale blue and divine gold.
public sealed record ElementLook(Color Primary, Color Secondary, string Shader, Color Tint, float NoiseScale, float Speed, float Dissolve, SpellStrike Strike, int StrikesPerCycle);

public static class ElementLooks
{
    private const string Shaders = "res://assets/shaders/elemental_";

    private static readonly Dictionary<Element, ElementLook> ByElement = new()
    {
        [Element.Fire] = new(new Color(1f, 0.44f, 0f), new Color(0.8f, 0.15f, 0f), "fire", new Color(1f, 0.95f, 0.35f), 7f, 13f, 0.48f, SpellStrike.Pillar, 3),
        [Element.Water] = new(new Color(0.3f, 0.65f, 0.78f), new Color(0.1f, 0.4f, 0.55f), "water", new Color(0.4f, 0.8f, 0.9f), 7.5f, 1.4f, 0.35f, SpellStrike.Shard, 5),
        [Element.Wind] = new(new Color(0.55f, 0.78f, 1f), new Color(0.78f, 0.92f, 1f), "wind", new Color(0.92f, 0.98f, 1f), 7.5f, 3.6f, 0.6f, SpellStrike.Flash, 3),
        [Element.Earth] = new(new Color(0.5f, 0.35f, 0.1f), new Color(0.3f, 0.5f, 0.1f), "earth", new Color(0.45f, 0.33f, 0.2f), 2f, 0f, 0.7f, SpellStrike.Spike, 4),
        [Element.Divine] = new(new Color(1f, 0.82f, 0.28f), new Color(1f, 0.98f, 0.92f), "divine", new Color(1f, 1f, 0.98f), 4f, 0.6f, 0.44f, SpellStrike.Ring, 1),
        [Element.Void] = new(new Color(0.3f, 0.05f, 0.5f), new Color(0.12f, 0f, 0.2f), "void", new Color(0.38f, 0f, 0.58f), 4f, 1.8f, 0.65f, SpellStrike.Blob, 3),
    };

    private static readonly Dictionary<Element, StandardMaterial3D> Glows = new();
    private static readonly Dictionary<Element, ShaderMaterial> Overlays = new();

    public static ElementLook For(Element element) =>
        ByElement.TryGetValue(element, out var look) ? look : throw new KeyNotFoundException($"No look for the element {element}");

    // The element's own colour, lit from within. Shared: nothing changes it once made.
    public static StandardMaterial3D Glow(Element element)
    {
        if (!Glows.TryGetValue(element, out var glow))
        {
            var look = For(element);
            glow = new StandardMaterial3D
            {
                AlbedoColor = look.Primary,
                EmissionEnabled = true,
                Emission = look.Primary,
                EmissionEnergyMultiplier = 1f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            Glows[element] = glow;
        }

        return glow;
    }

    // The element's surface shader, to lay over a mesh made of it. Shared.
    public static ShaderMaterial Overlay(Element element)
    {
        if (!Overlays.TryGetValue(element, out var overlay))
        {
            var look = For(element);
            overlay = new ShaderMaterial { Shader = Assets.Load<Shader>($"{Shaders}{look.Shader}.gdshader") };
            overlay.SetShaderParameter("tint", look.Tint);
            overlay.SetShaderParameter("noise_scale", look.NoiseScale);
            overlay.SetShaderParameter("speed", look.Speed);
            overlay.SetShaderParameter("dissolve", look.Dissolve);
            Overlays[element] = overlay;
        }

        return overlay;
    }

    // A mesh made of the element: its colour, with its shader over it. Nothing made of magic casts a shadow.
    public static MeshInstance3D Made(Element element, Mesh mesh) => new()
    {
        Mesh = mesh,
        MaterialOverride = Glow(element),
        MaterialOverlay = Overlay(element),
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    // Everdawn's volley dart (SpellProjectile.BuildBipyramidMesh): two four-sided pyramids base to base, its length
    // along +Y.
    public static ArrayMesh Bipyramid(float radius, float length)
    {
        var top = new Vector3(0f, length / 2f, 0f);
        var bottom = -top;
        var rim = new Vector3[4];
        for (int i = 0; i < rim.Length; i++)
        {
            float angle = i * Mathf.Tau / rim.Length;
            rim[i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < rim.Length; i++)
        {
            var a = rim[i];
            var b = rim[(i + 1) % rim.Length];
            foreach (var corner in new[] { top, b, a, bottom, a, b })
            {
                tool.AddVertex(corner);
            }
        }

        tool.GenerateNormals();
        return tool.Commit();
    }

    public static float Jitter(float around, float share) => around * (1f + (GD.Randf() * 2f - 1f) * share);

    // A point in a disc of this radius, any point as likely as another.
    public static Vector3 SpotWithin(float radius)
    {
        float angle = GD.Randf() * Mathf.Tau;
        float distance = radius * MathF.Sqrt(GD.Randf());
        return new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
    }
}
