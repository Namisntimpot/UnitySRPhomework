#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Common.hlsl"
#include "Surface.hlsl"

// ���ʹ�� PCF33�����趨�������Ǹ��ؼ��ʣ�����Ҫ4����������ÿ����˫���Բ���2x2��������ҪCore RP�����Tent_3x3. ��������
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

// ������Ӱͼֻ��һ��ǡ���ķ��������Բ�����Unity�ġ��󶨡���sampler_XXX)�����ǿ�����ʽ����һ��������.
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
    float strengthFading;  // 0-1, ��_ShadowDistanceFade�涨�ķ�Χ��ʼ˥����ֱ���ﵽmaxDistance��ʱ��˥��ϵ���ﵽ0
    // Fading �ù���ȥ����Ӱ��Ϊ0��û����Ӱ������˥����Ϊ1����û��˥����
    float cascadeBlending;  // �ڲ�ͬ�ļ���������.
};

struct DirectionalLightShadowData
{
    // ƽ�й����Դ������
    float strength;
    int tileIndex;    // �����Ӱ��ƽ�й���Ӱ�����е��±꣬Ҳ����Atlas���±�ı��.
    float normalBias;
};

float FadingShadowStrength(float depth, float scale, float fadeRange)
{
    return saturate((1 - depth * scale) * fadeRange);
}

CascadeShadowData GetCascadeShadowData(Surface surfWS)
{
    CascadeShadowData ret;
    // ����ƽ�����ȣ�saturate((1-d/m)/f), where d is depth, m is the max shadow distance, f is a ratio defining where starts fading.
    // _ShadowDistanceFade.x = max shadow distance, ____.y = f
    ret.strengthFading =
        FadingShadowStrength(surfWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y); // saturate: clampe to [0,1]. �ܱ�֤�������Χ��Ϊ0��
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
                // ������㼶����Ӱͼ�У���Ҫcascade fade.
                // ��ʽ��(1 - d^2/r^2) / (1 - (1-f)^2); d is distance between fragment and sphere's center
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
        ret.strengthFading = 0;    // ���������cullingSphere�Լ����볬��maxDistance�Ķ�Ӧ��ȥ����Ӱ!
    }
#if defined(_CASCADE_BLEND_DITHER)  // �Ƕ�������
		else if (ret.cascadeBlending < surfWS.dither) {    // ��������һ������ȥ!
			i += 1;
		}
#endif
#if !defined(_CASCADE_BLEND_SOFT)  // ����soft����
    ret.cascadeBlending = 1.0;
#endif
    ret.cascadeIndex = i;
    return ret;
}

float SampleDirectionalShadowAtlas(float3 positionSTS)
{
    // ��ƽ�й���Ӱͼ���в�����������������.
    // ע������float3, �ǰ����������Ϣ�ģ������Ǹ�������������һ����ȱȽϣ����Բ������ص��ܹ�ֱ����������Ӱ, �����Ǿ���
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

// ����Ӱͼ�в����������˲������.
float FilterDirectionalShadow(float3 positionSTS)
{
#if defined(DIRECTIONAL_FILTER_SETUP)
    // Ҫʹ���˲�.
    float weights[DIRECTIONAL_FILTER_SAMPLES];  // SAMPLES��Ȩ��
    float2 positions[DIRECTIONAL_FILTER_SAMPLES]; // SAMPLES������λ��.
    float4 size = _ShadowAtlasSize.yyxx;        // ��Ӱͼ�ֱ���.
    DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);  // ��ȡ������ݣ�Ҳ����weights and positions
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

// �����ƽ�й���Ӱ��ɵĹ���˥��
float GetDirectionalShadowAttenuation(DirectionalLightShadowData dirShadowData, Surface surf, CascadeShadowData cascadeShadowData)
{
    if(dirShadowData.strength <= 0.0)   //����˵��Ӧ�ô���...
        return 1.;
    // normal bias, �൱���ڲ���ǰ����displacement...
    float3 normalBias = surf.normal * _CascadeDatas[cascadeShadowData.cascadeIndex].y * dirShadowData.normalBias;
    float3 positionSTS = mul(_DirectionalShadowMatrices[dirShadowData.tileIndex], float4(surf.position + normalBias, 1.0)).xyz;  // �任����Ӧ��Դ����Ӱͼ����
    float shadow = FilterDirectionalShadow(positionSTS);
#if defined(_CASCADE_BLEND_SOFT)
    if (cascadeShadowData.cascadeBlending < 1.0)
    {
        // Ҫ������֮�����.�ر�ģ�������һ����ɡ���Ϊ������cascadeblendingһ����1.
        normalBias = surf.normal * _CascadeDatas[cascadeShadowData.cascadeIndex + 1].y * dirShadowData.normalBias;
        positionSTS = mul(_DirectionalShadowMatrices[dirShadowData.tileIndex + 1], float4(surf.position + normalBias, 1.0)).xyz;
        shadow = lerp(FilterDirectionalShadow(positionSTS), shadow, cascadeShadowData.cascadeBlending);
    }
#endif
    return lerp(1.0, shadow, dirShadowData.strength);
    //return dirShadowData.strength;
}

#endif