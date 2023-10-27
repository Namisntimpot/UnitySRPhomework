#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

#define MAX_DIRECTIONAL_LIGHT_COUNT 4

#include "Surface.hlsl"
#include "Shadows.hlsl"
#include "GI.hlsl"

CBUFFER_START(_CustomLight)
//平行光相关数据
int _DirectionalLightCount;
float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
float4 _DirectionalLightShadowDatas[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct DirectionalLight
{
    // 平行光，逐光源属性
    float3 color;  // 光的颜色.
    float3 direction;  //方向, 应该保证标准化.
    float attenuation; // 把衰减率也包含进去.
};

// 获取阴影数据.
DirectionalLightShadowData GetDirectionalLightShadowData(int index, CascadeShadowData shadowData)
{
    // 获取下标为 i 的平行光的阴影数据（强度和在阴影数组中的下标）
    DirectionalLightShadowData ret;
    ret.strength = _DirectionalLightShadowDatas[index].x * shadowData.strengthFading;  // 进行衰减
    ret.tileIndex = _DirectionalLightShadowDatas[index].y + shadowData.cascadeIndex;
    // 这里，如果cascadeIndex = _CascadeCount, 也就是这个fragment超过了最外层的剔除球，则tileIndex会错误地使用下一个光源的阴影图，所以要强制剔除为0.
    // 用CascadeShadowData.strengthFading 将超出范围的shadow直接衰减至0，即使在错误的地方采样也没问题.
    ret.normalBias = _DirectionalLightShadowDatas[index].z; // 从CPU传进来的_DirectionalLightShadowDatas处获取逐光源的normalBias rate.
    return ret;
}

float Square(float v)
{
    return v * v;
}

/// 获取有多少个平行光.
int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

/// 获取该平行光的所有信息，包括级联阴影信息，衰减率，阴影图索引等等等.
DirectionalLight GetDirectionalLight(int index, Surface surfWS)
{
    CascadeShadowData cascadeData = GetCascadeShadowData(surfWS);
    DirectionalLight dirlight;
    dirlight.color = _DirectionalLightColors[index].rgb;
    dirlight.direction = _DirectionalLightDirections[index].xyz;
    DirectionalLightShadowData dirShadowData = GetDirectionalLightShadowData(index, cascadeData);
    dirlight.attenuation = GetDirectionalShadowAttenuation(dirShadowData, surfWS, cascadeData);
    //dirlight.attenuation = cascadeData.cascadeIndex / 4.0;  // 看级联层级.
    //dirlight.attenuation = cascadeData.strengthFading;    // 看看阴影fading 率
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
    /// 获取兰伯特光照部分，包含光色信息，但是不乘以颜色，也就是只有光照的影响. 注意它是float3!
    // 先最简单的漫反射.

    float3 lamport = saturate(dot(surf.normal, dirlit.direction)) * dirlit.color;  //这里引入了光色.
    lamport *= dirlit.attenuation;   // 在这里算入了衰减.
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
    /// 返回自身材质配合光照带来的染色反射光和散射光.
    float3 diffuse = GetBRDFDiffuse(surf); //漫反射光.
    float3 specular = GetBRDFSpecular(surf);
    float specular_strength = GetSpecularStrength(surf.roughness, surf.view, dirlit.direction, surf.normal);
    return diffuse + specular * specular_strength; //这里只考虑了材质本身
}

float3 GetBRDFColor(Surface surf, DirectionalLight dirlit, GI gi)
{
    // 考虑了烘焙光照的情况.
    float3 diffuse = surf.diffuse_reflectance * surf.color.rgb; //漫反射光.
    float3 specular = lerp(MIN_REFLECTIVITY, surf.color.rgb, surf.metallic);
    float specular_strength = GetSpecularStrength(surf.roughness, surf.view, dirlit.direction, surf.normal);
    return diffuse + specular * specular_strength; //这里只考虑了材质本身
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


// 间接brdf, 反射用.
float3 GetIndirectBRDF(Surface surf, float3 diffuse, float3 specular)
{
    float3 brdf_diffuse = GetBRDFDiffuse(surf);
    float3 brdf_specular = GetBRDFSpecular(surf);
    float3 reflection = specular * brdf_specular;
    reflection /= surf.roughness * surf.roughness + 1;
    float fresnelitem = surf.fresnelStrength * Pow4(1.0 - saturate(dot(surf.normal, surf.view)));  // 近似菲涅尔项
    return brdf_diffuse * diffuse + reflection;
}

#endif