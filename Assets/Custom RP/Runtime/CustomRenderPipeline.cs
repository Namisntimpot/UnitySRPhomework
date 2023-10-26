using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer cameraRenderer = new CameraRenderer();  // 相机渲染器, 用于其中Render()独立完整地渲染每个相机所见

    // 阴影设置
    ShadowSettings shadowSettings;

    // 后处理栈设置
    PostFXSettings postFXSettings;

    // 构造函数，用于启用一些特殊功能，如SRP Batcher等.
    public CustomRenderPipeline(ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
        this.shadowSettings = shadowSettings;
        this.postFXSettings = postFXSettings;
    }

    // 这个是好的
    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        //base.Render(context, cameras);
        // 直接挨个相机挨个相机的渲染.
        foreach(var camera in cameras)
        {
            cameraRenderer.Render(context, camera, shadowSettings, postFXSettings);
        }
    }

    // 这个可能造成内存泄漏
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        throw new System.NotImplementedException();
    }
}
