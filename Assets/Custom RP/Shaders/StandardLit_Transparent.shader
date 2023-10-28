Shader "Custom/Standard Lit Transparent"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.5,0.5,0.5,1)

        [Toggle(_Normal_Map)] _NormalMapToggle("Using Normal Map", Float) = 0
        [NoScaleOffset] _NormalMap("Normal",2D) = "bump"{}
        _NormalScale("Normal Scale", Range(0, 1)) = 1

            // ֻ��Ϊ����lightmap������ת.
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
        [HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)

            // �����ȵȣ������ʵ��Ӧ�ò�����ȫ�Ľ�����������.
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        [Toggle(_Metallic_Map)] _MetallicMapToggle("Using Metallic Map", Float) = 0
        [NoScaleOffset] _MetallicMap("Metallic Map", 2D) = "white" {}

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Fresnel("Fresnel", Range(0, 1)) = 1  // ��������

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2.0
        [Enum(On, 1, Off, 0)] _ZWrite("Z Write", Float) = 0

        // Alpha Clipping
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_ALPHACLIPPING)] _Clip("Alpha Clipping", Float) = 0.0

        // ������Ӱģʽ.
        /// ���͸���ȼ��㷽����alpha cutoff(����ѡ�������), ����Ӱһ���� clip
        /// ���͸���ȼ��㷽����alpha blend�������ѡ��dither��clip������clip����. ���ͬʱѡ��clip��dither���ͻ���clip.
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
                ColorMask 0    // ���ε���ɫ

                HLSLPROGRAM
                #pragma target 3.5
                #pragma shader_feature _ALPHACLIPPING
                #pragma shader_feature _SHADOW_CLIP
                #pragma shader_feature _SHADOW_DITHER
                #pragma shader_feature _Normal_Map
                #pragma shader_feature _Metallic_Map
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
                #pragma shader_feature _Normal_Map
                #pragma shader_feature _Metallic_Map
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
                #pragma shader_feature _Normal_Map
                #pragma shader_feature _Metallic_Map
                #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7  // ���ݹؼ�����ѡ��ͬ�ġ��汾����_ for no keyword..?
                #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER              // �л������ȷ���         
                #pragma vertex vert
                #pragma fragment frag
                #include "./StandardLitPass.hlsl"
                ENDHLSL
            }
        }
    CustomEditor "GIShaderGUI"
}
