// Earth overlay — rocky plates covering the surface like stone tiles.
// Voronoi cells form interlocking rock boundaries with dark narrow cracks between
// them. Each cell gets a per-id shade and surface noise adds weathered texture.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled;

uniform vec4  tint        : source_color = vec4(0.45, 0.33, 0.20, 1.0);
uniform float speed       : hint_range(0.0, 2.0)  = 0.0;
uniform float noise_scale : hint_range(1.0, 12.0) = 3.5;
uniform float dissolve    : hint_range(0.0, 1.0)  = 0.70;
uniform float crack_width : hint_range(0.01, 0.1) = 0.04;
uniform float time_offset : hint_range(0.0, 100.0) = 0.0;
uniform float crack_power : hint_range(0.5, 4.0) = 1.3;
uniform float rim_power   : hint_range(0.5, 6.0) = 2.0;
// Cell shade range: dark/light scale around tint. Lower dark = deeper shadow plates,
// higher light = brighter peaks; widening this increases plate-to-plate variation.
uniform float stone_dark_scale  : hint_range(0.1, 1.0) = 0.40;
uniform float stone_light_scale : hint_range(1.0, 2.0) = 1.25;

#include "res://assets/shaders/noise_lib.gdshaderinc"

// Euclidean Voronoi with perpendicular bisector edge detection.
// Returns (cell_dist, edge_dist, cell_id).
vec3 voronoiRocks(vec2 uv) {
    vec2 p = floor(uv);
    vec2 f = fract(uv);

    float minDist = 8.0;
    vec2  minSeed = vec2(0.0);
    vec2  minCell = vec2(0.0);
    float minId   = 0.0;

    for (int j = -1; j <= 1; j++) {
        for (int i = -1; i <= 1; i++) {
            vec2 cell = vec2(float(i), float(j));
            vec2 seed = cell + hash2(p + cell);
            vec2 diff = seed - f;
            float dist = length(diff);
            if (dist < minDist) {
                minDist = dist;
                minSeed = seed;
                minCell = cell;
                minId   = hash(p + cell);
            }
        }
    }

    // Perpendicular bisector edge distance — consistent-width lines between plates.
    float edgeDist = voronoiBisectorDist(minSeed, minCell, f, p, 1.0, 0.0);
    return vec3(minDist, edgeDist, minId);
}

void fragment() {
    vec2 uv = UV * noise_scale;
    vec3 v = voronoiRocks(uv);
    float edge   = v.y;
    float cellId = v.z;

    // Narrow dark crack lines at plate boundaries.
    float crack = 1.0 - smoothstep(0.0, crack_width, edge);
    crack = pow(crack, crack_power);

    // Weathered noise texture varying across each plate surface.
    float weather = noise(uv * 3.0) * 0.40 + 0.60;

    // Sub-cell roughness noise -- adds small-scale grit within each rock.
    float grit = noise(uv * 7.0) * 0.25 + 0.75;

    float rim = rimFactor(NORMAL, VIEW, rim_power);
    RIM = 1.3;

    // Each Voronoi cell mixes between a dark and light shade of the tint color
    // based on its randomly-assigned id.
    vec3 stoneDark  = tint.rgb * stone_dark_scale;
    vec3 stoneLight = tint.rgb * stone_light_scale;
    vec3 crackDark  = tint.rgb * 0.10;

    vec3 stoneColor = mix(stoneDark, stoneLight, cellId) * weather * grit;
    stoneColor += tint.rgb * rim * 0.35;

    vec3 col = mix(stoneColor, crackDark, crack);

    // Coverage: stone surface shows where cells are solid; rim helps silhouette.
    float cover = (1.0 - crack * 0.5) * weather + rim * 0.3;
    float threshold = 1.0 - dissolve;
    if (cover < threshold) discard;

    float strength = (cover - threshold) / max(dissolve, 0.001);
    strength = clamp(strength, 0.0, 1.0);

    ALBEDO    = col;
    ALPHA     = strength;
    EMISSION  = tint.rgb * rim * 0.12 * strength;
    ROUGHNESS = 0.92;
    SPECULAR  = 0.08;
}
