#ifndef CUSTOM_SURFACE_INCLUDED
#define CUSTOM_SURFACE_INCLUDED

// ����������С��һ����0.04���������ɶɶ���ۣ�,��diffuse��specular����ʵ�ǻ�����ʣ�µ�0.96
#define MIN_REFLECTIVITY 0.04

// ������һ���������������.
struct Surface
{
    float3 position;  // ��������
    float4 color;
    float3 view;   // ���߷���.
    float3 normal; // �����normalӦ�ñ�֤��׼��.
    float depth;   // ����ӽ��µ�z���� �� �෴��.
    // ���ߣ�������...
    
    // metallic, smoothness����ָ��.
    float metallic;
    float smoothness;
    
    // �����BRDF(α)����. ������ʵ���Ǵ������metallic, smoothness�ȼ��������.
    float diffuse_reflectance; // ��������. ֱ�ӳ�����ɫ.
    float reflectance;  // ������. ��ʹ��.������������ֱ����0.04��surface.color����metallic����ֵ. Ȼ����Է���ǿ��.
    float roughness; // �ֲڶ�
    
    // ������䶶������.
    float dither;
};

Surface GetSurface(float3 positionWS, float4 positionCS, float4 color, float3 view, float3 normal, float metallic, float smoothness)
{
    // �䵱�˹��캯��������.
    Surface surf;
    surf.position = positionWS;
    surf.color = color;
    surf.view = normalize(view);
    surf.normal = normalize(normal);
    surf.depth = -TransformWorldToView(positionWS).z;
    surf.metallic = metallic;
    surf.smoothness = smoothness;
    // ����ʣ�µ�.
    surf.diffuse_reflectance = (1 - MIN_REFLECTIVITY) * (1 - metallic);  //��ȥ�̶��ķ����0.04, ʣ�µ�0.96�У�diffuse��ռ����1-metallic.
    // metallicָ����ʣ�µ�0.96���ж����Ƿ����.
    surf.reflectance = 1 - surf.diffuse_reflectance;
    
    float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
    surf.roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    
    surf.dither = InterleavedGradientNoise(positionCS.xy, 0);
    return surf;
}

#endif