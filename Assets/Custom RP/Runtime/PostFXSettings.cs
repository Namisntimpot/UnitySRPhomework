using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
public class PostFXSettings : ScriptableObject
{
    [SerializeField]
    Shader shader = default;

    [System.NonSerialized]
    Material material;

    public Material Material
    {
        get
        {
            if(material == null && shader != null)
            {
                material = new Material(shader);
                material.hideFlags = HideFlags.DontSave;   // ֻ����ʱ�Ĳ��ʣ���Ӧ����Asset��
            }
            return material;
        }
    }

    [System.Serializable]
    public struct BloomSettings
    {
        [Range(0, 16)]
        public int maxIterations;

        [Min(1)]
        public int downScaleLimit;

        // ���� bloom ��ֵ
        [Min(0f)]
        public float threshold;

        [Range(0f, 1f)]
        public float thresholdKnee;

        [Min(0f)]
        public float intensity;
    }

    [SerializeField]
    BloomSettings bloom = default;
    public BloomSettings bloomSettings => bloom;

    public bool bloomBicubicUpsampling = false;
}
