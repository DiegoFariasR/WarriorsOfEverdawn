// Fire / elemental overlay — one pass, instantiated 3 times with different Color + noise_scale.
// Uses discard cutout for crisp dissolve edges.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled;

uniform vec4  tint        : source_color = vec4(1.0, 0.45, 0.05, 1.0);
uniform float speed       : hint_range(0.1, 8.0)  = 1.5;
uniform float noise_scale : hint_range(0.5, 12.0) = 3.0;  // controls pattern coarseness
uniform float dissolve    : hint_range(0.0, 1.0)  = 0.5;  // overall opacity / dissolve
uniform float time_offset : hint_range(0.0, 100.0) = 0.0; // anti-phase offset for off-hand weapon
uniform float rim_power     : hint_range(0.5, 6.0) = 2.0;
uniform float emission_gain : hint_range(0.5, 6.0) = 1.0; // overall emission scale; bumps bloom on fire

#include "res://assets/shaders/noise_lib.gdshaderinc"

void fragment() {
    float t = TIME + time_offset;
    // Scroll downward (fire falls); 1:3 UV stretch makes patterns tall and flame-like
    vec2 uv = UV * vec2(1.0, 3.0) * noise_scale + vec2(0.0, t * speed * 0.25);
    float n = fbm2Rotated(uv, 3, 0.5);

    // Crisp cutout like FantasyTactics ALPHA_SCISSOR_THRESHOLD
    float threshold = 1.0 - dissolve;
    if (n < threshold) discard;

    // Built-in rim lighting for glowing edges
    RIM = 2.0;
    // Manual rim for dissolve strength modulation (post-discard to skip discarded pixels)
    float rim = rimFactor(NORMAL, VIEW, rim_power);

    float strength = (n - threshold) / max(dissolve, 0.001);
    strength = clamp(strength * (rim * 0.5 + 0.7), 0.0, 1.0);

    ALBEDO   = tint.rgb;
    ALPHA    = strength;
    EMISSION = tint.rgb * strength * emission_gain * (1.0 + rim * 1.5);
}
