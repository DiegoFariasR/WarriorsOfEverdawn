// Water overlay -- flowing liquid surface with caustic highlights, sea-blue / teal.
// Distinct from elemental_ice: no Voronoi facets, no crack lines, smooth curving
// waves that drift across the surface. Specular peaks read as caustic ripples.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled;

uniform vec4  tint        : source_color = vec4(0.10, 0.45, 0.70, 1.0);
uniform float speed       : hint_range(0.0, 4.0)  = 0.8;
uniform float noise_scale : hint_range(0.5, 12.0) = 3.5;
uniform float dissolve    : hint_range(0.0, 1.0)  = 0.6;
uniform float time_offset : hint_range(0.0, 100.0) = 0.0;
uniform vec3  caustic_highlight : source_color = vec3(0.55, 0.85, 0.95);
uniform vec2  caustic_band      = vec2(0.55, 0.78);
uniform float caustic_power     : hint_range(1.0, 4.0) = 2.0;
uniform float rim_power         : hint_range(0.5, 6.0) = 1.4;
// rim threshold below which a sub-threshold combined value still discards;
// raise for thicker silhouette, lower for cleaner edges.
uniform float rim_discard_below : hint_range(0.0, 0.5) = 0.10;

#include "res://assets/shaders/noise_lib.gdshaderinc"

void fragment() {
    float t = (TIME + time_offset) * speed;

    // Two slow-flowing fbm fields drifting in opposite diagonals -- their
    // intersection produces rolling caustic-like ripples instead of static blobs.
    vec2  uvA = UV * noise_scale + vec2(t * 0.18, t * 0.30);
    vec2  uvB = UV * noise_scale * 1.7 + vec2(-t * 0.15, t * 0.22);
    float a   = fbm2(uvA, 3);
    float b   = fbm2(uvB, 2);
    float waves = a * 0.6 + b * 0.4;

    // Caustic peaks: thin bright bands where the wave field crosses a high
    // threshold. Squared falloff narrows the bands so they read as light
    // refractions rather than fat blobs.
    float caustic = smoothstep(caustic_band.x, caustic_band.y, waves);
    caustic       = pow(caustic, caustic_power);

    // Body fill: smooth gradient mid-band so the surface stays mostly visible
    // (water shouldn't dissolve into nothing the way ice does between facets).
    float fill = smoothstep(0.18, 0.62, waves);

    float rim = rimFactor(NORMAL, VIEW, rim_power);
    RIM = 1.0;

    float combined  = clamp(fill * 0.55 + caustic + rim * 0.25, 0.0, 1.0);
    float threshold = 1.0 - dissolve;
    if (combined < threshold && rim < rim_discard_below) discard;

    float strength = clamp((combined - threshold) / max(dissolve, 0.001), 0.0, 1.0);

    // Lift toward sea-foam highlight on caustic peaks; otherwise keep the
    // body in the deep-blue tint. The mix is gentler than ice's crack-line
    // mix so peaks blend rather than pop as hard lines.
    vec3 col = mix(tint.rgb, caustic_highlight, caustic * 0.65 + rim * 0.20);

    ALBEDO    = col;
    ALPHA     = strength * 0.78;
    EMISSION  = col * (caustic * 0.40 + rim * 0.15) * strength;
    SPECULAR  = 0.7;
    ROUGHNESS = 0.22;
}
