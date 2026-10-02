using Godot;

namespace WarriorsOfEverdawn.Main;

// Everdawn's light and atmosphere (its ForestArena and EnvironmentDefaults), so both games are lit alike: a warm,
// low sun with soft shadows; the sky for ambient light and for what metal reflects; a thin haze; filmic tones,
// shading in the creases, a little bloom, and the far distance out of focus.
public static class WorldLook
{
    // The field is some 80 long where Everdawn's arena is 20 across: shadows are drawn this far from the camera
    // and no further, so what they have of detail goes on what is in view.
    private const float ShadowsWithin = 60f;

    public static void Apply(WorldEnvironment world, DirectionalLight3D sun)
    {
        var environment = world.Environment;
        environment.AmbientLightSource = Environment.AmbientSource.Sky;
        environment.AmbientLightSkyContribution = 1f;
        environment.AmbientLightEnergy = 0.3f;
        environment.ReflectedLightSource = Environment.ReflectionSource.Sky;

        environment.FogEnabled = true;
        environment.FogLightColor = new Color(0.55f, 0.58f, 0.55f);
        environment.FogLightEnergy = 0.2f;
        environment.FogDensity = 0.012f;
        environment.FogSkyAffect = 0.25f;

        environment.TonemapMode = Environment.ToneMapper.Aces;
        environment.TonemapExposure = 1f;
        environment.TonemapWhite = 6f;
        environment.SsaoEnabled = true;
        environment.SsaoRadius = 1.5f;
        environment.SsaoIntensity = 1f;
        environment.GlowEnabled = true;
        environment.GlowIntensity = 0.6f;
        environment.GlowBloom = 0.05f;

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
