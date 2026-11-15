#ifndef ISOGOLF_AMBIENT_MOTION_INCLUDED
#define ISOGOLF_AMBIENT_MOTION_INCLUDED

// Ambient life of M4 PR 4.5: calm sway of foliage and the flag, and fading highlights on water facets.
// Visual only: nothing in the simulation reads any of it, and normals are never moved, so every facet keeps its tone.
//
// UV1 semantics, written by TerrainMeshData (keep the two comments in step):
//   x >  0         a swaying vertex: x is its weight (0 at the root, 1 at the tip), y is its phase (0..1, one per tree)
//   x == 0, y > 0  a water facet: y is its glint hash (0..1, one value per facet)
//   x == 0, y > 1  a waterfall facet: y packs its row down the fall and a hash (see ISOGOLF_FALL_* below); its tone flows down
//   (0, 0)         static: ground, trunks, rocks, walls, the flag pole, the root edge of the pennant
// A mesh with no UV1 channel reads (0, 0) and is left exactly as it is.

#define ISOGOLF_TWO_PI 6.28318530718

// Waterfall flow: UV1.y packing (see AmbientGlint_float and TerrainMeshData.FallPhase, keep the two in step) and the look of the bands.
#define ISOGOLF_FALL_GATE 1.005
#define ISOGOLF_FALL_BASE 1.01
#define ISOGOLF_FALL_SCALE 0.98
#define ISOGOLF_FALL_BAND 0.32       // bands per row: how far apart the light bands are down the sheet
#define ISOGOLF_FALL_SPEED 0.55      // bands per second moving down
#define ISOGOLF_FALL_AMPLITUDE 0.2   // how far a facet's tone goes lighter or darker
#define ISOGOLF_FALL_RAMP_ROWS 3.0   // rows over which the bands fade in below the brink (row 0 stays still)

// Params: x = horizontal amplitude (m, at weight 1), y = vertical amplitude (m), z = speed (rad/s),
//         w = time offset (s), for captures and tests; 0 in play.
// The vertex stage runs the same code in every pass, so the shadow caster sways with the foliage. Amplitudes stay small
// (a few centimetres) so the crisp shadow edges do not swim.
void AmbientSway_float(float3 PositionOS, float4 UV1, float4 Params, out float3 Out)
{
    float t = ((_Time.y + Params.w) * Params.z) + (UV1.y * ISOGOLF_TWO_PI);

    // Two sines per axis at unrelated rates, so the motion never visibly loops. Each sum stays within -1.5..1.5.
    float3 wave = float3(
        sin(t) + (0.5 * sin((t * 1.7) + 1.3)),
        sin((t * 1.3) + 0.7),
        sin((t * 0.83) + 2.1) + (0.5 * sin((t * 1.9) + 0.4)));
    wave *= float3(Params.x, Params.y, Params.x) * (1.0 / 1.5);

    // Weight 0 (and every water facet, whose x is 0) multiplies the offset away: those vertices stay bit-still.
    Out = PositionOS + (wave * UV1.x);
}

// GlintParams: x = share of water facets that glint (0..1), y = speed (rad/s), z = strength (0..1, how far a facet
// goes toward the glint colour), w = time offset (s).
void AmbientGlint_float(float3 Color, float4 UV1, float3 GlintColor, float4 GlintParams, out float3 Out)
{
    float hash = UV1.y;
    float isWater = step(UV1.x, 0.0) * step(0.0001, hash);

    // A facet glints when its hash falls under the share; the hash then also sets its phase, spread over a cycle.
    float chosen = step(hash, GlintParams.x);
    float phase = (hash / max(GlintParams.x, 0.0001)) * ISOGOLF_TWO_PI;
    float pulse = saturate(sin(((_Time.y + GlintParams.w) * GlintParams.y) + phase));

    // One tone per facet: the whole triangle fades together, so it stays flat shaded.
    float3 pond = lerp(Color, GlintColor, pulse * pulse * GlintParams.z * chosen * isWater);

    // Waterfall (M4 PR 4.8): a facet with UV1.y above ISOGOLF_FALL_GATE. A pond's glint hash never exceeds 1, so ponds never take this
    // branch. UV1.y packs the facet's row down the fall and a hash: y = FALL_BASE + FALL_SCALE * (row + 0.99 * hash) / 16.
    float isFall = step(UV1.x, 0.0) * step(ISOGOLF_FALL_GATE, UV1.y);
    float packed = max(((UV1.y - ISOGOLF_FALL_BASE) / ISOGOLF_FALL_SCALE) * 16.0, 0.0);
    float row = floor(packed);
    float fallHash = frac(packed) / 0.99;

    // Light and dark bands travel down the sheet: each facet's tone follows a sine of its row minus time, so the bands move down.
    // Only the tone changes (flat per facet): no vertex moves, and nothing reads this but the eye.
    float wave = 0.5 + (0.5 * sin(ISOGOLF_TWO_PI * ((row * ISOGOLF_FALL_BAND) - (_Time.y * ISOGOLF_FALL_SPEED)) + (fallHash * 2.4)));
    // The bands fade in over the first rows: the brink keeps the pond's still tone, so no band flickers at the joint.
    float amplitude = ISOGOLF_FALL_AMPLITUDE * saturate(row / ISOGOLF_FALL_RAMP_ROWS);
    float3 fall = Color * (1.0 + (amplitude * ((2.0 * wave) - 1.0)));

    Out = lerp(pond, saturate(fall), isFall);
}

#endif
