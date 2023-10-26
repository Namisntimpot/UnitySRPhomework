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


// ����������꣡������
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

// UnLit ֻ�÷�����ɫ
v2f vert(a2v input)
{
	// �Ȱ�����ת��Ϊ�������꣬Ȼ��ת��Ϊ��Ļ����
    //float3 positionWS = TransformObjectToWorld(positionOS);
    v2f output;
    float3 positionWS = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
    output.positionCS = TransformWorldToHClip(positionWS); // �����������SpaceTransforms.hlsl���, vsû�а�������.
    /// ����Ĵ�����Ϊ�˷�ֹһ�ֽ�shadow pancaking������Unity�Ὣ��Դ����ӰͶ��㾡����ǰ�ƣ������׶�御����ǰ���������Ⱦ��ȣ�������Դ��
    /// ��׶�������ĳЩ�������ӰͶ�����ܱ��ƶ��������ƽ��֮ǰ�ˣ����²��ñ��ü�����Ӱ���ü���.(���ܶ�)  
    /// ����취���������ֻ�����Ž�ƽ��.
    /// ��������ȫ�����ͶӰ�㲻�ᱻ�ü������������ε�ĳЩ���ֿ��ܱ��Σ�Ҳ��֪��Ϊʲô������취�ǽ���ƽ����Զһ�㡣��֪��Ϊʲô
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
    // ͸���Ȳü�.�õ�������ɫ֮����͸�������ж�.
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