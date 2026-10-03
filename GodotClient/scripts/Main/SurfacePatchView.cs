using System;
using System.Collections.Generic;
using EverdawnKit.Magic;
using Godot;
using WarriorsOfEverdawn.Character;
using WarriorsOfEverdawn.Core.Combat;

namespace WarriorsOfEverdawn.Main;

// How a patch of burning ground or ice looks where it lies, for as long as it lasts. Burning ground: the ground
// scorched dark, tongues of fire standing up out of it and flickering, embers rising, and an orange light thrown about.
// Ice: a sheet of it, pale and glossy, with shards standing up out of it. Each spreads over a moment as it is laid and
// fades as it ends, then frees itself. Looks only: GroundSurfaces decides where and for how long.
public partial class SurfacePatchView : Node3D
{
    private const float SpreadTime = 0.25f;
    private const float FadeTime = 0.6f;

    // Just clear of the floor, so the floor does not cut through it.
    private const float OverFloor = 0.03f;

    // Fire's tongues and ice's shards, so many to a metre of radius.
    private const float FlamesPerMetre = 4f;
    private const float ShardsPerMetre = 3.5f;

    private static readonly Color Scorch = new(0.16f, 0.08f, 0.04f, 0.6f);
    private static readonly Color Smoulder = new(1f, 0.35f, 0.08f, 0.35f);
    private static readonly Color Ember = new(1f, 0.5f, 0.15f);
    private static readonly Color FireLight = new(1f, 0.45f, 0.15f);
    private static readonly Color IceSheet = new(0.55f, 0.82f, 1f, 0.7f);

    private readonly SurfaceKind _kind;
    private readonly float _radius;
    private readonly List<(Node3D Flame, float Height, float Phase, float Rate)> _flames = new();
    private readonly List<Node3D> _shards = new();
    private readonly List<StandardMaterial3D> _fading = new();
    private readonly List<float> _alphas = new();
    private OmniLight3D? _light;
    private float _left;
    private float _age;

    public SurfacePatchView(SurfaceKind kind, float radius, float lasts)
    {
        _kind = kind;
        _radius = radius;
        _left = lasts;
    }

    // Godot needs a parameterless constructor to instantiate script classes itself.
    public SurfacePatchView()
    {
    }

    public event Action? Gone;

    // It lasts this much longer from now; none makes it fade at once.
    public void Renew(float lasts) => _left = Mathf.Max(lasts, 0f);

    public override void _Ready()
    {
        Scale = Vector3.One * 0.01f;
        if (_kind == SurfaceKind.Burning)
        {
            BuildFire();
        }
        else
        {
            BuildIce();
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        _left -= dt;
        if (_left <= -FadeTime)
        {
            Gone?.Invoke();
            QueueFree();
            return;
        }

        float spread = Mathf.Min(1f, _age / SpreadTime);
        Scale = Vector3.One * Mathf.Max(0.01f, 1f - (1f - spread) * (1f - spread));
        float shown = Mathf.Clamp(1f + _left / FadeTime, 0f, 1f);
        for (int i = 0; i < _fading.Count; i++)
        {
            _fading[i].AlbedoColor = _fading[i].AlbedoColor with { A = _alphas[i] * shown };
        }

        foreach (var (flame, height, phase, rate) in _flames)
        {
            float flicker = 0.75f + 0.25f * Mathf.Sin(_age * rate + phase) + 0.1f * Mathf.Sin(_age * rate * 2.7f + phase * 3f);
            flame.Scale = new Vector3(1f, Mathf.Max(0.01f, height * flicker * shown), 1f);
        }

        foreach (var shard in _shards)
        {
            shard.Scale = Vector3.One * Mathf.Max(0.01f, shown);
        }

        if (_light != null)
        {
            _light.LightEnergy = (1.4f + 0.3f * Mathf.Sin(_age * 17f) + 0.2f * Mathf.Sin(_age * 7.3f)) * shown;
        }
    }

    private void BuildFire()
    {
        AddChild(Disc(Scorch));

        // The scorched ground still glows from within, brightest in the middle.
        var glow = Disc(Smoulder);
        ((StandardMaterial3D)glow.MaterialOverride).BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        glow.Scale = Vector3.One * 0.8f;
        glow.Position += Vector3.Up * 0.01f;
        AddChild(glow);
        int flames = Mathf.Max(3, Mathf.RoundToInt(_radius * FlamesPerMetre));
        for (int i = 0; i < flames; i++)
        {
            float width = ElementLooks.Jitter(0.16f, 0.3f);
            // Held by its foot, so it flickers up and down from the ground.
            var flame = ElementLooks.Made(Element.Fire, new CylinderMesh { TopRadius = 0f, BottomRadius = width, Height = 1f, RadialSegments = 8, Rings = 1 });
            flame.Position = Vector3.Up * 0.5f;
            var foot = new Node3D { Position = ElementLooks.SpotWithin(_radius * 0.85f) + Vector3.Up * OverFloor };
            foot.AddChild(flame);
            AddChild(foot);
            _flames.Add((foot, ElementLooks.Jitter(0.8f, 0.4f), GD.Randf() * Mathf.Tau, ElementLooks.Jitter(9f, 0.3f)));
        }

        AddChild(Embers());
        _light = new OmniLight3D { LightColor = FireLight, LightEnergy = 1.4f, OmniRange = _radius * 2.2f, ShadowEnabled = false, Position = Vector3.Up * 0.8f };
        AddChild(_light);
    }

    private void BuildIce()
    {
        var sheet = Disc(IceSheet);
        var material = (StandardMaterial3D)sheet.MaterialOverride;
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
        material.Roughness = 0.05f;
        material.Metallic = 0.1f;
        material.MetallicSpecular = 1f;
        sheet.MaterialOverlay = ElementLooks.Overlay(Element.Ice);
        AddChild(sheet);
        int shards = Mathf.Max(3, Mathf.RoundToInt(_radius * ShardsPerMetre));
        for (int i = 0; i < shards; i++)
        {
            float height = ElementLooks.Jitter(0.35f, 0.4f);
            var shard = ElementLooks.Made(Element.Ice, SpellMeshes.Bipyramid(ElementLooks.Jitter(0.08f, 0.3f), height));
            shard.Position = ElementLooks.SpotWithin(_radius * 0.9f) + Vector3.Up * (height * 0.25f);
            shard.Rotation = new Vector3((GD.Randf() - 0.5f) * 0.8f, GD.Randf() * Mathf.Tau, (GD.Randf() - 0.5f) * 0.8f);
            AddChild(shard);
            _shards.Add(shard);
        }
    }

    // The ground under it, coloured, soft at its edge: a radial fall-off on a square.
    private MeshInstance3D Disc(Color colour)
    {
        var fallOff = new Gradient();
        fallOff.SetColor(0, Colors.White);
        fallOff.SetColor(1, Colors.White with { A = 0f });
        fallOff.AddPoint(0.7f, Colors.White);
        var material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = colour,
            AlbedoTexture = new GradientTexture2D
            {
                Gradient = fallOff,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(1f, 0.5f),
                Width = 128,
                Height = 128,
            },
        };
        _fading.Add(material);
        _alphas.Add(colour.A);
        return new MeshInstance3D
        {
            Name = "Ground",
            Mesh = new PlaneMesh { Size = new Vector2(_radius * 2f, _radius * 2f) },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = Vector3.Up * OverFloor,
        };
    }

    private CpuParticles3D Embers() => new()
    {
        Name = "Embers",
        Amount = Mathf.Max(8, Mathf.RoundToInt(_radius * 10f)),
        Lifetime = 0.9f,
        Mesh = new SphereMesh { Radius = 0.03f, Height = 0.06f, RadialSegments = 6, Rings = 3 },
        MaterialOverride = MagicMaterials.Flare(Ember, alpha: 0.9f, energy: 3f),
        EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
        EmissionBoxExtents = new Vector3(_radius * 0.7f, 0.05f, _radius * 0.7f),
        Direction = Vector3.Up,
        Spread = 25f,
        Gravity = new Vector3(0f, 0.6f, 0f),
        InitialVelocityMin = 0.6f,
        InitialVelocityMax = 1.5f,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };
}
