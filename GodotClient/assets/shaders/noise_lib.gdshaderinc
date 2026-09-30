// Shared 2D procedural hashes and value noise used by elemental overlay shaders.

float hash(vec2 p) {
    p = fract(p * vec2(127.1, 311.7));
    p += dot(p, p + 19.19);
    return fract(p.x * p.y);
}

// Vector hash for Voronoi seed placement (ice facets, earth rock plates).
vec2 hash2(vec2 p) {
    p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
    return fract(sin(p) * 43758.5453123);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash(i),              hash(i + vec2(1, 0)), f.x),
        mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x),
        f.y);
}

// 2D fbm with axis offset per octave (no per-octave rotation).
float fbm2(vec2 p, int octaves) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < octaves; i++) {
        v += a * noise(p);
        p  = p * 2.1 + vec2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

// 2D fbm with constant per-octave rotation. The rotation prevents axis-aligned
// patterning that betrays the noise grid; useful for organic flame/wave fields.
// Used by elemental_fire (rot=0.5 rad).
float fbm2Rotated(vec2 p, int octaves, float rotRadians) {
    float v = 0.0;
    float a = 0.5;
    float c = cos(rotRadians);
    float s = sin(rotRadians);
    mat2 rot = mat2(vec2(c, s), vec2(-s, c));
    for (int i = 0; i < octaves; i++) {
        v += a * noise(p);
        p  = rot * p * 2.1 + vec2(1.7, 9.2);
        a *= 0.5;
    }
    return v;
}

// Fresnel-style rim factor used by every elemental overlay + barrier shader.
// `power` controls the tightness of the rim falloff (higher = narrower rim).
float rimFactor(vec3 normal, vec3 view, float power) {
    return pow(1.0 - clamp(dot(normal, view), 0.0, 1.0), power);
}

// Perpendicular bisector edge distance (Inigo Quilez) shared by ice (crystal facets)
// and earth (rock plates). Seed placement differs per shader via `seedScale`/`seedBias`.
// Caller already computed `minSeed` and `minCell` via their own cell-distance metric.
float voronoiBisectorDist(vec2 minSeed, vec2 minCell, vec2 f, vec2 p,
                           float seedScale, float seedBias) {
    float edgeDist = 8.0;
    for (int j = -2; j <= 2; j++) {
        for (int i = -2; i <= 2; i++) {
            vec2 cell = minCell + vec2(float(i), float(j));
            vec2 seed = cell + hash2(p + cell) * seedScale + seedBias;
            vec2 diff = seed - minSeed;
            if (dot(diff, diff) < 0.0001) continue; // skip: same seed
            vec2 mid = (minSeed + seed) * 0.5;
            vec2 dir = normalize(diff);
            edgeDist = min(edgeDist, abs(dot(f - mid, vec2(-dir.y, dir.x))));
        }
    }
    return edgeDist;
}
