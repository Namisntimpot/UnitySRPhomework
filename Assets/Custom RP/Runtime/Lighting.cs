using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    const string bufferName = "Lighting Buffer";
    CommandBuffer cmdbuf = new CommandBuffer() { name = bufferName };

    //-----------------------���ֹ�Դ---------------------------------
    // ֧�ֶ���գ���������������.
    const int maxDirectionalLightCount = 4;
    // shader�й��ձ�����id��������uniform��id�Ƶ�.
    // ƽ�й�
    static int
        dirLightCountID = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsID = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsID = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataID = Shader.PropertyToID("_DirectionalLightShadowDatas");  // ÿ��ƽ�й����Ӱ����Ϣ������shadowStrength, index in ��Ӱ��ص�����
    // �ѹ������ݴ���shader.

    static Vector4[]
        dirLightColors = new Vector4[maxDirectionalLightCount],
        dirLightDirections = new Vector4[maxDirectionalLightCount],
        dirLightShadowDatas = new Vector4[maxDirectionalLightCount];


    //--------------------��Ӱ���------------------------------------
    Shadow shadow = new Shadow();  // ��Ӱ�ڹ����д���.

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        /// �������й�����Ϣ��ע�⣬��ӰͼҲ���ڵ����������֮����Ⱦ���.
        // ֱ��ͨ��cullingResults���Culling֮������пɼ���.
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;  //�ü�֮�󣬵�ǰ��������пɼ���.
        cmdbuf.BeginSample(bufferName);
        // ��ʼ����Ӱ.
        shadow.Setup(ref context, ref cullingResults, ref shadowSettings);
        // ׼��ƽ�й���. �����������������ƽ�й���Ӱ
        SetupDirectionalLight(ref visibleLights);
        // ׼����������...

        // ��Ⱦ��Ӱͼ
        cmdbuf.EndSample(bufferName);
        context.ExecuteCommandBuffer(cmdbuf);
        cmdbuf.Clear();
    }

    public void RenderShadowmap()
    {
        // ��Ⱦ��Ӱͼ.
        shadow.Render();
    }

    public void ClearShadowmap()
    {
        /// �������Ⱦ��Ӱͼ�õ�tempraryRenderTarget.
        shadow.ReleaseRT();
    }

    void SetupDirectionalLight(ref NativeArray<VisibleLight> visibleLights)
    {
        // ֻ����ƽ�й�.
        int count = 0;
        for (int i = 0; count < maxDirectionalLightCount && i < visibleLights.Length; ++i)
        {
            if (visibleLights[i].lightType == LightType.Directional) {
                dirLightColors[count] = visibleLights[i].finalColor;  //�������lightcolor * intensity.
                dirLightDirections[count] = -visibleLights[i].localToWorldMatrix.GetColumn(2);  //�任��������ʣ��䵽��Ȼ���ľ�����Ƿ���Ȼ����������Ȼ���µı�ʾ��ɵ�
                // ��ʼ�����ƽ�й����Ӱ.
                dirLightShadowDatas[count] = shadow.SetupDirectionalShadow(visibleLights[i].light, i);
                count++;
            } 
        }
        //cmdbuf.SetGlobalVector(dirLightColorsID, light.color * light.intensity);
        //cmdbuf.SetGlobalVector(dirLightDirectionsID, -light.transform.forward.normalized);
        cmdbuf.SetGlobalInt(dirLightCountID, count);
        cmdbuf.SetGlobalVectorArray(dirLightColorsID, dirLightColors);
        cmdbuf.SetGlobalVectorArray(dirLightDirectionsID, dirLightDirections);
        cmdbuf.SetGlobalVectorArray(dirLightShadowDataID, dirLightShadowDatas);
    }
}
