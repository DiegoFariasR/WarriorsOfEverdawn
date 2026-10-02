// Wind overlay -- horizontal streamlines flowing left-to-right, pale cyan-white.
// Distinct from elemental_lightning: no flicker, no bolt threshold, soft falloff.
// One pass, instantiated 3 times in the overlay chain with different tint/scale/speed.
//
// Anisotropy: noise input is stretched short on X (so each stripe stays roughly
// constant along the X axis) and long on Y (so the field varies vertically into
// many distinct stripes). Time advances in X to make stripes drift sideways.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled;

uniform vec4  tint        : source_color = vec4(0.78, 0.92, 1.0, 1.0);
uniform float speed       : hint_range(0.1, 8.0)  = 2.4;
uniform float noise_scale : hint_range(0.5, 12.0) = 4.0;
uniform float dissolve    : hint_range(0.0, 1.0)  = 0.5;
uniform float time_offset : hint_range(0.0, 100.0) = 0.0;
uniform float streak_power : hint_range(0.5, 4.0) = 1.4;
uniform vec2  streak_band  = vec2(0.55, 0.95);

#include "res://assets/shaders/noise_lib.gdshaderinc"

void fragment() {
    float t = (TIME + time_offset) * speed;

    // Horizontal stripes: many cells along Y, few along X. Time advances X so
    // each stripe drifts sideways without changing its vertical position.
    vec2 streakUv = vec2(UV.x * noise_scale * 0.30 + t * 0.65, UV.y * noise_scale * 1.6);
    float raw     = fbm2(streakUv, 3);
    // Ridged remap: smooth blob noise -> brighter narrow streamlines.
    float ridge   = 1.0 - abs(raw * 2.0 - 1.0);
    float streak  = pow(smoothstep(streak_band.x, streak_band.y, ridge), streak_power);

    // Slow per-stripe brightness drift -- prevents the field looking static.
    float drift = noise(UV * vec2(noise_scale * 0.4, noise_scale * 0.6) + vec2(t * 0.12, 0.0));
    streak = streak * (0.55 + drift * 0.7);

    float rim = rimFactor(NORMAL, VIEW, 1.6);
    RIM = 1.5;
    float combined = clamp(streak + rim * 0.18, 0.0, 1.0);

    float threshold = 1.0 - dissolve;
    if (combined < threshold) discard;

    float strength = clamp((combined - threshold) / max(dissolve, 0.001), 0.0, 1.0);

    ALBEDO   = tint.rgb;
    ALPHA    = strength * 0.55;
    EMISSION = tint.rgb * strength * (0.45 + streak * 0.6);
}
