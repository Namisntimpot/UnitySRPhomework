using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    // ���������Ⱦһ�����֮����  ��ֻ��Editor�����õĲ�����CameraRenderer.Editor.cs��.
    ScriptableRenderContext m_context;
    Camera m_camera;

    // ���ݹ�����Ϣ��shader��.
    Lighting lighting = new Lighting();

    // ����ջ��ʵ��.
    PostFXStack postFXStack = new PostFXStack();

    // ��ʱ��ȾĿ��� id.
    static int framebufferID = Shader.PropertyToID("_CameraFrameBuffer");

    public void Render(ScriptableRenderContext context, Camera camera, ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        // �޳�����Ⱦ������...
        m_context = context;
        m_camera = camera;

        string bufName = "Render Camera";
        CommandBuffer commandBuffer = new CommandBuffer { name = bufName };

        //// ������ÿ�����scene��Ҳ����UIԪ��
        //if(m_camera.cameraType == CameraType.SceneView)
        //{
        //    ScriptableRenderContext.EmitWorldGeometryForSceneView(m_camera);
        //}

        // ----------------------------------------------------
        // �����޳�. ע�⣬��Ҫ����ShadowSettings��maxDistance����Ӱͼ�����޳�.
        ScriptableCullingParameters p;
        if(!m_camera.TryGetCullingParameters(out p))
        {
            return;  //�����⣬ֱ�ӷ���.
        }
        p.shadowDistance = Mathf.Min(shadowSettings.maxDistance, m_camera.farClipPlane);  // ��Ӱ�������ϣ�ѡ��Ӱ���õ�����������Զƽ���Сֵ.
        CullingResults cullingResults = m_context.Cull(ref p);

        // ��Ⱦ֮ǰ����ʼ��������Ϣ. ShadowmapӦ����������ȾͼƬ֮ǰ��ɣ����԰���һ����ǰ����ʼ��context�����Ϣ��������ȾGeometry֮ǰ
        const string shadowSampleName = "Render Shadowmap";
        commandBuffer.BeginSample(shadowSampleName);  // һЩdebug��Ϣ
        ExecuteBuffer(ref commandBuffer);
        lighting.Setup(context, cullingResults, shadowSettings);  // ���ù��պ�����Ӱͼ����Ϣ
        lighting.RenderShadowmap();     // ��Ⱦ��Ӱͼ.

        // ��ʼ������ջ.
        postFXStack.Setup(context, camera, postFXSettings);
        commandBuffer.EndSample(shadowSampleName);

        
        // -------------Render Setup--------------------------------------
        // ��Ⱦǰ��׼������������������׼�����������.
        // ������ȾĿ��, CameraClearFlags: skybox, depth, color, dont clear. <= depth��Ҫ����depth, <=colorҲҪ����color.
        CameraClearFlags cameraClearFlags = m_camera.clearFlags;


        // Ϊ���ú��������Ⱦ����������Ҫ�ѳ�ʼ����Ⱦ����ŵ�һ����ʱ����ȾĿ�꣨���ı�ʶ���� framebufferID����(�������ջ��Ч�Ļ�).
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
        );  //clear��color�ñ�����ɫ���.
        // �������λ�˵Ȳ���.
        context.SetupCameraProperties(camera);
        // ������ʼ��������Ⱦ������beginSample��debugger�ȿ�ʼ����.
        commandBuffer.BeginSample(bufName);   // �����е�name��������profile����.
        // ִ�������clearTarget, beginSample
        ExecuteBuffer(ref commandBuffer);

        //--------------Rendering---------------------------------
        // ��ʽ����Ⱦ����.
        // ���ƿɼ������壬˳��Ӧ���� ��͸������պ��ӡ�͸��.
        DrawVisibleGeometry(ref cullingResults);

        // ���Ʋ�֧�ֵģ��滻Ϊ������ʣ�Hidden/InternelErrorShader��
        DrawUnsupportedShaders(ref cullingResults);


        //��β֮ǰDrawGizmos.
        DrawGizmosBeforeFX();

        //------------- Post Processing--------------------------
        /// �����Postprocessing����ǰ����Ⱦ�Ľ��ȫ�� sourceIDΪ��ʶ������ȾĿ����.
        if (postFXStack.isActive)
        {
            postFXStack.Render(framebufferID);
        }

        DrawGizmosAfterFX();

        // ��Ⱦ�����������������Ӱͼ�������ʱrendertarget(depth)���Լ������õ���ʱframe buffer.
        Cleanup(ref commandBuffer);

        // �����β������debugger��������submit.
        commandBuffer.EndSample(bufName);
        ExecuteBuffer(ref commandBuffer);
        context.Submit();
    }

    void ExecuteBuffer(ref CommandBuffer cmd)
    {
        // ִ��cmd���������.
        m_context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }

    void DrawVisibleGeometry(ref CullingResults cullingResults)
    {
        // �����ǲ�͸�������壬������ôӽ���Զ.
        // DrawingSettings ���ڹ涨�Ƿ��������Ҫ��Ⱦ��ЩPass. ����camera�涨���ĸ����Ϊ��׼.
        SortingSettings sortingSettings = new SortingSettings(m_camera) {criteria = SortingCriteria.CommonOpaque }; //͸����ʱ����´ӽ���Զ.
        ShaderTagId unLitShaderTagID = new ShaderTagId("SRPDefaultUnlit");  //Ĭ���޹��յ�shadertag�������Դ��γ�pass���Ӽ�.
        DrawingSettings drawingSettings = new DrawingSettings(unLitShaderTagID, sortingSettings)
        {
            perObjectData = PerObjectData.Lightmaps | 
                            PerObjectData.LightProbe | 
                            PerObjectData.LightProbeProxyVolume | 
                            PerObjectData.ReflectionProbes
        };   // ֮��ͨ�����drawingset�����drawcall�Ļ����̬���嶨�� LIGHTMAP_ON shader_feature, ����̬����û��������壬��ʹ��probe.

        // �����ǲ�͸�������UnLit������ͬʱ��Ⱦ��͸������� CustomStandardLit.
        ShaderTagId customLitShaderTagID = new ShaderTagId("CustomLit");
        drawingSettings.SetShaderPassName(1, customLitShaderTagID);

        // FilteringSettings ����ѡ��Ҫ��Ⱦ��Щ����. ע��������캯����ָ��������ʲôҲ����Ⱦ��һ���в�����ʣ�µľ���ȫ������Ⱦ���Ǹ�.
        RenderQueueRange renderQueueRange = RenderQueueRange.opaque; //�Ȳ�͸������.
        FilteringSettings filteringSettings = new FilteringSettings(renderQueueRange);

        // drawrenderִ��
        m_context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);


        // ����ǻ���(�������ӽ��µ�)��պ�.
        m_context.DrawSkybox(m_camera);

        // Ȼ����͸�����壬RenderQueue = Transparent.
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        m_context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
    }

    void Cleanup(ref CommandBuffer cmdbuf)
    {
        // ��������һ������ʱ�������Ӱ��ͼ��һ���Ǻ����õ���ʱframebuffer.
        lighting.ClearShadowmap();
        if (postFXStack.isActive)
        {
            cmdbuf.ReleaseTemporaryRT(framebufferID);
        }
    }
}