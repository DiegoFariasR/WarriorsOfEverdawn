// Depth-only prepass for the spirit overlay. Writes depth across the
// character's surface without contributing any color, so the translucent
// spirit shader linked via next_pass can depth-test against it and only
// render the front-most fragment per pixel — preventing the chest from
// blending through the back of the head when the body itself is
// translucent.
//
// blend_premul_alpha + ALBEDO=0, ALPHA=0 leaves the framebuffer color
// unchanged (final = src.rgb + dst.rgb * (1 - src.a) = 0 + dst*1 = dst).
// depth_draw_always still writes depth even though the material is
// "transparent". A low render_priority on this material (set in
// CharacterAssembler.ApplySpiritRecursive) clusters all depth-prepass
// draws ahead of the spirit-color next_pass in the transparent queue,
// so depth is established across all character meshes before any color
// blending begins.
shader_type spatial;
render_mode blend_premul_alpha, depth_draw_always, cull_back, unshaded;

uniform float fade : hint_range(0.0, 1.0) = 1.0;

void fragment() {
    // When the ghost is faded out, don't write depth — otherwise the
    // invisible body still occludes the scene and leaves a hole.
    if (fade < 0.05) discard;
    ALBEDO = vec3(0.0);
    ALPHA  = 0.0;
}
