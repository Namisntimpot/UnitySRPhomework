// 主要是用来修改 emission 的GUI.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class GIShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);

        // 检查是否开启烘焙自发光.
        BakedEmission(ref materialEditor);
        // 为透明物体.直接将_BaseMap和_BaseColor复制到 _MainTex和_Color上. unity渲染透明meta只能用这个.
    }

    void BakedEmission(ref MaterialEditor materialEditor)
    {
        EditorGUI.BeginChangeCheck();
        // 检查下面的组件是否变化.
        materialEditor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            // 有变化.
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
            // 是透明且有纹理.
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
