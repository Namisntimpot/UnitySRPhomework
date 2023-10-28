#ifndef CUSTOM_META_INCLUDED
#define CUSTOM_META_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/UnityInput.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

#include "LitInput.hlsl"

bool4 unity_MetaFragmentControl;
float unity_OneOverOutputBoost;
float unity_MaxOutputValue;

struct Attributes
{
    float3 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
    float2 lightMapUV : TEXCOORD1;
};

struct Varying
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : VAR_BASE_UV;
};

Varying MetaPassVertex(Attributes input)
{
    Varying output;
    input.positionOS.xy = input.lightMapUV.xy * unity_LightmapST.xy + unity_LightmapST.zw;
    input.positionOS.z = input.positionOS.z > 0.0 ? FLT_MIN : 0.0;    // 为了兼容openGL, 没有似乎会有问题.
    output.positionCS = TransformWorldToHClip(input.positionOS);
    output.baseUV = input.baseUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
    return output;
}

float4 MetaPassFragment(Varying input) : SV_TARGET
{
    float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV) * _BaseColor;
    float metallic = GetMetallic(input.baseUV);
    Surface surf = GetSurface(0.0, 0.0, baseColor, 1.0, 1.0, 1.0, metallic, _Smoothness, _Fresnel);  // 防止view 和 normal 在 normalize的时候除以0.
    float3 specular = GetBRDFSpecular(surf);
    float3 diffuse = GetBRDFDiffuse(surf);
    float4 meta = 0.0;
    if (unity_MetaFragmentControl.x)
    {
        meta = float4(diffuse, 1.0);
        meta.rgb += surf.roughness * specular * 0.5;
        meta.rgb = min(
			PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue
		);
    }
    else if (unity_MetaFragmentControl.y)
    {
        meta = float4(GetEmission(input.baseUV), 1.0);
    }
    return meta;
}


#endif