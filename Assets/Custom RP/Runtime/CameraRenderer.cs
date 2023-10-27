using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    // 这个单独渲染一个相机之所见  其只在Editor下有用的部分在CameraRenderer.Editor.cs中.
    ScriptableRenderContext m_context;
    Camera m_camera;

    // 传递光照信息给shader用.
    Lighting lighting = new Lighting();

    // 后处理栈的实例.
    PostFXStack postFXStack = new PostFXStack();

    // 临时渲染目标的 id.
    static int framebufferID = Shader.PropertyToID("_CameraFrameBuffer");

    public void Render(ScriptableRenderContext context, Camera camera, ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        // 剔除，渲染，后处理...
        m_context = context;
        m_camera = camera;

        string bufName = "Render Camera";
        CommandBuffer commandBuffer = new CommandBuffer { name = bufName };

        //// 这个设置可以在scene中也看到UI元素
        //if(m_camera.cameraType == CameraType.SceneView)
        //{
        //    ScriptableRenderContext.EmitWorldGeometryForSceneView(m_camera);
        //}

        // ----------------------------------------------------
        // 首先剔除. 注意，需要根据ShadowSettings的maxDistance在阴影图上做剔除.
        ScriptableCullingParameters p;
        if(!m_camera.TryGetCullingParameters(out p))
        {
            return;  //有问题，直接返回.
        }
        p.shadowDistance = Mathf.Min(shadowSettings.maxDistance, m_camera.farClipPlane);  // 阴影最大距离上，选阴影设置的最大距离和相机远平面的小值.
        CullingResults cullingResults = m_context.Cull(ref p);

        // 渲染之前，初始化光照信息. Shadowmap应该在正常渲染图片之前完成，所以把这一段提前到初始化context相机信息并且在渲染Geometry之前
        const string shadowSampleName = "Render Shadowmap";
        commandBuffer.BeginSample(shadowSampleName);  // 一些debug信息
        ExecuteBuffer(ref commandBuffer);
        lighting.Setup(context, cullingResults, shadowSettings);  // 设置光照和其阴影图的信息
        lighting.RenderShadowmap();     // 渲染阴影图.

        // 初始化后处理栈.
        postFXStack.Setup(context, camera, postFXSettings);
        commandBuffer.EndSample(shadowSampleName);

        
        // -------------Render Setup--------------------------------------
        // 渲染前的准备工作，包括清屏、准备相机参数等.
        // 清理渲染目标, CameraClearFlags: skybox, depth, color, dont clear. <= depth都要清理depth, <=color也要清理color.
        CameraClearFlags cameraClearFlags = m_camera.clearFlags;


        // 为了让后处理接入渲染工作，我们要把初始的渲染结果放到一个临时的渲染目标（它的标识符是 framebufferID）上(如果后处理栈有效的话).
        if (postFXStack.isActive)
        {
            if(cameraClearFlags > CameraClearFlags.Color)
            {
                cameraClearFlags = CameraClearFlags.Color;
            }
            commandBuffer.GetTemporaryRT(
                framebufferID, camera.pixelWidth, camera.pixelHeight, 
                32, FilterMode.Bilinear, RenderTextureFormat.Default
            );
            commandBuffer.SetRenderTarget(framebufferID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }
        commandBuffer.ClearRenderTarget(
                cameraClearFlags <= CameraClearFlags.Depth,
                cameraClearFlags <= CameraClearFlags.Color,
                cameraClearFlags <= CameraClearFlags.Color ?  m_camera.backgroundColor.gamma : Color.clear
        );  //clear了color用背景颜色填充.
        // 设置相机位姿等参数.
        context.SetupCameraProperties(camera);
        // 即将开始真正的渲染工作，beginSample让debugger等开始工作.
        commandBuffer.BeginSample(bufName);   // 参数中的name仅用来给profile命名.
        // 执行上面的clearTarget, beginSample
        ExecuteBuffer(ref commandBuffer);

        //--------------Rendering---------------------------------
        // 正式的渲染工作.
        // 绘制可见几何体，顺序应该是 不透明、天空盒子、透明.
        DrawVisibleGeometry(ref cullingResults);

        // 绘制不支持的（替换为错误材质：Hidden/InternelErrorShader）
        DrawUnsupportedShaders(ref cullingResults);


        //收尾之前DrawGizmos.
        DrawGizmosBeforeFX();

        //------------- Post Processing--------------------------
        /// 如果有Postprocessing，则前面渲染的结果全在 sourceID为标识符的渲染目标上.
        if (postFXStack.isActive)
        {
            postFXStack.Render(framebufferID);
        }

        DrawGizmosAfterFX();

        // 渲染后的清理工作：清理阴影图申请的临时rendertarget(depth)，以及后处理用的临时frame buffer.
        Cleanup(ref commandBuffer);

        // 最后收尾，结束debugger采样，并submit.
        commandBuffer.EndSample(bufName);
        ExecuteBuffer(ref commandBuffer);
        context.Submit();
    }

    void ExecuteBuffer(ref CommandBuffer cmd)
    {
        // 执行cmd并且清空它.
        m_context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }

    void DrawVisibleGeometry(ref CullingResults cullingResults)
    {
        // 首先是不透明的物体，可以最好从近到远.
        // DrawingSettings 用于规定是否按深度排序、要渲染那些Pass. 传入camera规定以哪个相机为基准.
        SortingSettings sortingSettings = new SortingSettings(m_camera) {criteria = SortingCriteria.CommonOpaque }; //透明的时候大致从近到远.
        ShaderTagId unLitShaderTagID = new ShaderTagId("SRPDefaultUnlit");  //默认无光照的shadertag，用于以此形成pass的子集.
        DrawingSettings drawingSettings = new DrawingSettings(unLitShaderTagID, sortingSettings)
        {
            perObjectData = PerObjectData.Lightmaps | 
                            PerObjectData.LightProbe | 
                            PerObjectData.LightProbeProxyVolume | 
                            PerObjectData.ReflectionProbes
        };   // 之后，通过这个drawingset唤起的drawcall的会给静态物体定义 LIGHTMAP_ON shader_feature, 而动态物体没有这个定义，就使用probe.

        // 上面是不透明物体的UnLit，下面同时渲染不透明物体的 CustomStandardLit.
        ShaderTagId customLitShaderTagID = new ShaderTagId("CustomLit");
        drawingSettings.SetShaderPassName(1, customLitShaderTagID);

        // FilteringSettings 用于选择要渲染哪些物体. 注意如果构造函数不指定参数则什么也不渲染，一旦有参数，剩下的就是全部都渲染的那个.
        RenderQueueRange renderQueueRange = RenderQueueRange.opaque; //先不透明物体.
        FilteringSettings filteringSettings = new FilteringSettings(renderQueueRange);

        // drawrender执行
        m_context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);


        // 其次是绘制(这个相机视角下的)天空盒.
        m_context.DrawSkybox(m_camera);

        // 然后是透明物体，RenderQueue = Transparent.
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        m_context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void Cleanup(ref CommandBuffer cmdbuf)
    {
        // 清理工作。一个是临时申请的阴影贴图，一个是后处理用的临时framebuffer.
        lighting.ClearShadowmap();
        if (postFXStack.isActive)
        {
            cmdbuf.ReleaseTemporaryRT(framebufferID);
        }
    }
}