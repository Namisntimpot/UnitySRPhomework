using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    const string bufferName = "Lighting Buffer";
    CommandBuffer cmdbuf = new CommandBuffer() { name = bufferName };

    //-----------------------各种光源---------------------------------
    // 支持多光照，定义最大光照数量.
    const int maxDirectionalLightCount = 4;
    // shader中光照变量的id，就像是uniform的id似的.
    // 平行光
    static int
        dirLightCountID = Shader.PropertyToID("_DirectionalLightCount"),
        dirLightColorsID = Shader.PropertyToID("_DirectionalLightColors"),
        dirLightDirectionsID = Shader.PropertyToID("_DirectionalLightDirections"),
        dirLightShadowDataID = Shader.PropertyToID("_DirectionalLightShadowDatas");  // 每个平行光的阴影的信息，包括shadowStrength, index in 阴影相关的数组
    // 把光照数据传进shader.

    static Vector4[]
        dirLightColors = new Vector4[maxDirectionalLightCount],
        dirLightDirections = new Vector4[maxDirectionalLightCount],
        dirLightShadowDatas = new Vector4[maxDirectionalLightCount];


    //--------------------阴影相关------------------------------------
    Shadow shadow = new Shadow();  // 阴影在光照中处理.

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
    {
        /// 设置所有光照信息，注意，阴影图也会在调用这个函数之后渲染完毕.
        // 直接通过cullingResults获得Culling之后的所有可见光.
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;  //裁剪之后，当前相机的所有可见光.
        cmdbuf.BeginSample(bufferName);
        // 初始化阴影.
        shadow.Setup(ref context, ref cullingResults, ref shadowSettings);
        // 准备平行光照. 并在这个函数中设置平行光阴影
        SetupDirectionalLight(ref visibleLights);
        // 准备其他光照...

        // 渲染阴影图
        cmdbuf.EndSample(bufferName);
        context.ExecuteCommandBuffer(cmdbuf);
        cmdbuf.Clear();
    }

    public void RenderShadowmap()
    {
        // 渲染阴影图.
        shadow.Render();
    }

    public void ClearShadowmap()
    {
        /// 清理掉渲染阴影图用的tempraryRenderTarget.
        shadow.ReleaseRT();
    }

    void SetupDirectionalLight(ref NativeArray<VisibleLight> visibleLights)
    {
        // 只考虑平行光.
        int count = 0;
        for (int i = 0; count < maxDirectionalLightCount && i < visibleLights.Length; ++i)
        {
            if (visibleLights[i].lightType == LightType.Directional) {
                dirLightColors[count] = visibleLights[i].finalColor;  //这个就是lightcolor * intensity.
                dirLightDirections[count] = -visibleLights[i].localToWorldMatrix.GetColumn(2);  //变换矩阵的性质，变到自然基的矩阵就是非自然基向量在自然基下的表示组成的
                // 初始化这个平行光的阴影.
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
