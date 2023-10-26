using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    const string bufName = "Post FX";
    CommandBuffer cmdbuf = new CommandBuffer() { name = bufName };

    ScriptableRenderContext context;
    Camera camera;
    PostFXSettings fxSettings;

    public bool isActive => fxSettings != null;   //ֻ�е�fxSettings��Ч��ʱ��ż������ջ.

    int fxSourceID = Shader.PropertyToID("_PostFXSource");  // �����м�����õ���ʱ��ȾĿ���ʶ��.
    int fxSource2ID = Shader.PropertyToID("_PostFXSource2"); // ���������õ�
    int bloomBicubicUpsamplingID = Shader.PropertyToID("_BloomBicubicUpsampling");
    int bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter");  // ����һ�ΰ�filter�õ�һ��ֱ��ʵģ�Ȼ��������bloom ������һ��������ɸѡ������ֵ.
    int bloomThresholdId = Shader.PropertyToID("_BloomThreshold");  // ���з������ֵ
    int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");

    const int maxBloomPyramidLevel = 16;

    int bloomPyramidID;     // ������������������ֻ�ô洢��һ�����ɣ�ֻҪͬʱ����ģ�����������ʶ�����ǵ�����.

    // ָ����������.
    enum Pass
    {
        BloomCombine,
        BloomHorizontal,  // ƽ��bloom
        BloomPrefilter,   // Ԥ�ȹ���ɸѡ��������
        BloomVertical,    // ��ֱbloom, ΪʲôҪ�ֿ���.
        Copy,    // ʲôҲ������ֱ�Ӱ�ԭͼ���Ƶ���Ļ��.
    };

    public PostFXStack()
    {
        // ����16�ű�ʶ������������
        bloomPyramidID = Shader.PropertyToID("_BloomPyramid0");
        for(int i=1; i<maxBloomPyramidLevel*2; ++i)
        {
            // ������Ҫ *2, ��Ϊbloom��ˮƽ��ֱ�ֿ���.ÿ����Ҫ��Ⱦ��������.
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings fxSettings)
    {
        this.context = context;
        this.camera = camera;
        this.fxSettings = camera.cameraType <= CameraType.SceneView ? fxSettings : null;  // ����Ϊnull�����ú���ջ��Ч.
        ApplySceneViewStat();   //�ж�sceneview����Ƿ�������.
    }

    public void Render(int sourceID)
    {
        /// sourceID: ǰ���ǰ����Ⱦ�����еõ�����Ⱦ����id.
        //cmdbuf.Blit(sourceID, BuiltinRenderTextureType.CameraTarget);  // Ŀǰֻ�ǰ����е���Ⱦ������ǻ�ȥ��ʲôҲ����.
        //Draw(sourceID, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        DoBloom(sourceID);
        context.ExecuteCommandBuffer(cmdbuf);
        cmdbuf.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass fxpass)
    {
        // �� id Ϊfrom����Ⱦ������Ⱦ�� to ����ȥ������ʹ�� fxpass �ĺ�������.
        /// ��ȡ����������õ���ʱ��Ⱦ����.
        cmdbuf.SetGlobalTexture(fxSourceID, from);   // ����ȫ��������ǰ����Ⱦ�Ľ��������ȾĿ���ϣ�����Ϊȫ������.(ֱ������Ļ�����������)
        // �����ǰ� from ��������ŵ��� _PostFXSource ��
        cmdbuf.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);  // ��to��Ϊ��ȾĿ��.
        cmdbuf.DrawProcedural(Matrix4x4.identity, fxSettings.Material, (int)fxpass, MeshTopology.Triangles, 3);  // һ����������������.
    }

    void DoBloom(int sourceID)
    {
        PostFXSettings.BloomSettings bloomSettings = fxSettings.bloomSettings;
        cmdbuf.BeginSample("Post: Bloom");
        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
        // �����ȫ��Ҫ����
        if(bloomSettings.maxIterations == 0 || width < bloomSettings.downScaleLimit * 2 
            || height < bloomSettings.downScaleLimit * 2 || bloomSettings.intensity <= 0f)
        {
            Draw(sourceID, BuiltinRenderTextureType.CameraTarget, Pass.Copy);  // ֱ��copy����, ������ᷢ��combine, �����������ɫ.
            cmdbuf.EndSample("Post: Bloom");
            return;
        }
        RenderTextureFormat format = RenderTextureFormat.Default;
        // �������ֵ
        Vector4 threshold;
        //threshold.x = Mathf.GammaToLinearSpace(bloomSettings.threshold);
        threshold.x = bloomSettings.threshold;   // ��ֱ���õ�srgb��Ӧ�ò���ת��Ϊ���Կռ�?
        threshold.y = threshold.x * bloomSettings.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        cmdbuf.SetGlobalVector(bloomThresholdId, threshold);

        // ����һ����ֱ��ʵ���Ϊ��ʼ
        cmdbuf.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        Draw(sourceID, bloomPrefilterId, Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        int fromID = bloomPrefilterId, toID = bloomPyramidID;
        int i = 0;
        while(width >= bloomSettings.downScaleLimit && height >= bloomSettings.downScaleLimit && i < bloomSettings.maxIterations)
        {
            cmdbuf.GetTemporaryRT(toID, width, height, 0, FilterMode.Bilinear, format);  //��һ����ʱ����ȾĿ����� toID ������
            Draw(fromID, toID, Pass.BloomHorizontal);  // ����һ�κ�����Ⱦ.
            // ��һ��
            fromID = toID;
            toID += 1;
            cmdbuf.GetTemporaryRT(toID, width, height, 0, FilterMode.Bilinear, format);
            Draw(fromID, toID, Pass.BloomVertical);
            fromID = toID;
            toID += 1;
            width /= 2;
            height /= 2;
            ++i;
        }
        cmdbuf.ReleaseTemporaryRT(bloomPrefilterId);  // ������Ҫ����

        // ����settingsȷ���Ƿ���Ҫ bicubic ��ֵ
        cmdbuf.SetGlobalFloat(bloomBicubicUpsamplingID, fxSettings.bloomBicubicUpsampling ? 1.0f : 0.0f);

        //Draw(fromID, BuiltinRenderTextureType.CameraTarget, Pass.Copy);  // ֱ�Ӱ����һ�Ÿ��Ƶ�camera target����.

        cmdbuf.SetGlobalFloat(bloomIntensityId, 1);  // �����У��Ȱѷ���ǿ������Ϊ1

        // ֻ�ͷŵ����һ��horizontal, ǰ���horizontal����������Ⱦ��ȥ.
        if (i > 1)  // ����Ҫ�����ֲ��ܽ��н������ϲ���
        {
            cmdbuf.ReleaseTemporaryRT(fromID - 1);
            toID -= 4;  // �˻ص������ڶ��ֽ�������horizontal
            for (--i; i > 0; --i)
            {
                // ����ÿ�ֵ�����i(start from 0)��2iΪhorizontal, 2i+1Ϊvertical.
                // ������������ȥ��fromID��ԭ��ÿ�ֵ�vertical, toID��ԭ��ÿ�ֵ���һ�ֵ�horizontal
                // ÿ���ͷŵ�ÿ�ֵ�vertical ����һ�ֵ�vertical
                /// Ҫ��Ⱦ����ͼ����һ�ֵ� horizontal(toID)������һ�ֵ� vertical��toid+1����ΪҪ������ȫ������
                /// ����һ�ֵ� vertical(fromID)(�������һ����ǰ�ƣ��������ǰ���֣������뵱ǰ�ֵ�horizontal(��һ�εĽ��)) �����.
                /// ��һ�㣬��Ϊ��fromID = toID �����£�ֻ�ñ�֤toID��ȷ�ͺ���
                cmdbuf.SetGlobalTexture(fxSource2ID, toID + 1);  // �Ѹ߷ֱ����ǲ��ͼ�ŵ� _PostFXSource2 ��
                Draw(fromID, toID, Pass.BloomCombine);                 // ֮��ͷֱ��ʵ�ͼ���� _PostFXSource ��
                cmdbuf.ReleaseTemporaryRT(fromID);  // �����Ѿ�����˵�
                cmdbuf.ReleaseTemporaryRT(toID + 1);  // �����ղ���������ԭͼ��
                fromID = toID;
                toID -= 2;
            }
        }
        else
        {
            // ֱ���ͷ�fromID-1, ����������, Ȼ������ȥ����
            cmdbuf.ReleaseTemporaryRT(bloomPyramidID);
        }
        // ����fromID�����ʼ���Ǹ�����������
        /// ���һ����ϣ�ʹ�÷���ǿ��
        cmdbuf.SetGlobalFloat(bloomIntensityId, bloomSettings.intensity);
        cmdbuf.SetGlobalTexture(fxSource2ID, sourceID);
        Draw(fromID, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);  // �����ԭͼ��һ�λ��
        cmdbuf.ReleaseTemporaryRT(fromID);
        cmdbuf.EndSample("Post: Bloom");
    }
}
