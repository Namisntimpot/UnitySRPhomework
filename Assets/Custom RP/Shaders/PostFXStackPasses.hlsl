#ifndef CUSTOM_POST_FX_STACK_INCLUDED
#define CUSTOM_POST_FX_STACK_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

// Unity�Դ���Blit����Ⱦ����ֱ�ӻ���֡�ϻ��������������Σ�ʵ���Ͽ�����һ������������.

TEXTURE2D(_PostFXSource);   // ��PostFXStack�����õ�ȫ������ʵ���ϱ����˺���ǰ��Ⱦ��ͼ��Ľ��.
SAMPLER(sampler_linear_clamp);

TEXTURE2D(_PostFXSource2); // ����������ȥʱ���õ�.

float4 _PostFXSource_TexelSize;

bool _BloomBicubicUpsampling;  // �����Ƿ� bicubic ����

float4 _BloomThreshold;   // Ԥ����ѡ�񷺹������õ�.

float _BloomIntensity;   // ����ǿ��

float4 GetSourceColor(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);  //������Mipmap
}

float4 GetSource2Color(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource2, sampler_linear_clamp, screenUV, 0);
}

float4 GetSourceTexelSize()
{
    return _PostFXSource_TexelSize;
}

float4 GetSourceBicubic(float2 screenUV)
{
    return SampleTexture2DBicubic(
		TEXTURE2D_ARGS(_PostFXSource, sampler_linear_clamp), screenUV,
		_PostFXSource_TexelSize.zwxy, 1.0, 0.0
	);
}

struct Varying
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

Varying DefaultPassVertex(uint vertexID : SV_VertexID)
{
    Varying output;
    // �ü��ռ����꣺0-(-1,-1), 1-(-1,3), 2-(3,-1); �������οռ��������(-1,-1) ��(1,1), ������һ���������ڽ�����Ҫ��Ⱦ�������Σ�ֻ��һ�������μ���.
    output.positionCS = float4(
        vertexID <= 1 ? -1 : 3,
        vertexID == 1 ? 3 : -1,
        0.0, 1.0
    );
    // �����������ĻUVӦ���� (-1,-1), (-1,1), (1,-1)����֪��Ϊʲôȫ��+1.
    output.screenUV = float2(
        vertexID <= 1 ? 0 : 2,
        vertexID == 1 ? 2 : 0
    );
    if (_ProjectionParams.x <= 0.0)  // ��Ҫ��תv��
    {
        output.screenUV.y = 1.0 - output.screenUV.y;  
    }
    return output;
}

float4 CopyPassFragment(Varying input) : SV_TARGET
{
    return GetSourceColor(input.screenUV);   // ֱ�ӷ���ԭͼ��ɫ.
}

float4 BloomHorizontalPassFragment(Varying input) : SV_TARGET
{
    float offset[] ={
        -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
    };
    float weights[] ={
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
    };  // Ȩ��������˹���������е��У�Ȼ���һ��.
    float3 color = 0.0;
    for (int i = 0; i < 9; ++i)
    {
        float off = offset[i] * 2.0 * GetSourceTexelSize().x;  //why 2.0? --��Ϊ���ж�Ҫ��������һ��.ԭ��2�����زŶ�������֮��1������
        float2 newuv = input.screenUV + float2(off, 0);
        color += GetSourceColor(newuv).rgb * weights[i];
    }
    return float4(color, 1.0);  //һ��Ҫ����͸����Ϊ1.
}

// ΪʲôҪ����..?
float4 BloomVerticalPassFragment(Varying input) : SV_TARGET
{
  //  float offset[] =
  //  {
  //      -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
  //  };
  //  float weights[] =
  //  {
  //      0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		//0.19459459, 0.12162162, 0.05405405, 0.01621622
  //  }; // Ȩ��������˹���������е��У�Ȼ���һ��.
    
    // �����Ǽ��ٲ���������5�ļ��� �����
    float offset[] =
    {
        -3.23076923, -1.38461538, 0.0, 1.38461538, 3.23076923
    };
    float weights[] =
    {
        0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027
    };
    float3 color = 0.0;
    for (int i = 0; i < 5; ++i)
    {
        float off = offset[i] * GetSourceTexelSize().y; //why 2.0? --��Ϊˮƽģ����ʱ���Ѿ����������ˣ���ֱ�ľͲ���*2
        float2 newuv = input.screenUV + float2(0, off);
        color += GetSourceColor(newuv).rgb * weights[i];
    }
    return float4(color, 1.0); //һ��Ҫ����͸����Ϊ1.
}

// �������Զ�����combine����ȥ�õ�.
float4 BloomCombinePassFragment(Varying input) : SV_TARGET
{
    float3 lowRes = _BloomBicubicUpsampling ? GetSourceBicubic(input.screenUV).rgb : GetSourceColor(input.screenUV);
    float3 highRes = GetSource2Color(input.screenUV).rgb;
    return float4((lowRes * _BloomIntensity + highRes), 1.0);
}


// Ԥ�ȹ���ɸѡ��������.
float3 ApplyBloomThreshold(float3 color)
{
    float brightness = Max3(color.r, color.g, color.b);
    float soft = brightness + _BloomThreshold.y;
    soft = clamp(soft, 0.0, _BloomThreshold.z);
    soft = soft * soft * _BloomThreshold.w;
    float contribution = max(soft, brightness - _BloomThreshold.x);
    contribution /= max(brightness, 0.00001);
    return color * contribution;
}

float4 BloomPrefilterPassFragment(Varying input) : SV_TARGET
{
    float3 color = ApplyBloomThreshold(GetSourceColor(input.screenUV).rgb);
    return float4(color, 1.0);
}

#endif