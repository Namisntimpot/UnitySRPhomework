// ��Ҫ�������޸� emission ��GUI.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class GIShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);

        // ����Ƿ����決�Է���.
        BakedEmission(ref materialEditor);
        // Ϊ͸������.ֱ�ӽ�_BaseMap��_BaseColor���Ƶ� _MainTex��_Color��. unity��Ⱦ͸��metaֻ�������.
    }

    void BakedEmission(ref MaterialEditor materialEditor)
    {
        EditorGUI.BeginChangeCheck();
        // ������������Ƿ�仯.
        materialEditor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            // �б仯.
            foreach(Material mat in materialEditor.targets)
            {
                mat.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    void CopyMainTexAndColorForTransparent(ref MaterialProperty[] matProperties)
    {
        MaterialProperty mainTex = FindProperty("_MainTex", matProperties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", matProperties, false);
        if (mainTex != null && baseMap != null)
        {
            // ��͸����������.
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }
        MaterialProperty color = FindProperty("_Color", matProperties, false);
        MaterialProperty baseColor = FindProperty("_BaseColor", matProperties, false);
        if(color != null && baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
        }
    }
}
