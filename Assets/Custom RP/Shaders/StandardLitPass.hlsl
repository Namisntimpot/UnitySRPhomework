#ifndef CUSTOM_STANDARDLIT_INCLUDED
#define CUSTOM_STANDARDLIT_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

#include "LitInput.hlsl"

struct a2v
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 baseMapUV : TEXCOORD0;
    GI_ATTRIBUTE_DATA
};

struct v2f
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : V2F_POSITION_WS;
    float3 normalWS : V2F_NORMAL_WS;
    float2 baseMapUV : V2F_BASE_UV;
    GI_VARYING_DATA
};

// UnLit 只用返回颜色
v2f vert(a2v input)
{
	// 先把坐标转化为世界坐标，然后转化为屏幕坐标
    //float3 positionWS = TransformObjectToWorld(positionOS);
    v2f output;
    float3 positionWS = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
    output.positionWS = positionWS;
    output.positionCS = TransformWorldToHClip(positionWS); // 这个函数是在SpaceTransforms.hlsl里的, vs没有包含进来.
    output.baseMapUV = input.baseMapUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    GI_TRANSFER_DATA(input, output)
    return output;
}

float4 frag(v2f input) : SV_TARGET
{
    float4 baseMapColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseMapUV);
    float4 albedo = _BaseColor * baseMapColor;
    // 计算光照.
    /// 形成表面属性.
    float3 view = normalize(_WorldSpaceCameraPos - input.positionWS);
    Surface surf = GetSurface(input.positionWS, input.positionCS, albedo, view, input.normalWS, _Metallic, _Smoothness);
    // 平行光
    int count = GetDirectionalLightCount();
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surf);
    float3 finalrgb = gi.diffuse * GetBRDFDiffuse(surf);
    for (int i = 0; i < count; i++)
    {
        DirectionalLight dirlit = GetDirectionalLight(i, surf);
        finalrgb += GetBRDFwithLamPort(surf, dirlit, gi);
    }
    finalrgb.rgb += GetEmission(input.baseMapUV);
    float4 finalColor = float4(finalrgb, albedo.a);
    // 透明度裁剪.得到最终颜色之后用透明度做判断.
#ifdef _ALPHACLIPPING
    clip(finalColor.a - _Cutoff);
#endif
    return finalColor;
}

#endif