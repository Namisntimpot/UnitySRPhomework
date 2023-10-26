using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class Shadow
{
    // 和Lighting类似，专门处理阴影.
    const string bufferName = "Shadows";
    CommandBuffer cmdbuf = new CommandBuffer() { name = bufferName };
    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings shadowSettings;

    //-------------------滤波相关的着色器 Keywork.---------------
    static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };  // 默认采样器就是 PCF2, 什么也不用做.

    //------------------层级间过渡方法的关键词--------------------
    static string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER",
    };

    //-------------------平行光阴影------------------------------
    const int maxShadowedDirectionalLightCount = 4;    //最大的能产生阴影的平行光的数量. 恰好可以将正方形纹理一分为四，所以四是有讲究的...
    struct ShadowedDirectionalLight    // 专门存储每个能投射阴影的平行光的信息.
    {
        public int visibleLightIndex;  // 这个能投射阴影的平行光在 cullingResults.visibleLight中的下标.
        public float slopeScaleBias;   // 每个光源的slope bias，可以逐光源指定，需要调整.
        public float nearPlaneOffset;  // 近平面偏移，用来处理跨越过大的大三角形的变形，原理不明... 它也是在light面板中可设置的.
    }
    ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount]; // 存储所有visibleLight的平行光的信息
    int shadowedDirectionalLightCount = 0;  //实际有的投射阴影的平行光.


    //-------------------Shadow Map在Shader中的属性---------------
    const int maxCascadesCount = 4;   // 最大级联阴影级联层数.
    static int directionalShadowAtlasID = Shader.PropertyToID("_DirectionalShadowAtlas");  // 存放平行光阴影图集的shader属性ID.  
    static int directionalShadowMatricesID = Shader.PropertyToID("_DirectionalShadowMatrices");  // 存放平行光阴影图从世界坐标到纹理空间的变换.
    static int cascadeCountID = Shader.PropertyToID("_CascadeCount");   // 级联的层级
    static int cascadeCullingSphereID = Shader.PropertyToID("_CascadeCullingSpheres");  // 确定级联采样层数用的culling spheres，保存为圆心xyz和作为半径的w
    static int shadowDistanceFadeID = Shader.PropertyToID("_ShadowDistanceFade");   // x:maxDistance, y:距离/最大距离超过1-y之后开始衰减过度
    static int cascadeDataID = Shader.PropertyToID("_CascadeDatas");  // 每一层 cascade shadow map 的一些辅助相关信息.
    static int shadowAtlasSizeID = Shader.PropertyToID("_ShadowAtlasSize");  // 阴影图的分辨率.

    static Matrix4x4[] directionalShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascadesCount];  // 脚本中计算并存放这些平行光阴影变换矩阵
    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascadesCount];
    static Vector4[] cascadeDatas = new Vector4[maxCascadesCount];

    // 用context, 裁剪结果、阴影设置初始化
    public void Setup(ref ScriptableRenderContext context, ref CullingResults cullingResults, ref ShadowSettings shadowSettings)
    {
        shadowedDirectionalLightCount = 0;
        this.context = context;
        this.cullingResults = cullingResults;
        this.shadowSettings = shadowSettings;
    }

    // 设置Keyword，确定使用哪个filter kernel. 注意这里面没有execute buffer
    void SetKeyword(string[] keywordGroup, int enableIndex)
    {
        for(int i=0; i<keywordGroup.Length; ++i)
        {
            if (i == enableIndex)
                cmdbuf.EnableShaderKeyword(keywordGroup[i]);
            else
                cmdbuf.DisableShaderKeyword(keywordGroup[i]);
        }
    }

    public Vector3 SetupDirectionalShadow(Light visible, int index)
    {
        /// 这一步存储、初始化将要投射阴影的平行光的信息.
        /// 认为visible是平行光不做检验，当visible满足下列条件，index为这个visiblelight在当前帧的visiblelight数组中下标.存储到Shadow内置的投射阴影平行光信息数组中：
        /// 1. 当前已有的投射阴影平行光数量小于最大数量
        /// 2. 该平行光开启了阴影投射
        /// 3. 该平行光的阴影投射强度大于0
        /// 4. 该平行光影响范围内有能投射阴影的物体（一这个物体要设置为receive shadow，二这个物体要在该光的distance范围内）.
        /// 返回值：如果该平行光有阴影，返回三维向量(阴影强度，这个灯光的级联阴影图小块编号的开始，该光源的normal bias rate) 阴影相关的数组中的下标就是这个光源在atlas中的小块的编号的开始!
        /// 这个返回值是逐光源的，而不是逐级联层级的.
        if (shadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
           visible.shadows != LightShadows.None &&
           visible.shadowStrength > 0f &&
           cullingResults.GetShadowCasterBounds(index, out Bounds outBounds)){
            // 最后一个函数，如果范围内有物体才会返回true. 其输出无用
            shadowedDirectionalLights[shadowedDirectionalLightCount] = new ShadowedDirectionalLight {
                visibleLightIndex = index,
                slopeScaleBias = visible.shadowBias,
                nearPlaneOffset = visible.shadowNearPlane,
            };
            // dirShadowinfo 是一个逐光源的信息，包含，
            Vector3 dirShadowinfo = new Vector3(
                visible.shadowStrength, 
                shadowedDirectionalLightCount * shadowSettings.directional.cascadeCount,
                visible.shadowNormalBias
                );
            shadowedDirectionalLightCount++;
            return dirShadowinfo;
        }
        else
        {
            return Vector3.zero;
        }
    }

    public void Render()
    {
        /// 该函数渲染各种类型的光源投射的阴影（shadow map atlas）.
        if(shadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            // 因为CameraRenderer的Render函数总会调用一次ReleaseShadowmap释放所有获取的阴影图，所以即使没有阴影，也给一个假的无用的临时渲染目标作为阴影图
            cmdbuf.GetTemporaryRT(directionalShadowAtlasID, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            // 这么做是为了兼容某些平台。后面着色器中当然会写从阴影图中采样的代码，GL2.0将采样器和纹理绑定，一旦有采样器（写了采样代码）而没有匹配的纹理，就会报错
            // 所以使用获取一个无用的临时渲染目标，而非用一个标记变量来处理没有阴影要渲染的情况.
        }
    }

    void RenderDirectionalShadows()
    {
        /// 渲染所有平行光投射的阴影.
        // 先获取存放阴影图的临时纹理.(RenderTarget)
        int atlasSize = ((int)shadowSettings.directional.atlasSize);  /// 获取分辨率
        cmdbuf.GetTemporaryRT(directionalShadowAtlasID, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap); ///32: DepthBuffer,什么来的
        cmdbuf.SetRenderTarget(directionalShadowAtlasID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        cmdbuf.ClearRenderTarget(true, false, Color.clear);  // 把刚才绑定过来的随意纹理清空，因为shadowmap只要深度，所以只清理depth即可，颜色无所谓.
        ExecuteBuffer();

        const string dirShadowSampleName = "Render Directional Light Shadow map";
        cmdbuf.BeginSample(dirShadowSampleName);
        ExecuteBuffer();

        // 如果有两个或以上平行光要投射阴影，就把纹理一分为四，即横纵都除以2.
        int tiles = shadowedDirectionalLightCount * shadowSettings.directional.cascadeCount;  // 整张图一共分成了这么多份.
        // 但是只能同时在横轴纵轴上同时均匀分，所以划分方案一定只有1，4，16，对应split是1，2，4
        int split = tiles <= 1 ? 1 : 
                    tiles <= 4 ? 2 : 4;
        
        int tileSize = ((int)shadowSettings.directional.atlasSize) / split;
        // 遍历所有投射阴影的平行光.
        for(int i=0; i<shadowedDirectionalLightCount; i++)
        {
            RenderDirectionalShadows(i, tileSize, split);
        }
        cmdbuf.SetGlobalVector(
            shadowDistanceFadeID, 
            new Vector4(
                1f/shadowSettings.maxDistance, 
                1f/shadowSettings.distanceFade,
                1f/(1f - (1f - shadowSettings.directional.cascadeFade)*(1f-shadowSettings.directional.cascadeFade)),
                0)
            );          // 把最大阴影深度, 衰减公式中的一些项传入GPU.
        cmdbuf.SetGlobalMatrixArray(directionalShadowMatricesID, directionalShadowMatrices);  // 将变换矩阵传入GPU.
        cmdbuf.SetGlobalInt(cascadeCountID, shadowSettings.directional.cascadeCount);
        cmdbuf.SetGlobalVectorArray(cascadeCullingSphereID, cascadeCullingSpheres);      // 把culling sphere 存进去.
        cmdbuf.SetGlobalVectorArray(cascadeDataID, cascadeDatas);
        cmdbuf.SetGlobalVector(shadowAtlasSizeID, new Vector4(atlasSize, 1f / atlasSize));
        SetKeyword(directionalFilterKeywords, (int)shadowSettings.directional.filter-1);   // 设置关键词。因为这个关键词是给之后的custom lit的pass用的，所以应该放在DrawShader完成之后
        SetKeyword(cascadeBlendKeywords, ((int)shadowSettings.directional.cascadeBlendMode) - 1);
        cmdbuf.EndSample(dirShadowSampleName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows(int index, int tileSize, int split)
    {
        /// 注意这个函数处理完一个光源的所有层级的cascade
        /// index: 平行光在shadowedDirectionalLights中的下标
        /// 因为是平行光，所以渲染的时候应该用正交投影。不过似乎Unity根据visibleLight的LightType自己处理了
        ShadowedDirectionalLight shadowedDirLight = shadowedDirectionalLights[index];
        ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, shadowedDirLight.visibleLightIndex);
        int cascadeCount = shadowSettings.directional.cascadeCount;
        Vector3 ratios = shadowSettings.directional.CascadeRatio;
        int tileIndexStart = index * cascadeCount;  //这个光源对应的cascadeCount块小块索引的开始

        // 如果保证一些低层级的物体已经在低级的级联阴影中，就不需要在高级（更大范围)的级联阴影中渲染它，可以提前剔除。通过下面的参数调整剔除的激进程度.
        float cascadeCullingFactor = Mathf.Max(0, 0.8f - shadowSettings.directional.cascadeFade);

        // 将光源假定为相机
        for(int cascadeLayer = 0; cascadeLayer < cascadeCount; ++cascadeLayer) {
            int tileIndex = tileIndexStart + cascadeLayer;
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                shadowedDirLight.visibleLightIndex, cascadeLayer, cascadeCount, ratios, tileSize, 
                shadowedDirectionalLights[index].nearPlaneOffset,
                out Matrix4x4 view, out Matrix4x4 proj, out ShadowSplitData splitData);
            // 第2-4个参数和级联阴影有关，近平面先不管. view:World to view space, proj: 正交投影矩阵. splitData: 该光源的阴影影响范围中物体被cull的信息.
            splitData.shadowCascadeBlendCullingFactor = cascadeCullingFactor;
            shadowDrawingSettings.splitData = splitData;
            if(index == 0)
            {
                // 设置每一层cascade相关的信息.
                SetCascadeDatas(cascadeLayer, splitData.cullingSphere, tileSize);
            }
            SetViewport(tileIndex, split, tileSize);             // 每个光源各取一个纹理的小部分.
            cmdbuf.SetViewProjectionMatrices(view, proj);    // 将这次渲染的"相机"移动到了光源处，并且使用正交投影！
            directionalShadowMatrices[tileIndex] = ConvertClipToAtlasTextureUV(proj * view, tileIndex, split);  // 注意之后将右乘一个向量，所以是先view没错.
            /// SetGlobalDepthDias中的第一个参数，将阴影投射点从光源开始往远推，这样得到的阴影距离会变长。这个值要设定地非常大才能完全消除斑纹，但会导致阴影偏移太多
            /// 第二个参数是斜率bias slope bias，会根据光线和法线的夹角动态地算bias，直射几乎没有bias，夹角越大bias越大，90度bias趋近无限。这个值只用很小即可有效.
            /// 只用逐光源人为调整的slope bias，而不用第一个参数。教程使用了其他“更直观”的实现——即在采样之前将面沿normal 做一定的 displacement.
            cmdbuf.SetGlobalDepthBias(0f, shadowedDirectionalLights[index].slopeScaleBias);    // shadow map bias.
            ExecuteBuffer();
            context.DrawShadows(ref shadowDrawingSettings);  // 渲染这个Shadermap! 注意它只会渲染有ShadowCaster Pass的material!
            cmdbuf.SetGlobalDepthBias(0f, 0f);
        }
    }

    void SetCascadeDatas(int cascadeLayer, Vector4 cullingSphere, float tileSize)
    {
        // 设置不同层的cascade相关的信息.
        // texelSize, shadow map texture中每个像素大概多大（**不知道这是咋算的**）
        // texelSize 需要随不同的filter级别而改变，卷积核越大texel size越大.
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)shadowSettings.directional.filter + 1f);
        // 把每一层的cullingSphere存进来
        cullingSphere.w -= filterSize;     // 卷积导致在计算边缘的阴影的时候可能会采样到剔除球之外，所以应该相应的减小剔除球的范围, 外面的就直接为0了.
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[cascadeLayer] = cullingSphere;
        cascadeDatas[cascadeLayer] = new Vector4(
            1f / cullingSphere.w,         // 这是给最外层剔除球衰减用的，也可以不用.
            filterSize * 1.4142136f
            );
    }

    void SetViewport(int index, int split, int tileSize)
    {
        /// index: 分块的阴影图中对应小块的索引，也是阴影图变换矩阵中的索引
        /// 把 wholeSize * wholeSize 的图片横竖都分为split份， 得到每个小块的分辨率是tileSize^2, 从左到右从下到上从零开始编号，设置视口为编号为 i 的小部分.
        int x = index % split, y = index / split;
        cmdbuf.SetViewport(new Rect(x * tileSize, y * tileSize, tileSize, tileSize));
    }

    public void ReleaseRT()
    {
        // 释放申请的临时渲染目标.
        cmdbuf.ReleaseTemporaryRT(directionalShadowAtlasID);
        ExecuteBuffer();
    }

    Matrix4x4 ConvertClipToAtlasTextureUV(Matrix4x4 m, int index, int split)
    {
        /// proj*view 变换到clip空间中，范围是-1，1，而深度图的texture的uv坐标是0,1，而且有Atlas的问题. 需要修改这个矩阵，让它能直接得到uv.  
        Matrix4x4 ret = m;
        if (SystemInfo.usesReversedZBuffer)
        {
            // 有些深度缓冲里面存储的是负数，所以应该把形成z坐标的那一行(行下标2)全部取反.
            ret.m20 = -ret.m20;     ret.m21 = -ret.m21;
            ret.m22 = -ret.m22;     ret.m23 = -ret.m23;
        }
        /// 坐标结果要从-1到1变换到0-1，除以2+0.5. 可以再左乘一个矩阵, 对角线和最后一列全是0.5，也可以拆开一行行写.
        /// 变换完了之后，还需要乘以Scale变成Atlas尺度下一场图的大小，然后移动offset*Scale到对应位置.
        /// 平行光深度图是正交投影没问题，如果是透视投影，Clip空间w不一定是1，不能直接乘以矩阵，还是要一行行写...?
        float scale = 1f / split;
        Vector2 offset = new Vector2(index % split, index / split);
        ret.m00 = (0.5f * (ret.m00 + ret.m30) + offset.x * ret.m30) * scale;
        ret.m01 = (0.5f * (ret.m01 + ret.m31) + offset.x * ret.m31) * scale;
        ret.m02 = (0.5f * (ret.m02 + ret.m32) + offset.x * ret.m32) * scale;
        ret.m03 = (0.5f * (ret.m03 + ret.m33) + offset.x * ret.m33) * scale;
        ret.m10 = (0.5f * (ret.m10 + ret.m30) + offset.y * ret.m30) * scale;
        ret.m11 = (0.5f * (ret.m11 + ret.m31) + offset.y * ret.m31) * scale;
        ret.m12 = (0.5f * (ret.m12 + ret.m32) + offset.y * ret.m32) * scale;
        ret.m13 = (0.5f * (ret.m13 + ret.m33) + offset.y * ret.m33) * scale;
        ret.m20 = 0.5f * (ret.m20 + ret.m30);
        ret.m21 = 0.5f * (ret.m21 + ret.m31);
        ret.m22 = 0.5f * (ret.m22 + ret.m32);
        ret.m23 = 0.5f * (ret.m23 + ret.m33);

        return ret;
    }
    
    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(cmdbuf);
        cmdbuf.Clear();
    }
}
