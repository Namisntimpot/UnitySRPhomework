#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4

#include "Surface.hlsl"
#include "Shadows.hlsl"
#include "GI.hlsl"

CBUFFER_START(_CustomLight)
//ƽ�й��������
int _DirectionalLightCount;
float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionalLightShadowDatas[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct DirectionalLight
{
    // ƽ�й⣬���Դ����
    float3 color;  // �����ɫ.
    float3 direction;  //����, Ӧ�ñ�֤��׼��.
    float attenuation; // ��˥����Ҳ������ȥ.
};

// ��ȡ��Ӱ����.
DirectionalLightShadowData GetDirectionalLightShadowData(int index, CascadeShadowData shadowData)
{
    // ��ȡ�±�Ϊ i ��ƽ�й����Ӱ���ݣ�ǿ�Ⱥ�����Ӱ�����е��±꣩
    DirectionalLightShadowData ret;
    ret.strength = _DirectionalLightShadowDatas[index].x * shadowData.strengthFading;  // ����˥��
    ret.tileIndex = _DirectionalLightShadowDatas[index].y + shadowData.cascadeIndex;
    // ������cascadeIndex = _CascadeCount, Ҳ�������fragment�������������޳�����tileIndex������ʹ����һ����Դ����Ӱͼ������Ҫǿ���޳�Ϊ0.
    // ��CascadeShadowData.strengthFading ��������Χ��shadowֱ��˥����0����ʹ�ڴ���ĵط�����Ҳû����.
    ret.normalBias = _DirectionalLightShadowDatas[index].z; // ��CPU��������_DirectionalLightShadowDatas����ȡ���Դ��normalBias rate.
    return ret;
}

float Square(float v)
{
    return v * v;
}

/// ��ȡ�ж��ٸ�ƽ�й�.
int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

/// ��ȡ��ƽ�й��������Ϣ������������Ӱ��Ϣ��˥���ʣ���Ӱͼ�����ȵȵ�.
DirectionalLight GetDirectionalLight(int index, Surface surfWS)
{
    CascadeShadowData cascadeData = GetCascadeShadowData(surfWS);
    DirectionalLight dirlight;
    dirlight.color = _DirectionalLightColors[index].rgb;
    dirlight.direction = _DirectionalLightDirections[index].xyz;
    DirectionalLightShadowData dirShadowData = GetDirectionalLightShadowData(index, cascadeData);
    dirlight.attenuation = GetDirectionalShadowAttenuation(dirShadowData, surfWS, cascadeData);
    //dirlight.attenuation = cascadeData.cascadeIndex / 4.0;  // �������㼶.
    //dirlight.attenuation = cascadeData.strengthFading;    // ������Ӱfading ��
    return dirlight;
}

float GetSpecularStrength(float roughness, float3 view, float3 lightdir, float3 normal)
{
    float3 halfview = normalize(normal + lightdir);
    float lh2 = Square(saturate(dot(lightdir, halfview)));
    float nh2 = Square(saturate(dot(normal, halfview)));
    float r2 = Square(roughness);
    float d2 = Square(nh2 * (r2 - 1) + 1.0001);
    float n = 4 * roughness + 2;
    return r2 / (d2 * max(0.1, lh2) * n);
}

float3 GetLamportLighting(Surface surf, DirectionalLight dirlit)
{
    /// ��ȡ�����ع��ղ��֣�������ɫ��Ϣ�����ǲ�������ɫ��Ҳ����ֻ�й��յ�Ӱ��. ע������float3!
    // ����򵥵�������.

    float3 lamport = saturate(dot(surf.normal, dirlit.direction)) * dirlit.color;  //���������˹�ɫ.
    lamport *= dirlit.attenuation;   // ������������˥��.
    return lamport;
}

float3 GetBRDFDiffuse(Surface surf)
{
    return surf.diffuse_reflectance * surf.color.rgb;
}

float3 GetBRDFSpecular(Surface surf)
{
    return lerp(MIN_REFLECTIVITY, surf.color.rgb, surf.metallic);
}

float3 GetBRDFColor(Surface surf, DirectionalLight dirlit)
{
    /// �������������Ϲ��մ�����Ⱦɫ������ɢ���.
    float3 diffuse = GetBRDFDiffuse(surf); //�������.
    float3 specular = GetBRDFSpecular(surf);
    float specular_strength = GetSpecularStrength(surf.roughness, surf.view, dirlit.direction, surf.normal);
    return diffuse + specular * specular_strength; //����ֻ�����˲��ʱ���
}

float3 GetBRDFColor(Surface surf, DirectionalLight dirlit, GI gi)
{
    // �����˺決���յ����.
    float3 diffuse = surf.diffuse_reflectance * surf.color.rgb; //�������.
    float3 specular = lerp(MIN_REFLECTIVITY, surf.color.rgb, surf.metallic);
    float specular_strength = GetSpecularStrength(surf.roughness, surf.view, dirlit.direction, surf.normal);
    return diffuse + specular * specular_strength; //����ֻ�����˲��ʱ���
}

float3 GetBRDFwithLamPort(Surface surf, DirectionalLight dirlit)
{
    return GetBRDFColor(surf, dirlit) * GetLamportLighting(surf, dirlit);
    //return float3(dirlit.attenuation, dirlit.attenuation, dirlit.attenuation); // debug: fading
}

float3 GetBRDFwithLamPort(Surface surf, DirectionalLight dirlit, GI gi)
{
    return GetBRDFColor(surf, dirlit, gi) * GetLamportLighting(surf, dirlit);
    //return float3(dirlit.attenuation, dirlit.attenuation, dirlit.attenuation); // debug: fading
}


// ���brdf, ������.
float3 GetIndirectBRDF(Surface surf, float3 diffuse, float3 specular)
{
    float3 brdf_diffuse = GetBRDFDiffuse(surf);
    float3 brdf_specular = GetBRDFSpecular(surf);
    float3 reflection = specular * brdf_specular;
    reflection /= surf.roughness * surf.roughness + 1;
    float fresnelitem = surf.fresnelStrength * Pow4(1.0 - saturate(dot(surf.normal, surf.view)));  // ���Ʒ�������
    return brdf_diffuse * diffuse + reflection;
}

#endif