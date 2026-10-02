// Divine overlay — golden shimmer and light sparkles radiating from the character.
// blend_add: additive so the character glows golden without being covered.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled;

uniform vec4  tint        : source_color = vec4(1.0, 0.82, 0.2, 1.0);
uniform float speed       : hint_range(0.1, 5.0) = 0.9;
uniform float noise_scale : hint_range(0.25, 4.0) = 1.0;
uniform float dissolve    : hint_range(0.0, 1.0) = 0.50;
uniform float sparkle_threshold : hint_range(0.5, 0.9) = 0.73;
uniform float time_offset : hint_range(0.0, 100.0) = 0.0;
uniform float emission_gain : hint_range(0.5, 6.0) = 1.0;
uniform float rim_power     : hint_range(0.5, 6.0) = 1.6;

#include "res://assets/shaders/noise_lib.gdshaderinc"

void fragment() {
    float t = (TIME + time_offset) * speed;
    vec2 uv = UV;

    // Slow shimmer — light drifting upward like heat haze from a divine presence.
    float shimmer = noise(uv * 5.5 * noise_scale + vec2(t * 0.2, -t * 0.7));
    shimmer = pow(shimmer, 1.4);

    // Sparkles — high-frequency noise thresholded to bright points, animated blink.
    float sparkle = noise(uv * 11.0 * noise_scale + vec2(t * 0.6, t * 0.5));
    sparkle = step(sparkle_threshold, sparkle) * (sin(t * 5.0 + uv.y * 9.0 + uv.x * 4.0) * 0.5 + 0.5);

    float rim = rimFactor(NORMAL, VIEW, rim_power);
    RIM = 2.4;

    float combined = shimmer * 0.28 + sparkle * 0.62 + rim * 0.45;
    float threshold = 1.0 - dissolve;
    if (combined < threshold) discard;

    float strength = (combined - threshold) / max(dissolve, 0.001);
    strength = clamp(strength, 0.0, 1.0);

    // White-dominant: sparkles/rim stay pure white, shimmer fill keeps the layer's gold tint.
    float whiteMix = clamp(sparkle * 1.1 + rim * 0.7, 0.0, 1.0);
    vec3 col = mix(tint.rgb, vec3(1.0, 1.0, 0.98), whiteMix);

    ALBEDO   = col;
    ALPHA    = strength;
    EMISSION = col * strength * emission_gain * (2.6 + sparkle * 5.5 + rim * 2.2);
}
