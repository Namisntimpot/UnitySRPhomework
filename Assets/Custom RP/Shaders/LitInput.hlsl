#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

CBUFFER_START(UnityPerMaterial)
float4 _BaseColor;
float4 _BaseMap_ST;
float _Cutoff;
float _Metallic;
float _Smoothness;
float4 _EmissionColor;
float _Fresnel;

CBUFFER_END


// 必须用这个宏！！！！
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);

float4 GetBaseColor(float2 uv)
{
    float4 mapcolor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    return mapcolor * _BaseColor;
}

float3 GetEmission(float2 uv)
{
    float4 mapcolor = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv);
    return (_EmissionColor * mapcolor).rgb;
}

#endif