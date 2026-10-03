using EverdawnKit;
using Godot;
using WarriorsOfEverdawn.Core.Combat;
using WarriorsOfEverdawn.Util;

namespace WarriorsOfEverdawn.Character;

// The barrier a staff raises, as every machine draws it: Everdawn's honeycomb (the kit's barrier_hex) on a shell round the
// caster, in the colour of the staff's element, thinning as the barrier is spent and gone when it gives. Looks only:
// what the barrier takes is the host's business (PlayerVitals).
public partial class BarrierBubble : MeshInstance3D
{
    private const string ShaderPath = KitPaths.BarrierShader;
    private const float Radius = 1.25f;
    private const float Height = 1f;

    // The shader's `dissolve` with the barrier whole and with it all but spent: how much of the honeycomb shows.
    // Much more and the cells fill in, and the shell hides whoever stands in it.
    private const float FullDissolve = 0.3f;
    private const float SpentDissolve = 0.14f;

    private readonly ShaderMaterial _material = new();

    public BarrierBubble()
    {
        Name = "Barrier";
        Mesh = new SphereMesh { Radius = Radius, Height = Radius * 2f, RadialSegments = 32, Rings = 16 };
        Position = Vector3.Up * Height;
        CastShadow = ShadowCastingSetting.Off;
        Visible = false;
    }

    public override void _Ready()
    {
        _material.Shader = Assets.Load<Shader>(ShaderPath);
        _material.SetShaderParameter("hex_scale", 12f);

        // The shader was written for a plate that fades toward its rim; a shell has no rim.
        _material.SetShaderParameter("plate_fade_band", new Vector2(10f, 11f));
        _material.SetShaderParameter("edge_fade_band", new Vector2(0f, 0.05f));

        // Below the range the shader was written for: a shell drawn over the fight has to be seen through.
        _material.SetShaderParameter("emission_gain", 0.2f);
        MaterialOverride = _material;
    }

    // Up, in that element's colour, with this share of it left (0 to 1); down, with none left or no element.
    public void Show(Element? element, float left)
    {
        Visible = element != null && left > 0f;
        if (element is { } of && Visible)
        {
            _material.SetShaderParameter("tint", ElementLooks.For(of).Primary);
            _material.SetShaderParameter("dissolve", Mathf.Lerp(SpentDissolve, FullDissolve, left));
        }
    }
}
