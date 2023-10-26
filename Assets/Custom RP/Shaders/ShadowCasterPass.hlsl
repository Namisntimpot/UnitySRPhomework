#ifndef CUSTOM_SHADOWCASTER_INCLUDED
#define CUSTOM_SHADOWCASTER_INCLUDED
#include "../ShaderLibrary/Common.hlsl"

//#if defined(_SHADOW_DITHER)
//    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
//#endif

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
    output.positionCS = TransformWorldToHClip(positionWS); // 这个函数是在SpaceTransforms.hlsl里的, vs没有包含进来.
    /// 下面的代码是为了防止一种叫shadow pancaking的现象。Unity会将光源的阴影投射点尽可能前移（相对视锥体尽可能前）来提高深度精度，但当光源在
    /// 视锥体外面的某些情况，阴影投射点可能被移动到相机近平面之前了，导致不该被裁剪的阴影被裁剪掉.(不很懂)  
    /// 解决办法是让它最多只能贴着近平面.
    /// 但不能完全解决。投影点不会被裁剪，但大三角形的某些部分可能变形，也不知道为什么。解决办法是将近平面推远一点。不知道为什么
#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    output.baseMapUV = input.baseMapUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
    return output;
}

void frag(v2f input)
{
    float4 baseMapColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseMapUV);
    float4 albedo = _BaseColor * baseMapColor;
    // 透明度裁剪.得到最终颜色之后用透明度做判断.
#if defined(_ALPHACLIPPING)
    clip(albedo.a - _Cutoff);
#elif defined(_SHADOW_CLIP)
    clip(albedo.a - _Cutoff);
#elif defined(_SHADOW_DITHER)
    float dither = InterleavedGradientNoise(input.positionCS.xy, 0);
    clip(albedo.a - dither);
#endif
}

#endif