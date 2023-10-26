#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Common.hlsl"
#include "Surface.hlsl"

// 如果使用 PCF33，即设定了下面那个关键词，就需要4个采样器（每个是双线性采样2x2），并且要Core RP定义的Tent_3x3. 其他类似
#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#else
    #define DIRECTIONAL_FILTER_SAMPLES 16
	#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

// 采样阴影图只有一种恰当的方法，可以不依赖Unity的“绑定”（sampler_XXX)，我们可以显式定义一个采样器.
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
int _CascadeCount;
float4 _ShadowDistanceFade; // x:1/maxdistance, y:1/linear_f, z:1-(1-cascade_f)^2
float4 _ShadowAtlasSize;
float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
float4 _CascadeDatas[MAX_CASCADE_COUNT];
float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
CBUFFER_END

struct CascadeShadowData
{
    int cascadeIndex;
    float strengthFading;  // 0-1, 从_ShadowDistanceFade规定的范围开始衰减，直到达到maxDistance的时候衰减系数达到0
    // Fading 用过来去掉阴影。为0则没有阴影，而光衰减率为1，即没有衰减。
    float cascadeBlending;  // 在不同的级联层间过度.
};

struct DirectionalLightShadowData
{
    // 平行光逐光源的属性
    float strength;
    int tileIndex;    // 这个阴影在平行光阴影数组中的下标，也是在Atlas中下标的编号.
    float normalBias;
};

float FadingShadowStrength(float depth, float scale, float fadeRange)
{
    return saturate((1 - depth * scale) * fadeRange);
}

CascadeShadowData GetCascadeShadowData(Surface surfWS)
{
    CascadeShadowData ret;
    // 线性平滑过度：saturate((1-d/m)/f), where d is depth, m is the max shadow distance, f is a ratio defining where starts fading.
    // _ShadowDistanceFade.x = max shadow distance, ____.y = f
    ret.strengthFading =
        FadingShadowStrength(surfWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y); // saturate: clampe to [0,1]. 能保证超过最大范围的为0。
    ret.cascadeBlending = 1.0;
    int i;
    for (i = 0; i < _CascadeCount; ++i)
    {
        float4 cullingSphere = _CascadeCullingSpheres[i];
        float dis_squared = DistanceSquared(surfWS.position, cullingSphere.xyz);
        if (dis_squared < cullingSphere.w)
        {
            float fading = FadingShadowStrength(dis_squared, 1.0 / cullingSphere.w, _ShadowDistanceFade.z);
            if (i == _CascadeCount-1)
            {
                // 在最外层级联阴影图中，需要cascade fade.
                // 公式：(1 - d^2/r^2) / (1 - (1-f)^2); d is distance between fragment and sphere's center
                ret.strengthFading *= fading;
                
            }
            else
            {
                ret.cascadeBlending = fading;
            }
            break;
        }
    }
    if (i == _CascadeCount)
    {
        ret.strengthFading = 0;    // 超出最外层cullingSphere以及距离超过maxDistance的都应该去掉阴影!
    }
#if defined(_CASCADE_BLEND_DITHER)  // 是抖动过渡
		else if (ret.cascadeBlending < surfWS.dither) {    // 让它到下一级采样去!
			i += 1;
		}
#endif
#if !defined(_CASCADE_BLEND_SOFT)  // 不是soft过渡
    ret.cascadeBlending = 1.0;
#endif
    ret.cascadeIndex = i;
    return ret;
}

float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    // 对平行光阴影图进行采样，输入纹理坐标.
    // 注意这是float3, 是包含了深度信息的，下面那个宏里面隐含了一次深度比较，所以采样返回的能够直接是有无阴影, 而不是距离
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

// 在阴影图中采样，并用滤波器卷积.
float FilterDirectionalShadow(float3 positionSTS)
{
#if defined(DIRECTIONAL_FILTER_SETUP)
    // 要使用滤波.
    float weights[DIRECTIONAL_FILTER_SAMPLES];  // SAMPLES个权重
    float2 positions[DIRECTIONAL_FILTER_SAMPLES]; // SAMPLES个采样位置.
    float4 size = _ShadowAtlasSize.yyxx;        // 阴影图分辨率.
    DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);  // 获取卷积数据，也就是weights and positions
    float shadow = 0.0;
    for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; ++i)
    {
        shadow += SampleDirectionalShadowAtlas(float3(positions[i], positionSTS.z)) * weights[i];
    }
    return shadow;
#else
    return SampleDirectionalShadowAtlas(positionSTS);
#endif
}

// 获得因平行光阴影造成的光照衰减
float GetDirectionalShadowAttenuation(DirectionalLightShadowData dirShadowData, Surface surf, CascadeShadowData cascadeShadowData)
{
    if(dirShadowData.strength <= 0.0)   //按理说不应该存在...
        return 1.;
    // normal bias, 相当于在采样前做了displacement...
    float3 normalBias = surf.normal * _CascadeDatas[cascadeShadowData.cascadeIndex].y * dirShadowData.normalBias;
    float3 positionSTS = mul(_DirectionalShadowMatrices[dirShadowData.tileIndex], float4(surf.position + normalBias, 1.0)).xyz;  // 变换到对应光源的阴影图坐标
    float shadow = FilterDirectionalShadow(positionSTS);
#if defined(_CASCADE_BLEND_SOFT)
    if (cascadeShadowData.cascadeBlending < 1.0)
    {
        // 要在两层之间过度.特别的，是与下一层过渡。因为最外层的cascadeblending一定是1.
        normalBias = surf.normal * _CascadeDatas[cascadeShadowData.cascadeIndex + 1].y * dirShadowData.normalBias;
        positionSTS = mul(_DirectionalShadowMatrices[dirShadowData.tileIndex + 1], float4(surf.position + normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, cascadeShadowData.cascadeBlending);
    }
#endif
    return lerp(1.0, shadow, dirShadowData.strength);
    //return dirShadowData.strength;
}

#endif