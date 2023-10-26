#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

// 反射率有最小，一般用0.04。（电介质啥啥理论）,而diffuse和specular率其实是划分了剩下的0.96
#define MIN_REFLECTIVITY 0.04

// 定义了一个表面的所有属性.
struct Surface
{
    float3 position;  // 世界坐标
    float4 color;
    float3 view;   // 视线方向.
    float3 normal; // 这里的normal应该保证标准化.
    float depth;   // 相机视角下的z坐标 的 相反数.
    // 切线，副切线...
    
    // metallic, smoothness可以指定.
    float metallic;
    float smoothness;
    
    // 表面的BRDF(伪)属性. 他们其实都是从上面的metallic, smoothness等计算出来的.
    float diffuse_reflectance; // 漫反射率. 直接乘以颜色.
    float reflectance;  // 反射率. 不使用.真正的做法是直接在0.04和surface.color间以metallic做插值. 然后乘以反射强度.
    float roughness; // 粗糙度
    
    // 用来层间抖动过渡.
    float dither;
};

Surface GetSurface(float3 positionWS, float4 positionCS, float4 color, float3 view, float3 normal, float metallic, float smoothness)
{
    // 充当了构造函数的作用.
    Surface surf;
    surf.position = positionWS;
    surf.color = color;
    surf.view = normalize(view);
    surf.normal = normalize(normal);
    surf.depth = -TransformWorldToView(positionWS).z;
    surf.metallic = metallic;
    surf.smoothness = smoothness;
    // 计算剩下的.
    surf.diffuse_reflectance = (1 - MIN_REFLECTIVITY) * (1 - metallic);  //除去固定的反射的0.04, 剩下的0.96中，diffuse的占比是1-metallic.
    // metallic指定了剩下的0.96中有多少是反射光.
    surf.reflectance = 1 - surf.diffuse_reflectance;
    
    float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    surf.roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    
    surf.dither = InterleavedGradientNoise(positionCS.xy, 0);
    return surf;
}

#endif