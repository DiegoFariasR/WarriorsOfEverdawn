using Godot;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Main;

// How a magic orb looks where it lies: a cut gem lit from within that floats and turns, its colours drifting, with a
// glow round it, its light thrown on the ground and a few sparks rising. Looks only: Loot decides when it falls and
// who gets it.
public partial class MagicOrb : Node3D
{
    // One trip round the colours takes this long, here and in the orb's shaders.
    public const float ColourCycle = 6f;

    private const string GemShaderPath = "res://assets/shaders/orb_gem.gdshader";
    private const string HaloShaderPath = "res://assets/shaders/orb_halo.gdshader";

    private const float GemRadius = 0.3f;
    private const float HaloRadius = 0.7f;

    // Chest high on a skeleton's coins, so it is seen over them and over a crowd's feet.
    private const float FloatHeight = 0.85f;
    private const float BobHeight = 0.1f;
    private const float BobRate = 2.2f;
    private const float SpinRate = 1.1f;

    // Pale, so the gem keeps a white heart whatever colour it is passing through.
    private const float LightSaturation = 0.5f;
    private const float LightEnergy = 1.8f;
    private const float LightRange = 5f;

    private Node3D _gem = null!;
    private OmniLight3D _light = null!;
    private float _age;

    // The colour an orb's light has at this moment; the HUD's count wears it too.
    public static Color ColourNow() =>
        Color.FromHsv(Mathf.PosMod(Time.GetTicksMsec() / 1000f / ColourCycle, 1f), LightSaturation, 1f);

    public static MagicOrb Create()
    {
        var orb = new MagicOrb();
        orb._gem = new MeshInstance3D
        {
            Name = "Gem",
            // Few faces, so each reads as a cut.
            Mesh = new SphereMesh { Radius = GemRadius, Height = GemRadius * 2f, RadialSegments = 8, Rings = 4 },
            MaterialOverride = Shaded(GemShaderPath),
        };
        orb.AddChild(orb._gem);
        orb._gem.AddChild(new MeshInstance3D
        {
            Name = "Halo",
            Mesh = new SphereMesh { Radius = HaloRadius, Height = HaloRadius * 2f },
            MaterialOverride = Shaded(HaloShaderPath),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        orb._light = new OmniLight3D { Name = "Light", LightEnergy = LightEnergy, OmniRange = LightRange, ShadowEnabled = false };
        orb._gem.AddChild(orb._light);
        orb._gem.AddChild(Sparks());
        orb.Hover(0f);
        return orb;
    }

    public override void _Process(double delta) => Hover((float)delta);

    private void Hover(float delta)
    {
        _age += delta;
        _gem.Position = Vector3.Up * (FloatHeight + Mathf.Sin(_age * BobRate) * BobHeight);
        _gem.Rotation = new Vector3(0.4f, _age * SpinRate, 0f);
        _light.LightColor = ColourNow();
    }

    private static ShaderMaterial Shaded(string shaderPath)
    {
        var material = new ShaderMaterial { Shader = Assets.Load<Shader>(shaderPath) };
        material.SetShaderParameter("cycle", ColourCycle);
        return material;
    }

    private static CpuParticles3D Sparks()
    {
        var fade = new Gradient();
        fade.SetColor(0, Colors.White);
        fade.SetColor(1, new Color(1f, 1f, 1f, 0f));
        return new CpuParticles3D
        {
            Name = "Sparks",
            Amount = 10,
            Lifetime = 1.3f,
            LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = GemRadius * 1.6f,
            Direction = Vector3.Up,
            Spread = 60f,
            InitialVelocityMin = 0.15f,
            InitialVelocityMax = 0.45f,
            Gravity = Vector3.Up * 0.4f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1f,
            ColorRamp = fade,
            Mesh = new QuadMesh
            {
                Size = Vector2.One * 0.07f,
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
                    BillboardKeepScale = true,
                    VertexColorUseAsAlbedo = true,
                },
            },
        };
    }
}
