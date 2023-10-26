using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 存储阴影相关设置的容器，包括Shadowmap的最远距离、范围、分辨率等等.
// 也为级联阴影准备.

[System.Serializable]
public class ShadowSettings
{
    [Min(0f)]
    public float maxDistance = 20f;  //最大距离，默认100单位（100m）.
    [Range(0.001f, 1f)]
    public float distanceFade = 0.1f;  // 当距离达到最大距离的90%的时候，开始平滑过度.

    // 定义分辨率enum, TextureSize, for Shadow Map
    public enum TextureSize
    {
        _256 = 256, _512 = 512, _1024 = 1024, _2048 = 2048,
        _4096 = 4096, _8192 = 8192,
    }

    // 定义滤波
    public enum FilterMode
    {
        PCF2x2, PCF3x3, PCF5x5, PCF7x7,
    }

    [System.Serializable]
    public struct DirectionalShadowMap
    {
        public TextureSize atlasSize;   // 级联阴影图将放在一个纹理中. 所以叫atlas.
        public FilterMode filter;
        [Range(1, 4)]
        public int cascadeCount;   // 有了这个级联层数之后，相当于每个光源有count个不同层级的阴影图!
        [Range(0f, 1f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
        [Range(0.001f, 1f)]
        public float cascadeFade;   // 最外层剔除球的平滑过渡

        public enum CascadeBlendMode
        {
            Hard, Soft, Dither
        }   // hard表示不过滤. soft表示滤波，dither表示抖动替换.
        public CascadeBlendMode cascadeBlendMode;

        public Vector3 CascadeRatio => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
    }

    // 平行光的阴影图设置.
    public DirectionalShadowMap directional = new DirectionalShadowMap() {
        atlasSize = TextureSize._1024,
        filter = FilterMode.PCF2x2,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeFade = 0.1f,
        cascadeBlendMode = DirectionalShadowMap.CascadeBlendMode.Hard,
    };

    
}
