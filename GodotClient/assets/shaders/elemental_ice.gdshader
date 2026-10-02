// Ice / frost overlay — Voronoi crystal facets with hard-edged angular crack lines.
// Technique: Chebyshev distance Voronoi (angular square cells) + Inigo Quilez
// perpendicular bisector edge detection (consistent-width straight crack lines).
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled;

uniform vec4  tint        : source_color = vec4(0.55, 0.85, 1.0, 1.0);
uniform float speed       : hint_range(0.0, 2.0)  = 0.0;
uniform float noise_scale : hint_range(1.0, 12.0) = 5.0;
uniform float dissolve    : hint_range(0.0, 1.0)  = 0.70;
uniform float crack_width : hint_range(0.01, 0.1) = 0.05;
uniform float time_offset : hint_range(0.0, 100.0) = 0.0;
uniform float crack_power : hint_range(0.5, 4.0) = 1.5;
uniform float rim_power   : hint_range(0.5, 6.0) = 2.0;
uniform float rim_discard_below : hint_range(0.0, 0.5) = 0.12;

#include "res://assets/shaders/noise_lib.gdshaderinc"

// Returns vec2(cell_dist, edge_dist).
// cell_dist: Chebyshev distance to nearest seed (0 at center, ~0.5 at boundary).
// edge_dist: perpendicular distance to nearest facet boundary line.
vec2 voronoiCrystal(vec2 uv) {
    vec2 p = floor(uv);
    vec2 f = fract(uv);

    // Pass 1 — find closest seed (Chebyshev metric -> square/angular cells).
    float minDist = 8.0;
    vec2  minSeed = vec2(0.0);
    vec2  minCell = vec2(0.0);

    for (int j = -1; j <= 1; j++) {
        for (int i = -1; i <= 1; i++) {
            vec2 cell = vec2(float(i), float(j));
            // Nudge seeds inward from cell edges to avoid degenerate boundaries.
            vec2 seed = cell + hash2(p + cell) * 0.7 + 0.15;
            vec2 diff = seed - f;
            float dist = max(abs(diff.x), abs(diff.y)); // Chebyshev
            if (dist < minDist) {
                minDist = dist;
                minSeed = seed;
                minCell = cell;
            }
        }
    }

    // Pass 2 — perpendicular bisector distance (Quilez method).
    float edgeDist = voronoiBisectorDist(minSeed, minCell, f, p, 0.7, 0.15);
    return vec2(minDist, edgeDist);
}

void fragment() {
    vec2 uv = UV * noise_scale + vec2(0.0, (TIME + time_offset) * speed * 0.05);

    vec2  v        = voronoiCrystal(uv);
    float cell     = v.x; // 0 at seed center, ~0.5 at Chebyshev boundary
    float edge     = v.y; // 0 on crack line, grows away

    // Thin bright crack lines at facet boundaries.
    float crack = 1.0 - smoothstep(0.0, crack_width, edge);
    crack = pow(crack, crack_power);

    // Cell interior: bright at center, fades toward boundary.
    float interior = 1.0 - clamp(cell * 2.2, 0.0, 1.0);

    float rim = rimFactor(NORMAL, VIEW, rim_power);
    RIM = 1.5;

    float base      = interior * 0.55 + crack * 0.55 + rim * 0.3;
    float threshold = 1.0 - dissolve;
    if (base < threshold && rim < rim_discard_below) discard;

    float strength = clamp((base - threshold) / max(dissolve, 0.001), 0.0, 1.0);

    // Crack lines pale icy blue (not white); cell interiors use the layer tint.
    vec3 col = mix(tint.rgb, vec3(0.62, 0.82, 1.0), crack * 0.85 + interior * 0.25);

    ALBEDO    = col;
    ALPHA     = strength;
    EMISSION  = col * (crack * 0.5 + rim * 0.3) * strength;
    SPECULAR  = 0.95;
    ROUGHNESS = 0.05;
}
