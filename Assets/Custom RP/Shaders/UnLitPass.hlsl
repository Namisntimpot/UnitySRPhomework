#ifndef CUSTOM_UNLIT_INCLUDED
#define CUSTOM_UNLIT_INCLUDED

#include "../ShaderLibrary/Common.hlsl"

// ÿ������֮�䲻ͬ. �����SPR batch�õģ��������MaterialPropertyBlock�޸���ɫ�����GPU Instancing�������� SRP Batcher
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
	output.positionCS = TransformWorldToHClip(positionWS);  // �����������SpaceTransforms.hlsl���, vsû�а�������.
    output.baseMapUV = input.baseMapUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
    return output;
}

float4 frag(v2f input) : SV_TARGET
{
    float4 baseMapColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseMapUV);
    float4 albedo = _BaseColor * baseMapColor;
    // ͸���Ȳü�.�õ�������ɫ֮����͸�������ж�.
    #ifdef _ALPHACLIPPING
        clip(albedo.a - _Cutoff);
    #endif
	return albedo;
}

#endif