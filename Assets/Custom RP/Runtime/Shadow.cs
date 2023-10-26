using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class Shadow
{
    // ��Lighting���ƣ�ר�Ŵ�����Ӱ.
    const string bufferName = "Shadows";
    CommandBuffer cmdbuf = new CommandBuffer() { name = bufferName };
    ScriptableRenderContext context;
    CullingResults cullingResults;
    ShadowSettings shadowSettings;

    //-------------------�˲���ص���ɫ�� Keywork.---------------
    static string[] directionalFilterKeywords =
    {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };  // Ĭ�ϲ��������� PCF2, ʲôҲ������.

    //------------------�㼶����ɷ����Ĺؼ���--------------------
    static string[] cascadeBlendKeywords =
    {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER",
    };

    //-------------------ƽ�й���Ӱ------------------------------
    const int maxShadowedDirectionalLightCount = 4;    //�����ܲ�����Ӱ��ƽ�й������. ǡ�ÿ��Խ�����������һ��Ϊ�ģ����������н�����...
    struct ShadowedDirectionalLight    // ר�Ŵ洢ÿ����Ͷ����Ӱ��ƽ�й����Ϣ.
    {
        public int visibleLightIndex;  // �����Ͷ����Ӱ��ƽ�й��� cullingResults.visibleLight�е��±�.
        public float slopeScaleBias;   // ÿ����Դ��slope bias���������Դָ������Ҫ����.
        public float nearPlaneOffset;  // ��ƽ��ƫ�ƣ����������Խ����Ĵ������εı��Σ�ԭ����... ��Ҳ����light����п����õ�.
    }
    ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount]; // �洢����visibleLight��ƽ�й����Ϣ
    int shadowedDirectionalLightCount = 0;  //ʵ���е�Ͷ����Ӱ��ƽ�й�.


    //-------------------Shadow Map��Shader�е�����---------------
    const int maxCascadesCount = 4;   // �������Ӱ��������.
    static int directionalShadowAtlasID = Shader.PropertyToID("_DirectionalShadowAtlas");  // ���ƽ�й���Ӱͼ����shader����ID.  
    static int directionalShadowMatricesID = Shader.PropertyToID("_DirectionalShadowMatrices");  // ���ƽ�й���Ӱͼ���������굽����ռ�ı任.
    static int cascadeCountID = Shader.PropertyToID("_CascadeCount");   // �����Ĳ㼶
    static int cascadeCullingSphereID = Shader.PropertyToID("_CascadeCullingSpheres");  // ȷ���������������õ�culling spheres������ΪԲ��xyz����Ϊ�뾶��w
    static int shadowDistanceFadeID = Shader.PropertyToID("_ShadowDistanceFade");   // x:maxDistance, y:����/�����볬��1-y֮��ʼ˥������
    static int cascadeDataID = Shader.PropertyToID("_CascadeDatas");  // ÿһ�� cascade shadow map ��һЩ���������Ϣ.
    static int shadowAtlasSizeID = Shader.PropertyToID("_ShadowAtlasSize");  // ��Ӱͼ�ķֱ���.

    static Matrix4x4[] directionalShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascadesCount];  // �ű��м��㲢�����Щƽ�й���Ӱ�任����
    static Vector4[] cascadeCullingSpheres = new Vector4[maxCascadesCount];
    static Vector4[] cascadeDatas = new Vector4[maxCascadesCount];

    // ��context, �ü��������Ӱ���ó�ʼ��
    public void Setup(ref ScriptableRenderContext context, ref CullingResults cullingResults, ref ShadowSettings shadowSettings)
    {
        shadowedDirectionalLightCount = 0;
        this.context = context;
        this.cullingResults = cullingResults;
        this.shadowSettings = shadowSettings;
    }

    // ����Keyword��ȷ��ʹ���ĸ�filter kernel. ע��������û��execute buffer
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
        /// ��һ���洢����ʼ����ҪͶ����Ӱ��ƽ�й����Ϣ.
        /// ��Ϊvisible��ƽ�йⲻ�����飬��visible��������������indexΪ���visiblelight�ڵ�ǰ֡��visiblelight�������±�.�洢��Shadow���õ�Ͷ����Ӱƽ�й���Ϣ�����У�
        /// 1. ��ǰ���е�Ͷ����Ӱƽ�й�����С���������
        /// 2. ��ƽ�й⿪������ӰͶ��
        /// 3. ��ƽ�й����ӰͶ��ǿ�ȴ���0
        /// 4. ��ƽ�й�Ӱ�췶Χ������Ͷ����Ӱ�����壨һ�������Ҫ����Ϊreceive shadow�����������Ҫ�ڸù��distance��Χ�ڣ�.
        /// ����ֵ�������ƽ�й�����Ӱ��������ά����(��Ӱǿ�ȣ�����ƹ�ļ�����ӰͼС���ŵĿ�ʼ���ù�Դ��normal bias rate) ��Ӱ��ص������е��±���������Դ��atlas�е�С��ı�ŵĿ�ʼ!
        /// �������ֵ�����Դ�ģ������������㼶��.
        if (shadowedDirectionalLightCount < maxShadowedDirectionalLightCount &&
           visible.shadows != LightShadows.None &&
           visible.shadowStrength > 0f &&
           cullingResults.GetShadowCasterBounds(index, out Bounds outBounds)){
            // ���һ�������������Χ��������Ż᷵��true. ���������
            shadowedDirectionalLights[shadowedDirectionalLightCount] = new ShadowedDirectionalLight {
                visibleLightIndex = index,
                slopeScaleBias = visible.shadowBias,
                nearPlaneOffset = visible.shadowNearPlane,
            };
            // dirShadowinfo ��һ�����Դ����Ϣ��������
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
        /// �ú�����Ⱦ�������͵Ĺ�ԴͶ�����Ӱ��shadow map atlas��.
        if(shadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else
        {
            // ��ΪCameraRenderer��Render�����ܻ����һ��ReleaseShadowmap�ͷ����л�ȡ����Ӱͼ�����Լ�ʹû����Ӱ��Ҳ��һ���ٵ����õ���ʱ��ȾĿ����Ϊ��Ӱͼ
            cmdbuf.GetTemporaryRT(directionalShadowAtlasID, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            // ��ô����Ϊ�˼���ĳЩƽ̨��������ɫ���е�Ȼ��д����Ӱͼ�в����Ĵ��룬GL2.0��������������󶨣�һ���в�������д�˲������룩��û��ƥ��������ͻᱨ��
            // ����ʹ�û�ȡһ�����õ���ʱ��ȾĿ�꣬������һ����Ǳ���������û����ӰҪ��Ⱦ�����.
        }
    }

    void RenderDirectionalShadows()
    {
        /// ��Ⱦ����ƽ�й�Ͷ�����Ӱ.
        // �Ȼ�ȡ�����Ӱͼ����ʱ����.(RenderTarget)
        int atlasSize = ((int)shadowSettings.directional.atlasSize);  /// ��ȡ�ֱ���
        cmdbuf.GetTemporaryRT(directionalShadowAtlasID, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap); ///32: DepthBuffer,ʲô����
        cmdbuf.SetRenderTarget(directionalShadowAtlasID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        cmdbuf.ClearRenderTarget(true, false, Color.clear);  // �ѸղŰ󶨹���������������գ���ΪshadowmapֻҪ��ȣ�����ֻ����depth���ɣ���ɫ����ν.
        ExecuteBuffer();

        const string dirShadowSampleName = "Render Directional Light Shadow map";
        cmdbuf.BeginSample(dirShadowSampleName);
        ExecuteBuffer();

        // ���������������ƽ�й�ҪͶ����Ӱ���Ͱ�����һ��Ϊ�ģ������ݶ�����2.
        int tiles = shadowedDirectionalLightCount * shadowSettings.directional.cascadeCount;  // ����ͼһ���ֳ�����ô���.
        // ����ֻ��ͬʱ�ں���������ͬʱ���ȷ֣����Ի��ַ���һ��ֻ��1��4��16����Ӧsplit��1��2��4
        int split = tiles <= 1 ? 1 : 
                    tiles <= 4 ? 2 : 4;
        
        int tileSize = ((int)shadowSettings.directional.atlasSize) / split;
        // ��������Ͷ����Ӱ��ƽ�й�.
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
            );          // �������Ӱ���, ˥����ʽ�е�һЩ���GPU.
        cmdbuf.SetGlobalMatrixArray(directionalShadowMatricesID, directionalShadowMatrices);  // ���任������GPU.
        cmdbuf.SetGlobalInt(cascadeCountID, shadowSettings.directional.cascadeCount);
        cmdbuf.SetGlobalVectorArray(cascadeCullingSphereID, cascadeCullingSpheres);      // ��culling sphere ���ȥ.
        cmdbuf.SetGlobalVectorArray(cascadeDataID, cascadeDatas);
        cmdbuf.SetGlobalVector(shadowAtlasSizeID, new Vector4(atlasSize, 1f / atlasSize));
        SetKeyword(directionalFilterKeywords, (int)shadowSettings.directional.filter-1);   // ���ùؼ��ʡ���Ϊ����ؼ����Ǹ�֮���custom lit��pass�õģ�����Ӧ�÷���DrawShader���֮��
        SetKeyword(cascadeBlendKeywords, ((int)shadowSettings.directional.cascadeBlendMode) - 1);
        cmdbuf.EndSample(dirShadowSampleName);
        ExecuteBuffer();
    }

    void RenderDirectionalShadows(int index, int tileSize, int split)
    {
        /// ע���������������һ����Դ�����в㼶��cascade
        /// index: ƽ�й���shadowedDirectionalLights�е��±�
        /// ��Ϊ��ƽ�й⣬������Ⱦ��ʱ��Ӧ��������ͶӰ�������ƺ�Unity����visibleLight��LightType�Լ�������
        ShadowedDirectionalLight shadowedDirLight = shadowedDirectionalLights[index];
        ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(cullingResults, shadowedDirLight.visibleLightIndex);
        int cascadeCount = shadowSettings.directional.cascadeCount;
        Vector3 ratios = shadowSettings.directional.CascadeRatio;
        int tileIndexStart = index * cascadeCount;  //�����Դ��Ӧ��cascadeCount��С�������Ŀ�ʼ

        // �����֤һЩ�Ͳ㼶�������Ѿ��ڵͼ��ļ�����Ӱ�У��Ͳ���Ҫ�ڸ߼�������Χ)�ļ�����Ӱ����Ⱦ����������ǰ�޳���ͨ������Ĳ��������޳��ļ����̶�.
        float cascadeCullingFactor = Mathf.Max(0, 0.8f - shadowSettings.directional.cascadeFade);

        // ����Դ�ٶ�Ϊ���
        for(int cascadeLayer = 0; cascadeLayer < cascadeCount; ++cascadeLayer) {
            int tileIndex = tileIndexStart + cascadeLayer;
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                shadowedDirLight.visibleLightIndex, cascadeLayer, cascadeCount, ratios, tileSize, 
                shadowedDirectionalLights[index].nearPlaneOffset,
                out Matrix4x4 view, out Matrix4x4 proj, out ShadowSplitData splitData);
            // ��2-4�������ͼ�����Ӱ�йأ���ƽ���Ȳ���. view:World to view space, proj: ����ͶӰ����. splitData: �ù�Դ����ӰӰ�췶Χ�����屻cull����Ϣ.
            splitData.shadowCascadeBlendCullingFactor = cascadeCullingFactor;
            shadowDrawingSettings.splitData = splitData;
            if(index == 0)
            {
                // ����ÿһ��cascade��ص���Ϣ.
                SetCascadeDatas(cascadeLayer, splitData.cullingSphere, tileSize);
            }
            SetViewport(tileIndex, split, tileSize);             // ÿ����Դ��ȡһ�������С����.
            cmdbuf.SetViewProjectionMatrices(view, proj);    // �������Ⱦ��"���"�ƶ����˹�Դ��������ʹ������ͶӰ��
            directionalShadowMatrices[tileIndex] = ConvertClipToAtlasTextureUV(proj * view, tileIndex, split);  // ע��֮���ҳ�һ����������������viewû��.
            /// SetGlobalDepthDias�еĵ�һ������������ӰͶ���ӹ�Դ��ʼ��Զ�ƣ������õ�����Ӱ�����䳤�����ֵҪ�趨�طǳ��������ȫ�������ƣ����ᵼ����Ӱƫ��̫��
            /// �ڶ���������б��bias slope bias������ݹ��ߺͷ��ߵļнǶ�̬����bias��ֱ�伸��û��bias���н�Խ��biasԽ��90��bias�������ޡ����ֵֻ�ú�С������Ч.
            /// ֻ�����Դ��Ϊ������slope bias�������õ�һ���������̳�ʹ������������ֱ�ۡ���ʵ�֡������ڲ���֮ǰ������normal ��һ���� displacement.
            cmdbuf.SetGlobalDepthBias(0f, shadowedDirectionalLights[index].slopeScaleBias);    // shadow map bias.
            ExecuteBuffer();
            context.DrawShadows(ref shadowDrawingSettings);  // ��Ⱦ���Shadermap! ע����ֻ����Ⱦ��ShadowCaster Pass��material!
            cmdbuf.SetGlobalDepthBias(0f, 0f);
        }
    }

    void SetCascadeDatas(int cascadeLayer, Vector4 cullingSphere, float tileSize)
    {
        // ���ò�ͬ���cascade��ص���Ϣ.
        // texelSize, shadow map texture��ÿ�����ش�Ŷ��**��֪������զ���**��
        // texelSize ��Ҫ�治ͬ��filter������ı䣬�����Խ��texel sizeԽ��.
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)shadowSettings.directional.filter + 1f);
        // ��ÿһ���cullingSphere�����
        cullingSphere.w -= filterSize;     // ��������ڼ����Ե����Ӱ��ʱ����ܻ�������޳���֮�⣬����Ӧ����Ӧ�ļ�С�޳���ķ�Χ, ����ľ�ֱ��Ϊ0��.
        cullingSphere.w *= cullingSphere.w;
        cascadeCullingSpheres[cascadeLayer] = cullingSphere;
        cascadeDatas[cascadeLayer] = new Vector4(
            1f / cullingSphere.w,         // ���Ǹ�������޳���˥���õģ�Ҳ���Բ���.
            filterSize * 1.4142136f
            );
    }

    void SetViewport(int index, int split, int tileSize)
    {
        /// index: �ֿ����Ӱͼ�ж�ӦС���������Ҳ����Ӱͼ�任�����е�����
        /// �� wholeSize * wholeSize ��ͼƬ��������Ϊsplit�ݣ� �õ�ÿ��С��ķֱ�����tileSize^2, �����Ҵ��µ��ϴ��㿪ʼ��ţ������ӿ�Ϊ���Ϊ i ��С����.
        int x = index % split, y = index / split;
        cmdbuf.SetViewport(new Rect(x * tileSize, y * tileSize, tileSize, tileSize));
    }

    public void ReleaseRT()
    {
        // �ͷ��������ʱ��ȾĿ��.
        cmdbuf.ReleaseTemporaryRT(directionalShadowAtlasID);
        ExecuteBuffer();
    }

    Matrix4x4 ConvertClipToAtlasTextureUV(Matrix4x4 m, int index, int split)
    {
        /// proj*view �任��clip�ռ��У���Χ��-1��1�������ͼ��texture��uv������0,1��������Atlas������. ��Ҫ�޸��������������ֱ�ӵõ�uv.  
        Matrix4x4 ret = m;
        if (SystemInfo.usesReversedZBuffer)
        {
            // ��Щ��Ȼ�������洢���Ǹ���������Ӧ�ð��γ�z�������һ��(���±�2)ȫ��ȡ��.
            ret.m20 = -ret.m20;     ret.m21 = -ret.m21;
            ret.m22 = -ret.m22;     ret.m23 = -ret.m23;
        }
        /// ������Ҫ��-1��1�任��0-1������2+0.5. ���������һ������, �Խ��ߺ����һ��ȫ��0.5��Ҳ���Բ�һ����д.
        /// �任����֮�󣬻���Ҫ����Scale���Atlas�߶���һ��ͼ�Ĵ�С��Ȼ���ƶ�offset*Scale����Ӧλ��.
        /// ƽ�й����ͼ������ͶӰû���⣬�����͸��ͶӰ��Clip�ռ�w��һ����1������ֱ�ӳ��Ծ��󣬻���Ҫһ����д...?
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
