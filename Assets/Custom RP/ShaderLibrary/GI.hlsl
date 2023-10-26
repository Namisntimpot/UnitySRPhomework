#ifndef CUSTOM_GLOBAL_ILLUMINATION_INCLUDED
#define CUSTOM_GLOBAL_ILLUMINATION_INCLUDED

#include "UnityInput.hlsl"
#include "Surface.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

#if defined(LIGHTMAP_ON)
    #define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;     // a2v中的成员
    #define GI_VARYING_DATA float2 lightMapUV : VAR_LIGHTMAP_UV;  // v2f中的成员
    #define GI_TRANSFER_DATA(input, output) \
        output.lightMapUV = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    #define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
    #define GI_ATTRIBUTE_DATA
    #define GI_VARYING_DATA
    #define GI_TRANSFER_DATA(input, output)
    #define GI_FRAGMENT_DATA(input) 0.0   //一个没有意义的占位用的，因为它会作为GetGI函数的参数..
#endif
// light map
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

// probe volume
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

struct GI
{
    float3 diffuse;   // 只能处理漫反射
};

float3 SampleLightmap(float2 lightMapUV)
{
#if defined(LIGHTMAP_ON)
    // SampleLightmap 参数：第一个是lightmap和配套的采样器，用宏定义组合起来；第二个是lightmapuv，第三个是lightmapuvST，因为提前处理过所以不用，
    // 第四个是是否压缩，定义了UNITY_LIGHTMAP_FULL_HDR的时候是没压缩的；最后一个参数是解码相关的，只用前俩.
    return SampleSingleLightmap(
        TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap),lightMapUV,
        float4(1,1,0,0),
        #if defined(UNITY_LIGHTMAP_FULL_HDR)
            false,
        #else
            true,
        #endif
        float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
    );
#else
    return 0.0;
#endif
}

float3 SampleLightProbes(Surface surfWS)
{
    // 对于动态物体，使用lightprobes.
#if defined(LIGHTMAP_ON)
    return 0.0;
#else
    if (unity_ProbeVolumeParams.x)
    {
        // 使用LPPV
        return SampleProbeVolumeSH4(
            TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
            surfWS.position, surfWS.normal,
            unity_ProbeVolumeWorldToObject,
            unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
            unity_ProbeVolumeMin.xyz, unity_ProbeVolumeSizeInv.xyz
        );
    }
    else
    {
        float4 sh[7];
        sh[0] = unity_SHAr;
        sh[1] = unity_SHAg;
        sh[2] = unity_SHAb;
        sh[3] = unity_SHBr;
        sh[4] = unity_SHBg;
        sh[5] = unity_SHBb;
        sh[6] = unity_SHC;
        return max(0.0, SampleSH9(sh, surfWS.normal));  // 9个SH系数中，normal应该有贡献.
    }
#endif
}

GI GetGI(float2 lightMapUV, Surface surfWS)
{
    GI gi;
    gi.diffuse = SampleLightmap(lightMapUV) + SampleLightProbes(surfWS);
    return gi;
}


#endif