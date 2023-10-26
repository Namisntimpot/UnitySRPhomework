Shader "Custom/Standard Lit Transparent"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.5,0.5,0.5,1)

            // 只是为了让lightmap正常运转.
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
        [HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)

            // 金属度等（这里的实现应该不是完全的金属工作流）.
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2.0
        [Enum(On, 1, Off, 0)] _ZWrite("Z Write", Float) = 0

        // Alpha Clipping
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_ALPHACLIPPING)] _Clip("Alpha Clipping", Float) = 0.0

        // 关于阴影模式.
        /// 如果透明度计算方法是alpha cutoff(即勾选了上面的), 则阴影一定是 clip
        /// 如果透明度计算方法是alpha blend，则可以选择dither或clip。其中clip优先. 如果同时选了clip和dither，就会用clip.
        [Toggle(_SHADOW_CLIP)] _ShadowClip("Shadow Mode: Clip", Float) = 1.0
        [Toggle(_SHADOW_DITHER)] _ShadowDither("Shadow Mode: Dither", Float) = 0.0

    }
        SubShader
        {
            Tags { "RenderType" = "Transparent"}
            LOD 200

            Pass{
                Name "Shadow Caster"
                Tags {"LightMode" = "ShadowCaster"}
                ColorMask 0    // 屏蔽掉颜色

                HLSLPROGRAM
                #pragma target 3.5
                #pragma shader_feature _ALPHACLIPPING
                #pragma shader_feature _SHADOW_CLIP
                #pragma shader_feature _SHADOW_DITHER
                #pragma vertex vert
                #pragma fragment frag
                #include "./ShadowCasterPass.hlsl"
                ENDHLSL
            }

            Pass{
                Name "Meta Pass"
                Tags {"LightMode" = "Meta"}
                Cull Off

                HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex MetaPassVertex
                #pragma fragment MetaPassFragment
                #include "MetaPass.hlsl"
                ENDHLSL
            }

            Pass{
                Name "Custom Lit Transparent"
                Tags {"LightMode" = "CustomLit"}

                Blend[_SrcBlend][_DstBlend]
                Cull[_Cull]
                ZWrite[_ZWrite]

                HLSLPROGRAM
                // #pragma multi_compile_instancing
                #pragma target 3.5
                #pragma shader_feature _ALPHACLIPPING
                #pragma shader_feature _SHADOW_CLIP
                #pragma shader_feature _SHADOW_DITHER
                #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7  // 根据关键字来选择不同的“版本”，_ for no keyword..?
                #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER              // 切换层间过度方法         
                #pragma vertex vert
                #pragma fragment frag
                #include "./StandardLitPass.hlsl"
                ENDHLSL
            }
        }
    CustomEditor "GIShaderGUI"
}
