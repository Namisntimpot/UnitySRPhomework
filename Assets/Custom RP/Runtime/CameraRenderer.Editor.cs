using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

public partial class CameraRenderer
{ 
    // �ɵ�Tags. ��ǰ��.
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
        // ��ȡerror���ʡ�
        if (errorMaterial == null)
        {
            errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        // ���Ʋ���֧�ֵ�shaders��һ���Ǿɰ��shaders.
        SortingSettings sortingSettings = new SortingSettings(m_camera);
        DrawingSettings drawingSettings = new DrawingSettings(unsupportedLegacyShaderTagIds[0], sortingSettings)
        {
            overrideMaterial = errorMaterial
        };
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;  //������������.

        // ѭ��ʣ�µ�. DrawingSettings�ڲ���Ȼ�и����飬���԰�˳����Ⱦ����в�ͬTags��Pass
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