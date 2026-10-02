// The glow round a magic orb: a sphere added onto the scene, brightest through its middle and gone at its edge, in
// the colour the orb's light is at that moment.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_back;

// Seconds for one trip round the colours; MagicOrb.ColourCycle.
uniform float cycle = 6.0;
uniform float strength : hint_range(0.0, 1.0) = 0.28;
uniform float falloff : hint_range(0.5, 8.0) = 3.0;

vec3 hue(float h) {
    return clamp(abs(mod(h * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
}

void fragment() {
    float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
    ALBEDO = mix(vec3(1.0), hue(fract(TIME / cycle)), 0.75);
    ALPHA = pow(facing, falloff) * strength;
}
