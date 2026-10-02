// Renders the back-faces of a mesh pushed out along normals, creating a toon outline.
// Apply as NextPass on the main material.
shader_type spatial;
render_mode cull_front, unshaded, depth_draw_opaque;

uniform float thickness : hint_range(0.0, 0.10) = 0.02;
// BattlePresenter's screen-space toon shader floors brightness at 0.12, so any
// outline color below that gets pinned to flat 0.12 grey. The displayed color is
// what survives that clamp -- pure black (0,0,0) shows up identically to (0.12,
// 0.12, 0.12) in battle, so authoring matches the post-process to keep outline
// appearance consistent across both rendered surfaces (battle + creator).
uniform vec4 color : source_color = vec4(0.04, 0.04, 0.04, 1.0);
// Distance scaling keeps outline a roughly constant pixel width across cameras.
// Battle ~13u, creator ~3u, portraits ~2u; defaults span all three reasonably.
uniform float dist_gain      : hint_range(0.02, 0.30) = 0.1;
uniform float dist_min_scale : hint_range(0.1, 1.0)   = 0.5;
uniform float dist_max_scale : hint_range(1.0, 3.0)   = 1.3;

void vertex() {
    float dist = length(MODELVIEW_MATRIX[3].xyz);
    float scaled = thickness * clamp(dist * dist_gain, dist_min_scale, dist_max_scale);
    VERTEX += NORMAL * scaled;
}

void fragment() {
    ALBEDO = color.rgb;
}
