// A cut gem lit from within: every facet takes its own colour, the colours drift as the gem turns and as time passes,
// and the facets turned to the eye burn toward white. Unshaded, so it shines the same in shadow.
shader_type spatial;
render_mode unshaded, cull_back;

// Seconds for one trip round the colours; MagicOrb.ColourCycle.
uniform float cycle = 6.0;
// How far apart in colour neighbouring facets are.
uniform float spread = 1.3;
uniform float heart : hint_range(0.0, 1.0) = 0.55;
uniform float rim : hint_range(0.0, 2.0) = 0.6;

vec3 hue(float h) {
    return clamp(abs(mod(h * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
}

void fragment() {
    // The facet's own normal, not the mesh's smoothed one: each face reads as one flat cut.
    vec3 facet = normalize(cross(dFdx(VERTEX), dFdy(VERTEX)));
    if (dot(facet, VIEW) < 0.0) {
        facet = -facet;
    }

    float facing = clamp(dot(facet, normalize(VIEW)), 0.0, 1.0);
    vec3 world_facet = (INV_VIEW_MATRIX * vec4(facet, 0.0)).xyz;
    float h = fract(dot(world_facet, vec3(0.37, 0.61, 0.71)) * spread + TIME / cycle);
    vec3 colour = mix(hue(h), vec3(1.0), 0.12);

    // Only the facets turned straight at the eye flash white; the rest keep their colour.
    ALBEDO = mix(colour, vec3(1.0), pow(facing, 6.0) * heart) * (1.0 + pow(1.0 - facing, 2.0) * rim);
}
