#ifndef ISOGOLF_SHADOW_TINT_INCLUDED
#define ISOGOLF_SHADOW_TINT_INCLUDED

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
#endif
}

#endif
