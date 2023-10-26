using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering / Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    // 给整个渲染管线设置阴影.
    [SerializeField]
    ShadowSettings shadowSettings = default;

    [SerializeField]
    PostFXSettings postFXSettings = default;
    protected override RenderPipeline CreatePipeline()
    {
        //shadowSettings.maxDistance = 10;
        return new CustomRenderPipeline(shadowSettings, postFXSettings);
    }
}