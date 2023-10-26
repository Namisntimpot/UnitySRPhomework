using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer cameraRenderer = new CameraRenderer();  // �����Ⱦ��, ��������Render()������������Ⱦÿ���������

    // ��Ӱ����
    ShadowSettings shadowSettings;

    // ����ջ����
    PostFXSettings postFXSettings;

    // ���캯������������һЩ���⹦�ܣ���SRP Batcher��.
    public CustomRenderPipeline(ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
        this.shadowSettings = shadowSettings;
        this.postFXSettings = postFXSettings;
    }

    // ����Ǻõ�
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        //base.Render(context, cameras);
        // ֱ�Ӱ�����������������Ⱦ.
        foreach(var camera in cameras)
        {
            cameraRenderer.Render(context, camera, shadowSettings, postFXSettings);
        }
    }

    // �����������ڴ�й©
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        throw new System.NotImplementedException();
    }
}
