#ifndef CUSTOM_POST_FX_STACK_INCLUDED
#define CUSTOM_POST_FX_STACK_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

// Unity自带的Blit把渲染纹理直接画到帧上绘制了两个三角形，实际上可以用一个三角形了事.

TEXTURE2D(_PostFXSource);   // 在PostFXStack中设置的全局纹理，实际上保存了后处理前渲染的图像的结果.
SAMPLER(sampler_linear_clamp);

TEXTURE2D(_PostFXSource2); // 金字塔倒回去时候用的.

float4 _PostFXSource_TexelSize;

bool _BloomBicubicUpsampling;  // 控制是否 bicubic 采样

float4 _BloomThreshold;   // 预过滤选择泛光区域用的.

float _BloomIntensity;   // 泛光强度

float4 GetSourceColor(float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_PostFXSource, sampler_linear_clamp, screenUV, 0);  //不考虑Mipmap
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
    // 裁剪空间坐标：0-(-1,-1), 1-(-1,3), 2-(3,-1); 而正方形空间的坐标是(-1,-1) 到(1,1), 所以用一个三角形内接了需要渲染的正方形，只用一个三角形即可.
    output.positionCS = float4(
        vertexID <= 1 ? -1 : 3,
        vertexID == 1 ? 3 : -1,
        0.0, 1.0
    );
    // 而三个点的屏幕UV应该是 (-1,-1), (-1,1), (1,-1)，不知道为什么全都+1.
    output.screenUV = float2(
        vertexID <= 1 ? 0 : 2,
        vertexID == 1 ? 2 : 0
    );
    if (_ProjectionParams.x <= 0.0)  // 需要反转v轴
    {
        output.screenUV.y = 1.0 - output.screenUV.y;  
    }
    return output;
}

float4 CopyPassFragment(Varying input) : SV_TARGET
{
    return GetSourceColor(input.screenUV);   // 直接返回原图颜色.
}

float4 BloomHorizontalPassFragment(Varying input) : SV_TARGET
{
    float offset[] ={
        -4.0, -3.0, -2.0, -1.0, 0.0, 1.0, 2.0, 3.0, 4.0
    };
    float weights[] ={
        0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703,
		0.19459459, 0.12162162, 0.05405405, 0.01621622
    };  // 权重来自帕斯卡三角形中的行，然后归一化.
    float3 color = 0.0;
    for (int i = 0; i < 9; ++i)
    {
        float off = offset[i] * 2.0 * GetSourceTexelSize().x;  //why 2.0? --因为行列都要降采样到一半.原本2个像素才顶降采样之后1个像素
        float2 newuv = input.screenUV + float2(off, 0);
        color += GetSourceColor(newuv).rgb * weights[i];
    }
    return float4(color, 1.0);  //一定要控制透明度为1.
}

// 为什么要拆开呢..?
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
  //  }; // 权重来自帕斯卡三角形中的行，然后归一化.
    
    // 下面是减少采样次数到5的技巧 不理解
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
        float off = offset[i] * GetSourceTexelSize().y; //why 2.0? --因为水平模糊的时候已经降采样过了，竖直的就不用*2
        float2 newuv = input.screenUV + float2(0, off);
        color += GetSourceColor(newuv).rgb * weights[i];
    }
    return float4(color, 1.0); //一定要控制透明度为1.
}

// 金字塔自顶向下combine倒回去用的.
float4 BloomCombinePassFragment(Varying input) : SV_TARGET
{
    float3 lowRes = _BloomBicubicUpsampling ? GetSourceBicubic(input.screenUV).rgb : GetSourceColor(input.screenUV).rgb;
    float3 highRes = GetSource2Color(input.screenUV).rgb;
    return float4((lowRes * _BloomIntensity + highRes), 1.0);
}


// 预先过滤筛选泛光区域.
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