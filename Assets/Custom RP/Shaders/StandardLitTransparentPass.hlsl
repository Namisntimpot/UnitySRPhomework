#ifndef CUSTOM_STANDARDLIT_INCLUDED
#define CUSTOM_STANDARDLIT_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseColor;
float4 _BaseMap_ST;
float _Metallic, _Smoothness;
float _Cutoff;
CBUFFER_END


// ����������꣡������
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

struct a2v
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 baseMapUV : TEXCOORD0;
};

struct v2f
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : V2F_POSITION_WS;
    float3 normalWS : V2F_NORMAL_WS;
    float2 baseMapUV : V2F_BASE_UV;
};

// UnLit ֻ�÷�����ɫ
v2f vert(a2v input)
{
	// �Ȱ�����ת��Ϊ�������꣬Ȼ��ת��Ϊ��Ļ����
    //float3 positionWS = TransformObjectToWorld(positionOS);
    v2f output;
    float3 positionWS = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
    output.positionWS = positionWS;
    output.positionCS = TransformWorldToHClip(positionWS); // �����������SpaceTransforms.hlsl���, vsû�а�������.
    output.baseMapUV = input.baseMapUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

float4 frag(v2f input) : SV_TARGET
{
    float4 baseMapColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseMapUV);
    float4 albedo = _BaseColor * baseMapColor;
    // �������.
    /// �γɱ�������.
    float3 view = normalize(_WorldSpaceCameraPos - input.positionWS);
    Surface surf = GetSurface(input.positionWS, input.positionCS, albedo, view, input.normalWS, _Metallic, _Smoothness);
    // ƽ�й�
    int count = GetDirectionalLightCount();
    float3 finalrgb = 0.;
    for (int i = 0; i < count; i++)
    {
        DirectionalLight dirlit = GetDirectionalLight(i, surf);
        finalrgb += GetBRDFwithLamPort(surf, dirlit);
    }
    float4 finalColor = float4(finalrgb, albedo.a);
    // ͸���Ȳü�.�õ�������ɫ֮����͸�������ж�.
#ifdef _ALPHACLIPPING
    clip(finalColor.a - _Cutoff);
#endif
    return finalColor;
}

#endif