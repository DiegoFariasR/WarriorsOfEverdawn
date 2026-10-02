// Barrier hex pattern — precise honeycomb grid overlay.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled;

uniform vec4  tint        : source_color = vec4(0.85, 0.92, 1.0, 1.0);
uniform float speed       : hint_range(0.1, 5.0) = 0.8;
uniform float dissolve    : hint_range(0.0, 1.0) = 0.55;
uniform float hex_scale   : hint_range(1.0, 20.0) = 8.0;
uniform float edge_softness : hint_range(0.005, 0.10) = 0.04;
uniform float pulse_freq    : hint_range(2.0, 16.0) = 8.0;
uniform float pulse_speed   : hint_range(0.5, 6.0) = 2.0;
uniform float emission_gain : hint_range(0.5, 6.0) = 1.0;
// Grazing-angle silhouette fade band (smoothstep edges on n-dot-V). Lower lo = more
// dome edge transparent; higher hi = larger viewer-facing solid area.
uniform vec2  edge_fade_band  = vec2(0.05, 0.45);
// Radial UV falloff toward the dome lip. distFromC is normalised 0..1 across UV span.
uniform vec2  plate_fade_band = vec2(0.20, 0.95);

#include "res://assets/shaders/noise_lib.gdshaderinc"

// Exact signed distance to a flat-top regular hexagon (Inigo Quilez).
// r = inradius (center to edge midpoint).
float sdHexagon(vec2 p, float r) {
    const vec3 k = vec3(-0.866025, 0.5, 0.577350);
    p = abs(p);
    p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
    p -= vec2(clamp(p.x, -k.z * r, k.z * r), r);
    return length(p) * sign(p.y);
}

void fragment() {
    float t = TIME * speed;

    // Scale controls hex density. Flip V so pattern reads correctly on hemisphere.
    vec2 uv = vec2(UV.x, 1.0 - UV.y) * hex_scale;

    // Hex lattice tiling: two offset rectangular grids.
    const vec2 s = vec2(1.7320508, 1.0); // (sqrt3, 1) for flat-top
    const vec2 h = s * 0.5;

    // Grid A and Grid B (offset by half cell).
    vec2 a = mod(uv, s) - h;
    vec2 b = mod(uv + h, s) - h;

    // Pick nearest hex center.
    vec2 cell = dot(a, a) < dot(b, b) ? a : b;

    // Hex SDF: negative inside, positive outside. Inradius = 0.5.
    float d = sdHexagon(cell, 0.5);

    // Edge line: thin bright border at the hex boundary (d near 0).
    float edge = 1.0 - smoothstep(-edge_softness, 0.0, d);

    // Upward energy sweep (flipped V to match hemisphere).
    float pulse = sin((1.0 - UV.y) * pulse_freq - t * pulse_speed) * 0.5 + 0.5;
    pulse = pow(pulse, 3.0);

    // Fresnel rim. Reuse n-dot-V (rimFactor recomputes the same dot internally).
    float nDotV = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
    float rim   = pow(1.0 - nDotV, 2.5);

    // Shield edge fade: grazing-angle surfaces soften to transparent so the dome
    // reads as a curved plate with feathered edges rather than a closed bubble.
    float edgeFade = smoothstep(edge_fade_band.x, edge_fade_band.y, nDotV);

    // Plate edge fade: radial UV falloff so the rim fades to transparent,
    // leaving the center of the disk most visible.
    vec2  centered   = UV - vec2(0.5);
    float distFromC  = length(centered) * 2.0;
    float plateFade  = 1.0 - smoothstep(plate_fade_band.x, plate_fade_band.y, distFromC);

    float mask = edgeFade * plateFade;

    float combined = edge * 0.9 + rim * 0.3 + pulse * edge * 0.2;
    float threshold = 1.0 - dissolve;
    if (combined < threshold) discard;

    float strength = clamp((combined - threshold) / max(dissolve, 0.001), 0.0, 1.0);

    vec3 col = mix(tint.rgb, vec3(1.0), edge * 0.5);

    ALBEDO   = col;
    ALPHA    = strength * 0.35 * mask;
    EMISSION = col * strength * mask * emission_gain * (1.2 + pulse * 1.0 + rim * 0.4);
}
