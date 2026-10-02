// Void / corruption overlay — swirling dark vortex that consumes the character's surface.
// blend_mix: overlays near-black so the character appears darkened / absorbed into void.
// Violet-magenta emission at edges gives the corrupted outline.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled;

uniform vec4  tint        : source_color = vec4(0.38, 0.0, 0.65, 1.0);
uniform float speed       : hint_range(0.1, 5.0) = 1.2;
uniform float noise_scale : hint_range(0.25, 4.0) = 1.0;
uniform float dissolve    : hint_range(0.0, 1.0) = 0.55;
uniform float tendril_fade_start : hint_range(0.0, 0.5) = 0.15;
uniform float tendril_fade_end   : hint_range(0.2, 1.0) = 0.48;
uniform float time_offset : hint_range(0.0, 100.0) = 0.0;
uniform float warp_strength : hint_range(0.0, 1.0) = 0.28;
uniform float rim_power     : hint_range(0.5, 6.0) = 1.8;
uniform float emission_gain : hint_range(0.5, 6.0) = 1.0;
uniform float rim_discard_below : hint_range(0.0, 0.5) = 0.22;

#include "res://assets/shaders/noise_lib.gdshaderinc"

void fragment() {
    float t = (TIME + time_offset) * speed;
    vec2 uv = UV;

    // Domain warp: two FBM passes offset by each other create a swirling spiral pull.
    float wx = fbm2(uv * 3.0 * noise_scale + vec2(t * 0.4,  t * 0.15), 4);
    float wy = fbm2(uv * 3.0 * noise_scale + vec2(wx + t * 0.2, t * 0.3), 4);
    vec2 warped = uv + vec2(wx, wy) * warp_strength;

    float n = fbm2(warped * 4.5 * noise_scale + vec2(-t * 0.55, 0.0), 4);

    float rim = rimFactor(NORMAL, VIEW, rim_power);
    RIM = 2.5;

    float threshold = 1.0 - dissolve;
    if (n < threshold && rim < rim_discard_below) discard;

    // Secondary tendril noise -- different frequency, direction, and speed so the
    // black streaks move independently of the base purple swirl. Computed post-discard.
    float tendril = fbm2(uv * 6.5 * noise_scale + vec2(-t * 0.3, t * 0.45), 4);

    float strength = max((n - threshold) / max(dissolve, 0.001), rim * 0.5);
    strength = clamp(strength, 0.0, 1.0);

    // Pure black dominates; tint only whispers through the brightest noise peaks / rim.
    vec3 col = mix(vec3(0.0), tint.rgb, n * 0.14 + rim * 0.18);

    // Overlay black tendrils where the secondary noise is strong -- ink-streak effect.
    float tendrilMask = smoothstep(tendril_fade_start, tendril_fade_end, tendril);
    col = mix(col, vec3(0.0), tendrilMask);

    ALBEDO   = col;
    ALPHA    = strength * 0.88;
    EMISSION = tint.rgb * rim * strength * emission_gain * 1.1 * (1.0 - tendrilMask * 0.97);
}
