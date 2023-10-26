using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public partial class CameraRenderer
{ 
    // 旧的Tags. 以前的.
    static ShaderTagId[] unsupportedLegacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };

    static Material errorMaterial = null;

    partial void DrawUnsupportedShaders(ref CullingResults cullingResults);

    partial void DrawGizmosBeforeFX();
    partial void DrawGizmosAfterFX();

#if UNITY_EDITOR
    partial void DrawUnsupportedShaders(ref CullingResults cullingResults)
    {
        // 获取error材质。
        if (errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        // 绘制不被支持的shaders，一般是旧版的shaders.
        SortingSettings sortingSettings = new SortingSettings(m_camera);
        DrawingSettings drawingSettings = new DrawingSettings(unsupportedLegacyShaderTagIds[0], sortingSettings)
        {
            overrideMaterial = errorMaterial
        };
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;  //包含所有物体.

        // 循环剩下的. DrawingSettings内部显然有个数组，可以按顺序渲染多个有不同Tags的Pass
        for (int i = 1; i < unsupportedLegacyShaderTagIds.Length; ++i)
        {
            drawingSettings.SetShaderPassName(i, unsupportedLegacyShaderTagIds[i]);
        }
        m_context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    partial void DrawGizmosBeforeFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            m_context.DrawGizmos(m_camera, GizmoSubset.PreImageEffects);
            //m_context.DrawGizmos(m_camera, GizmoSubset.PostImageEffects);
        }
    }

    partial void DrawGizmosAfterFX()
    {
        if (Handles.ShouldRenderGizmos())
        {
            //m_context.DrawGizmos(m_camera, GizmoSubset.PreImageEffects);
            m_context.DrawGizmos(m_camera, GizmoSubset.PostImageEffects);
        }
    }

#endif
}