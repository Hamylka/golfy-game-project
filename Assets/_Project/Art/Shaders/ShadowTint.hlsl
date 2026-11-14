#ifndef ISOGOLF_SHADOW_TINT_INCLUDED
#define ISOGOLF_SHADOW_TINT_INCLUDED

// Set from C# by CloudShadowView (globals, not per material): _CloudShadow x = strength (0 = off), y = radius in metres, z = how much
// narrower it is along Z than along X (an oval); _CloudShadowCentre xy = the centre on the ground (world X, Z).
float4 _CloudShadow;
float4 _CloudShadowCentre;

// Colors cast shadows without touching facets that merely face away from the sun. In stock URP Lit, a
// shadow is simply the ambient color, so tinting the ambient would tint every shaded facet too. This
// keeps the ambient warm and adds the tint through Emission, only where the sun is actually blocked.
void ShadowTint_float(float3 Albedo, float3 PositionWS, float3 NormalWS, float3 Tint, out float3 Out)
{
    Out = 0;

    // Only the forward pass has the shadow map and light data. Other passes and previews stay untinted.
#if !defined(SHADERGRAPH_PREVIEW) && defined(SHADERPASS) && (SHADERPASS == SHADERPASS_FORWARD)
    // This overload applies the same distance fade as Lit, so the tint disappears along with the shadow.
    Light sun = GetMainLight(TransformWorldToShadowCoord(PositionWS), PositionWS, half4(1, 1, 1, 1));
    float facing = saturate(dot(normalize(NormalWS), sun.direction));
    Out = Albedo * Tint * facing * (1.0 - sun.shadowAttenuation);

    // Cloud shadow (M4 4.7): one soft darkening drifting over the island. Emission is added after lighting, so subtracting a share of
    // the sun's own diffuse term darkens only what the sun lights, and never goes below the ambient. With the globals unset (strength 0)
    // nothing changes.
    float2 fromCentre = (PositionWS.xz - _CloudShadowCentre.xy) / max(_CloudShadow.y, 0.01);
    float cloud = 1.0 - smoothstep(0.35, 1.0, length(fromCentre * float2(1.0, _CloudShadow.z)));
    Out -= Albedo * sun.color * facing * sun.shadowAttenuation * (cloud * _CloudShadow.x);
#endif
}

#endif
