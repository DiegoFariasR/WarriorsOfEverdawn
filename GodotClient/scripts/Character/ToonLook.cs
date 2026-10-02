using System.Collections.Generic;
using System.Linq;
using Godot;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// Everdawn's look for characters and what they hold (its CharacterAssembler.ApplyCrudeMetallicLook): cel-shaded
// surfaces with a dark outline round every mesh, and real metal only where the model is metal, so plate and blades
// catch the sky while skin, cloth and leather stay flat. A character's metal is told by where its texture is read:
// KayKit's character textures are a grid of 8 by 4 colours with the two metals in the first row. A weapon's is told
// by colour, since weapon textures put their blade greys anywhere.
public static class ToonLook
{
    private const string OutlinePath = "res://assets/shaders/toon_outline.gdshader";

    // Everdawn's (CharacterAssembler.CharacterLightLayer): the render layer every mesh with this look joins, on
    // top of the default one, so a light whose cull mask is this layer alone lifts the figures and not the ground
    // and walls about them (WorldLook's fill).
    public const int CharacterLightLayer = 2;

    // Everdawn's numbers: metal is three quarters mirror, so its own colour still shows under a dim sky.
    private const float PeakMetal = 0.75f;
    private const float MetalRoughness = 0.15f;

    // A weapon's texture is read at this size to find its metal: its colours are flat patches, and the full
    // texture is a million pixels to walk.
    private const int WeaponMaskSize = 128;

    private static readonly string[] Untouched = { "_Eyes" };

    private static readonly Dictionary<(Material? Source, bool Weapon), StandardMaterial3D> Made = new();
    private static readonly Dictionary<Texture2D, (ImageTexture Metal, ImageTexture Rough)> WeaponMasks = new();
    private static (ImageTexture Metal, ImageTexture Rough)? _paletteMasks;
    private static ShaderMaterial? _outline;

    // A character's body and what it wears.
    public static void Apply(Node root) => Apply(root, weapon: false);

    // A weapon, a shield, anything held.
    public static void ApplyToWeapon(Node root) => Apply(root, weapon: true);

    private static void Apply(Node root, bool weapon)
    {
        var meshes = root.FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false).OfType<MeshInstance3D>().ToList();
        if (root is MeshInstance3D whole)
        {
            meshes.Add(whole);
        }

        foreach (var mesh in meshes)
        {
            if (mesh.Mesh == null)
            {
                continue;
            }

            mesh.SetLayerMaskValue(CharacterLightLayer, true);

            // A skeleton's eyes glow by their own material.
            if (Untouched.Any(mesh.Name.ToString().EndsWith))
            {
                continue;
            }

            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                var source = mesh.GetActiveMaterial(surface);
                if (source is StandardMaterial3D already && Made.ContainsValue(already))
                {
                    continue;
                }

                mesh.SetSurfaceOverrideMaterial(surface, Toon(source, weapon));
            }
        }
    }

    // Shared by every mesh drawn with the same material: nothing changes one once it is made.
    private static StandardMaterial3D Toon(Material? source, bool weapon)
    {
        if (!Made.TryGetValue((source, weapon), out var toon))
        {
            toon = source is StandardMaterial3D standard ? (StandardMaterial3D)standard.Duplicate() : new StandardMaterial3D();
            var (metal, rough) = weapon && toon.AlbedoTexture is { } albedo ? WeaponMasksOf(albedo) : PaletteMasks();
            toon.DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon;
            toon.SpecularMode = BaseMaterial3D.SpecularModeEnum.Toon;
            toon.Metallic = 1f;
            toon.MetallicTexture = metal;
            toon.Roughness = 1f;
            toon.RoughnessTexture = rough;
            toon.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
            toon.NextPass = _outline ??= new ShaderMaterial { Shader = Assets.Load<Shader>(OutlinePath) };
            Made[(source, weapon)] = toon;
        }

        return toon;
    }

    // KayKit's character grid: metal in the first row's fourth and fifth cells, everything else matte.
    private static (ImageTexture Metal, ImageTexture Rough) PaletteMasks()
    {
        if (_paletteMasks is not { } masks)
        {
            var metal = Image.CreateEmpty(8, 4, false, Image.Format.L8);
            var rough = Image.CreateEmpty(8, 4, false, Image.Format.L8);
            metal.Fill(Colors.Black);
            rough.Fill(Colors.White);
            foreach (int column in new[] { 3, 4 })
            {
                metal.SetPixel(column, 0, new Color(PeakMetal, PeakMetal, PeakMetal));
                rough.SetPixel(column, 0, new Color(MetalRoughness, MetalRoughness, MetalRoughness));
            }

            masks = (ImageTexture.CreateFromImage(metal), ImageTexture.CreateFromImage(rough));
            _paletteMasks = masks;
        }

        return masks;
    }

    // Everdawn's reading of a weapon's texture (GetGreyDetectMaskTextures): a pixel with little colour in it is
    // metal, cool greys more readily than warm ones so that steel shines and leather grips do not, and the darkest
    // and lightest pixels (outlines, paper) are left matte. A machine that draws nothing (a headless run) has no
    // pixels to read, and gets the character grid.
    private static (ImageTexture Metal, ImageTexture Rough) WeaponMasksOf(Texture2D albedo)
    {
        if (WeaponMasks.TryGetValue(albedo, out var masks))
        {
            return masks;
        }

        var image = albedo.GetImage();
        if (image == null || image.IsEmpty())
        {
            return PaletteMasks();
        }

        if (image.IsCompressed())
        {
            image.Decompress();
        }

        image.Resize(WeaponMaskSize, WeaponMaskSize, Image.Interpolation.Nearest);
        var metal = Image.CreateEmpty(WeaponMaskSize, WeaponMaskSize, false, Image.Format.L8);
        var rough = Image.CreateEmpty(WeaponMaskSize, WeaponMaskSize, false, Image.Format.L8);
        for (int y = 0; y < WeaponMaskSize; y++)
        {
            for (int x = 0; x < WeaponMaskSize; x++)
            {
                var pixel = image.GetPixel(x, y);
                float brightest = Mathf.Max(Mathf.Max(pixel.R, pixel.G), pixel.B);
                float chroma = brightest - Mathf.Min(Mathf.Min(pixel.R, pixel.G), pixel.B);
                bool cool = pixel.B >= pixel.R;
                float grey = 1f - Mathf.SmoothStep(cool ? 0.2f : 0.1f, cool ? 0.3f : 0.2f, chroma);
                float notDark = Mathf.SmoothStep(0.02f, 0.1f, brightest);
                float notLight = 1f - Mathf.SmoothStep(0.9f, 0.98f, brightest);
                float metalness = grey * notDark * notLight;
                float shine = metalness * PeakMetal;
                float roughness = 1f - metalness * (1f - MetalRoughness);
                metal.SetPixel(x, y, new Color(shine, shine, shine));
                rough.SetPixel(x, y, new Color(roughness, roughness, roughness));
            }
        }

        masks = (ImageTexture.CreateFromImage(metal), ImageTexture.CreateFromImage(rough));
        WeaponMasks[albedo] = masks;
        return masks;
    }
}
