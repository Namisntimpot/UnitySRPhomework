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

    public bool isActive => fxSettings != null;   //只有当fxSettings有效的时候才激活后处理栈.

    int fxSourceID = Shader.PropertyToID("_PostFXSource");  // 后处理中间过程用的临时渲染目标标识符.
    int fxSource2ID = Shader.PropertyToID("_PostFXSource2"); // 上升采样用的
    int bloomBicubicUpsamplingID = Shader.PropertyToID("_BloomBicubicUpsampling");
    int bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter");  // 先来一次半filter得到一半分辨率的，然后再真正bloom 它还有一个作用是筛选泛光阈值.
    int bloomThresholdId = Shader.PropertyToID("_BloomThreshold");  // 进行泛光的阈值
    int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");

    const int maxBloomPyramidLevel = 16;

    int bloomPyramidID;     // 泛光金字塔的逐层纹理，只用存储第一个即可，只要同时申请的，后面的纹理标识符都是递增的.

    // 指定后处理种类.
    enum Pass
    {
        BloomCombine,
        BloomHorizontal,  // 平行bloom
        BloomPrefilter,   // 预先过滤筛选泛光区域
        BloomVertical,    // 垂直bloom, 为什么要分开呢.
        Copy,    // 什么也不做，直接把原图复制到屏幕上.
    };

    public PostFXStack()
    {
        // 申请16张标识符连续的纹理
        bloomPyramidID = Shader.PropertyToID("_BloomPyramid0");
        for(int i=1; i<maxBloomPyramidLevel*2; ++i)
        {
            // 这里需要 *2, 因为bloom的水平竖直分开了.每次需要渲染两个纹理.
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings fxSettings)
    {
        this.context = context;
        this.camera = camera;
        this.fxSettings = camera.cameraType <= CameraType.SceneView ? fxSettings : null;  // 设置为null可以让后处理栈无效.
        ApplySceneViewStat();   //判断sceneview相机是否开启后处理.
    }

    public void Render(int sourceID)
    {
        /// sourceID: 前面的前向渲染管线中得到的渲染纹理id.
        //cmdbuf.Blit(sourceID, BuiltinRenderTextureType.CameraTarget);  // 目前只是把已有的渲染结果覆盖回去，什么也不做.
        //Draw(sourceID, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        DoBloom(sourceID);
        context.ExecuteCommandBuffer(cmdbuf);
        cmdbuf.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass fxpass)
    {
        // 把 id 为from的渲染纹理渲染到 to 上面去，并且使用 fxpass 的后处理种类.
        /// 获取后处理过程中用的临时渲染对象.
        cmdbuf.SetGlobalTexture(fxSourceID, from);   // 设置全局纹理，把前面渲染的结果（在渲染目标上）设置为全局纹理.(直接用屏幕坐标采样即可)
        // 这里是把 from 这张纹理放到了 _PostFXSource 里
        cmdbuf.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);  // 把to绑定为渲染目标.
        cmdbuf.DrawProcedural(Matrix4x4.identity, fxSettings.Material, (int)fxpass, MeshTopology.Triangles, 3);  // 一个三角形三个顶点.
    }

    void DoBloom(int sourceID)
    {
        PostFXSettings.BloomSettings bloomSettings = fxSettings.bloomSettings;
        cmdbuf.BeginSample("Post: Bloom");
        int width = camera.pixelWidth / 2, height = camera.pixelHeight / 2;
        // 如果完全不要泛光
        if(bloomSettings.maxIterations == 0 || width < bloomSettings.downScaleLimit * 2 
            || height < bloomSettings.downScaleLimit * 2 || bloomSettings.intensity <= 0f)
        {
            Draw(sourceID, BuiltinRenderTextureType.CameraTarget, Pass.Copy);  // 直接copy返回, 在下面会发生combine, 结果是两倍颜色.
            cmdbuf.EndSample("Post: Bloom");
            return;
        }
        RenderTextureFormat format = RenderTextureFormat.Default;
        // 泛光的阈值
        Vector4 threshold;
        //threshold.x = Mathf.GammaToLinearSpace(bloomSettings.threshold);
        threshold.x = bloomSettings.threshold;   // 我直接用的srgb，应该不用转化为线性空间?
        threshold.y = threshold.x * bloomSettings.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        cmdbuf.SetGlobalVector(bloomThresholdId, threshold);

        // 先来一个半分辨率的作为初始
        cmdbuf.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        Draw(sourceID, bloomPrefilterId, Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        int fromID = bloomPrefilterId, toID = bloomPyramidID;
        int i = 0;
        while(width >= bloomSettings.downScaleLimit && height >= bloomSettings.downScaleLimit && i < bloomSettings.maxIterations)
        {
            cmdbuf.GetTemporaryRT(toID, width, height, 0, FilterMode.Bilinear, format);  //把一个临时的渲染目标绑定在 toID 纹理上
            Draw(fromID, toID, Pass.BloomHorizontal);  // 进行一次后处理渲染.
            // 下一层
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
        cmdbuf.ReleaseTemporaryRT(bloomPrefilterId);  // 不再需要它了

        // 根据settings确定是否需要 bicubic 插值
        cmdbuf.SetGlobalFloat(bloomBicubicUpsamplingID, fxSettings.bloomBicubicUpsampling ? 1.0f : 0.0f);

        //Draw(fromID, BuiltinRenderTextureType.CameraTarget, Pass.Copy);  // 直接把最后一张复制到camera target上了.

        cmdbuf.SetGlobalFloat(bloomIntensityId, 1);  // 过程中，先把泛光强度设置为1

        // 只释放掉最后一轮horizontal, 前面的horizontal用作向上渲染回去.
        if (i > 1)  // 至少要有两轮才能进行金字塔上采样
        {
            cmdbuf.ReleaseTemporaryRT(fromID - 1);
            toID -= 4;  // 退回到倒数第二轮降采样的horizontal
            for (--i; i > 0; --i)
            {
                // 对于每轮迭代的i(start from 0)，2i为horizontal, 2i+1为vertical.
                // 现在升采样回去，fromID是原本每轮的vertical, toID是原本每轮的上一轮的horizontal
                // 每次释放掉每轮的vertical 和上一轮的vertical
                /// 要渲染掉的图是上一轮的 horizontal(toID)，以上一轮的 vertical（toid+1）作为要采样的全局纹理，
                /// 与这一轮的 vertical(fromID)(仅在最后一轮往前推；如果是在前面轮，就是与当前轮的horizontal(上一次的结果)) 做混合.
                /// 这一点，因为是fromID = toID 来更新，只用保证toID正确就好了
                cmdbuf.SetGlobalTexture(fxSource2ID, toID + 1);  // 把高分辨率那层的图放到 _PostFXSource2 里
                Draw(fromID, toID, Pass.BloomCombine);                 // 之后低分辨率的图就在 _PostFXSource 里
                cmdbuf.ReleaseTemporaryRT(fromID);  // 抛弃已经混合了的
                cmdbuf.ReleaseTemporaryRT(toID + 1);  // 抛弃刚才用来当作原图的
                fromID = toID;
                toID -= 2;
            }
        }
        else
        {
            // 直接释放fromID-1, 即金字塔根, 然后到下面去采样
            cmdbuf.ReleaseTemporaryRT(bloomPyramidID);
        }
        // 最后的fromID就是最开始的那个金字塔纹理
        /// 最后一步混合，使用泛光强度
        cmdbuf.SetGlobalFloat(bloomIntensityId, bloomSettings.intensity);
        cmdbuf.SetGlobalTexture(fxSource2ID, sourceID);
        Draw(fromID, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);  // 最后与原图做一次混合
        cmdbuf.ReleaseTemporaryRT(fromID);
        cmdbuf.EndSample("Post: Bloom");
    }
}
