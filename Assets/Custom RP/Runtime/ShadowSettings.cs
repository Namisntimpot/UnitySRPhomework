using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// �洢��Ӱ������õ�����������Shadowmap����Զ���롢��Χ���ֱ��ʵȵ�.
// ҲΪ������Ӱ׼��.

[System.Serializable]
public class ShadowSettings
{
    [Min(0f)]
    public float maxDistance = 20f;  //�����룬Ĭ��100��λ��100m��.
    [Range(0.001f, 1f)]
    public float distanceFade = 0.1f;  // ������ﵽ�������90%��ʱ�򣬿�ʼƽ������.

    // ����ֱ���enum, TextureSize, for Shadow Map
    public enum TextureSize
    {
        _256 = 256, _512 = 512, _1024 = 1024, _2048 = 2048,
        _4096 = 4096, _8192 = 8192,
    }

    // �����˲�
    public enum FilterMode
    {
        PCF2x2, PCF3x3, PCF5x5, PCF7x7,
    }

    [System.Serializable]
    public struct DirectionalShadowMap
    {
        public TextureSize atlasSize;   // ������Ӱͼ������һ��������. ���Խ�atlas.
        public FilterMode filter;
        [Range(1, 4)]
        public int cascadeCount;   // ���������������֮���൱��ÿ����Դ��count����ͬ�㼶����Ӱͼ!
        [Range(0f, 1f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
        [Range(0.001f, 1f)]
        public float cascadeFade;   // ������޳����ƽ������

        public enum CascadeBlendMode
        {
            Hard, Soft, Dither
        }   // hard��ʾ������. soft��ʾ�˲���dither��ʾ�����滻.
        public CascadeBlendMode cascadeBlendMode;

        public Vector3 CascadeRatio => new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
    }

    // ƽ�й����Ӱͼ����.
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
