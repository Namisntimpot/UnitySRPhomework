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

float _NormalScale;

CBUFFER_END


// 必须用这个宏！！！！
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

TEXTURE2D(_NormalMap);
SAMPLER(sampler_NormalMap);

TEXTURE2D(_MetallicMap);
SAMPLER(sampler_MetallicMap);

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

float GetMetallic(float2 uv)
{
#if defined(_Metallic_Map)
    return SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, uv).r;
#else
    return _Metallic;
#endif
}

float3 SampleNormalMap(float2 uv, float scale)
{
    float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
    float3 normal = DecodeNormal(map, scale);
    return normal;
}
float3 SampleNormalMap(float2 uv)
{
    float4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv);
    float3 normal = DecodeNormal(map, _NormalScale);
    return normal;
}

float3 GetNormalWS(float2 uv, float3 oldNormalWS, float4 tangentWS)
{
#if defined(_Normal_Map)
    float3 normalTS = SampleNormalMap(uv);
    float3 normalWS = NormalTangentToWorld(normalTS, oldNormalWS, tangentWS);
    return normalWS;
#else
    return oldNormalWS;
#endif
}



#endif