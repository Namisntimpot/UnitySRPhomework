#ifndef CUSTOM_GLOBAL_ILLUMINATION_INCLUDED
#define CUSTOM_GLOBAL_ILLUMINATION_INCLUDED

#include "UnityInput.hlsl"
#include "Surface.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

#if defined(LIGHTMAP_ON)
    #define GI_ATTRIBUTE_DATA float2 lightMapUV : TEXCOORD1;     // a2v�еĳ�Ա
    #define GI_VARYING_DATA float2 lightMapUV : VAR_LIGHTMAP_UV;  // v2f�еĳ�Ա
    #define GI_TRANSFER_DATA(input, output) \
        output.lightMapUV = input.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    #define GI_FRAGMENT_DATA(input) input.lightMapUV
#else
    #define GI_ATTRIBUTE_DATA
    #define GI_VARYING_DATA
    #define GI_TRANSFER_DATA(input, output)
    #define GI_FRAGMENT_DATA(input) 0.0   //һ��û�������ռλ�õģ���Ϊ������ΪGetGI�����Ĳ���..
#endif
// light map
TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);

// probe volume
TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

struct GI
{
    float3 diffuse;   // ֻ�ܴ���������
};

float3 SampleLightmap(float2 lightMapUV)
{
#if defined(LIGHTMAP_ON)
    // SampleLightmap ��������һ����lightmap�����׵Ĳ��������ú궨������������ڶ�����lightmapuv����������lightmapuvST����Ϊ��ǰ��������Բ��ã�
    // ���ĸ����Ƿ�ѹ����������UNITY_LIGHTMAP_FULL_HDR��ʱ����ûѹ���ģ����һ�������ǽ�����صģ�ֻ��ǰ��.
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
    // ���ڶ�̬���壬ʹ��lightprobes.
#if defined(LIGHTMAP_ON)
    return 0.0;
#else
    if (unity_ProbeVolumeParams.x)
    {
        // ʹ��LPPV
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
        return max(0.0, SampleSH9(sh, surfWS.normal));  // 9��SHϵ���У�normalӦ���й���.
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