// Lightning overlay — one pass, instantiated 3 times with different Color + noise_scale.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled;

uniform vec4  tint        : source_color = vec4(0.75, 0.85, 1.0, 1.0);
uniform float speed       : hint_range(0.1, 15.0) = 6.0;
uniform float noise_scale : hint_range(0.5, 12.0) = 3.0;
uniform float dissolve    : hint_range(0.0, 1.0)  = 0.5;
uniform float time_offset : hint_range(0.0, 100.0) = 0.0;
uniform float bolt_threshold : hint_range(0.3, 0.8) = 0.52;
uniform float ridge_power      : hint_range(1.0, 6.0) = 2.5;
uniform float ridge2_power     : hint_range(1.0, 6.0) = 3.5;
uniform float flicker_power    : hint_range(1.0, 6.0) = 3.5;
uniform float rim_power        : hint_range(0.5, 6.0) = 1.5;
uniform float emission_gain    : hint_range(0.5, 6.0) = 1.0;

#include "res://assets/shaders/noise_lib.gdshaderinc"

void fragment() {
    float t = (TIME + time_offset) * speed;
    // max(0, sin) instead of abs(sin): period is 2π (not π), so the 0.58s anti-phase offset
    // applied to the off-hand weapon correctly lands at half-period (π shift), not full-period.
    float flicker = pow(max(0.0, sin(t * 0.9 + UV.y * 0.4)), flicker_power);

    // Ridged noise -- transforms smooth blobs into sharp zigzag ridges/veins that
    // read as angular lightning filaments instead of soft round patches.
    float raw = noise(UV * noise_scale + vec2(t * 0.08, t * 0.11));
    float ridge = 1.0 - abs(raw * 2.0 - 1.0);
    ridge = pow(ridge, ridge_power);

    // Secondary high-frequency ridge for jagged breakup at the edges.
    float raw2 = noise(UV * noise_scale * 2.1 + vec2(-t * 0.15, t * 0.09));
    float ridge2 = 1.0 - abs(raw2 * 2.0 - 1.0);
    ridge2 = pow(ridge2, ridge2_power);

    float n = ridge * (0.65 + ridge2 * 0.7);
    float bolt = step(bolt_threshold, n) * flicker;

    float rim = rimFactor(NORMAL, VIEW, rim_power);
    RIM = 2.0;
    float combined = clamp(bolt + rim * 0.18 * (flicker * 0.5 + 0.5), 0.0, 1.0);

    float threshold = 1.0 - dissolve;
    if (combined < threshold) discard;

    float strength = (combined - threshold) / max(dissolve, 0.001);
    strength = clamp(strength, 0.0, 1.0);

    ALBEDO   = tint.rgb;
    ALPHA    = strength * 0.78;
    EMISSION = tint.rgb * strength * emission_gain * (0.7 + flicker * 0.9);
}
