#ifndef CUSTOM_UNLIT_INCLUDED
#define CUSTOM_UNLIT_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

// 每个材质之间不同. 这个是SPR batch用的，但如果用MaterialPropertyBlock修改颜色后可用GPU Instancing而不可用 SRP Batcher
CBUFFER_START(UnityPerMaterial)
float4 _BaseColor;
float4 _BaseMap_ST;
float _Cutoff;
CBUFFER_END

// 必须用这个宏！！！！
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);


struct a2v
{
    float3 positionOS : POSITION;
    float2 baseMapUV : TEXCOORD0;
};

struct v2f
{
    float4 positionCS : SV_POSITION;
    float2 baseMapUV : V2F_BASE_UV;
};

// UnLit 只用返回颜色
v2f vert(a2v input)
{
	// 先把坐标转化为世界坐标，然后转化为屏幕坐标
    //float3 positionWS = TransformObjectToWorld(positionOS);
    v2f output;
    float3 positionWS = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
	output.positionCS = TransformWorldToHClip(positionWS);  // 这个函数是在SpaceTransforms.hlsl里的, vs没有包含进来.
    output.baseMapUV = input.baseMapUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
    return output;
}

float4 frag(v2f input) : SV_TARGET
{
    float4 baseMapColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseMapUV);
    float4 albedo = _BaseColor * baseMapColor;
    // 透明度裁剪.得到最终颜色之后用透明度做判断.
    #ifdef _ALPHACLIPPING
        clip(albedo.a - _Cutoff);
    #endif
	return albedo;
}

#endif