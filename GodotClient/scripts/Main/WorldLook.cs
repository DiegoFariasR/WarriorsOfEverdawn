using EverdawnKit.Characters;
using Godot;
using WarriorsOfEverdawn.Character;

namespace WarriorsOfEverdawn.Main;

// Light and atmosphere for the field. The sun, the tones, the bloom and the depth of field are Everdawn's (its
// ForestArena and EnvironmentDefaults), so both games are lit alike. The rest follows Kay Lousberg's "Basic
// Environment and Lighting Setup in Godot", which is written for these models: ambient light of a chosen colour
// in place of whatever the sky gives, a haze the colour of the horizon so the far field runs into the sky, shading
// in the creases and light bounced between surfaces, and the contrast turned up a little.
public static class WorldLook
{
    // The field is some 80 long where Everdawn's arena is 20 across: shadows are drawn this far from the camera
    // and no further, so what they have of detail goes on what is in view.
    private const float ShadowsWithin = 60f;

    private static readonly Color SkyAbove = new(0.42f, 0.50f, 0.64f);
    private static readonly Color Horizon = new(0.70f, 0.80f, 0.90f);

    // The shade is the sky's: cooler than the sun, so a figure's lit side reads warm against its own shadow.
    private static readonly Color Shade = new(0.93f, 0.95f, 1f);

    // Everdawn's CharacterFill (its DungeonArena): a light without shadows that looks where the camera looks and
    // reaches the figures alone, so the side of them the camera sees is not left to the shade. Half Everdawn's
    // 0.5: there it is on only in torch-lit rooms, and here the figures stand in the sun, where 0.5 took skin and
    // bone to white.
    private const float CharacterFillEnergy = 0.25f;
    private static readonly Color CharacterFillColour = new(1f, 0.95f, 0.85f);

    public static void Apply(WorldEnvironment world, DirectionalLight3D sun, Camera3D camera)
    {
        camera.AddChild(new DirectionalLight3D
        {
            Name = "CharacterFill",
            LightColor = CharacterFillColour,
            LightEnergy = CharacterFillEnergy,
            LightSpecular = 0f,
            ShadowEnabled = false,
            LightCullMask = 1u << (ToonLook.CharacterLightLayer - 1),
        });

        var environment = world.Environment;
        if (environment.Sky?.SkyMaterial is not ProceduralSkyMaterial sky)
        {
            throw new System.InvalidOperationException($"{world.GetPath()} should have a procedural sky");
        }

        // Below the horizon the sky is the horizon's colour too: the ground plane ends, and what shows past its
        // edge should not be a brown band.
        sky.SkyTopColor = SkyAbove;
        sky.SkyHorizonColor = Horizon;
        sky.GroundHorizonColor = Horizon;
        sky.GroundBottomColor = Horizon;

        // Metal still mirrors the sky; only the flat fill is chosen.
        environment.AmbientLightSource = Environment.AmbientSource.Color;
        environment.AmbientLightColor = Shade;
        environment.AmbientLightEnergy = 0.5f;
        environment.ReflectedLightSource = Environment.ReflectionSource.Sky;

        environment.FogEnabled = true;
        environment.FogLightColor = Horizon;
        environment.FogLightEnergy = 1f;
        environment.FogMode = Environment.FogModeEnum.Depth;
        environment.FogDepthBegin = 35f;
        environment.FogDepthEnd = 150f;
        environment.FogDensity = 0.9f;
        environment.FogSkyAffect = 0f;

        environment.TonemapMode = Environment.ToneMapper.Aces;
        environment.TonemapExposure = 1f;
        environment.TonemapWhite = 6f;
        environment.SsaoEnabled = true;
        environment.SsaoRadius = 1.5f;
        environment.SsaoIntensity = 2f;
        environment.SsilEnabled = true;
        environment.GlowEnabled = true;
        environment.GlowIntensity = 0.6f;
        environment.GlowBloom = 0.05f;
        environment.AdjustmentEnabled = true;
        environment.AdjustmentContrast = 1.15f;

        world.CameraAttributes = new CameraAttributesPractical
        {
            DofBlurFarEnabled = true,
            DofBlurFarDistance = 30f,
            DofBlurFarTransition = 15f,
            DofBlurAmount = 0.05f,
        };

        // Late afternoon, from the upper left: 38 degrees down.
        sun.RotationDegrees = new Vector3(-38f, 55f, 0f);
        sun.LightColor = new Color(1f, 0.82f, 0.52f);
        sun.LightEnergy = 0.9f;
        sun.LightSpecular = 0.35f;
        sun.LightAngularDistance = 0.3f;
        sun.ShadowEnabled = true;
        sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal;
        sun.DirectionalShadowMaxDistance = ShadowsWithin;
        sun.ShadowOpacity = 0.55f;
        sun.ShadowBlur = 1.5f;
    }
}
