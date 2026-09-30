// Spirit / ghost replacement material — collapses the surface to a single
// tint, modulated by texture luminance. The body is translucent across
// the whole surface (front-facing alpha < 1) so the scene shows through,
// while a soft silhouette feather drops alpha further at the outer rim.
//
// To prevent the chest from blending through the back of the head while
// the body is translucent, the spirit shader is paired with
// spirit_depth.gdshader as a NextPass parent. The depth-only prepass
// writes depth across the whole character before any color blending,
// so the spirit pass here renders only the front-most fragment per
// pixel — see CharacterAssembler.ApplySpiritRecursive for the chain
// setup and render_priority.
//
// Applied via MeshInstance3D.MaterialOverride so the underlying
// SurfaceOverrideMaterial chain (toon + outline) is bypassed: the body
// itself becomes translucent, not just an additive layer over an opaque
// toon surface.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded;

uniform vec4      tint            : source_color = vec4(0.55, 0.95, 1.0, 1.0);
uniform vec4      albedo_color    : source_color = vec4(1.0);
uniform sampler2D albedo_tex      : source_color, hint_default_white, filter_linear;
uniform float     opacity_edge    : hint_range(0.0, 1.0) = 0.15;
uniform float     opacity_core    : hint_range(0.0, 1.0) = 0.45;
uniform float     edge_band       : hint_range(0.01, 0.5) = 0.15;
uniform float     glow_strength   : hint_range(0.0, 4.0) = 0.9;
uniform float     lum_floor       : hint_range(0.0, 1.0) = 0.05;
uniform float     halo_strength   : hint_range(0.0, 6.0) = 3.0;
uniform float     halo_power      : hint_range(0.5, 8.0) = 2.5;
uniform float     fade            : hint_range(0.0, 1.0) = 1.0;

void fragment() {
    vec4 tex = texture(albedo_tex, UV) * albedo_color;
    float lum = max(dot(tex.rgb, vec3(0.299, 0.587, 0.114)), lum_floor);

    float ndotv = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);

    // Front-facing body: texture luminance × tint, kept below the HDR
    // bloom threshold so face details (eyes, beard, mouth) read clearly
    // instead of being blown out by glow.
    float core = smoothstep(0.0, edge_band, ndotv);
    vec3  body = tint.rgb * lum * glow_strength;

    // Silhouette halo: high-value rim (above 1.0) so the world
    // environment's glow post-process picks it up as bloom — gives the
    // "lit from within" emission like the skeleton-eye glow but only at
    // the body's outer edge, leaving the front face untouched. Unshaded
    // skips EMISSION, so the halo is added directly to ALBEDO.
    float rim   = pow(1.0 - ndotv, halo_power);
    vec3  halo  = tint.rgb * rim * halo_strength;

    ALBEDO = body + halo;
    ALPHA  = mix(opacity_edge, opacity_core, core) * tint.a * tex.a * fade;
}
