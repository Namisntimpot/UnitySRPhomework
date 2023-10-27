Shader "Custom/Standard Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (0.5,0.5,0.5,1)

            // �����ȵȣ������ʵ��Ӧ�ò�����ȫ�Ľ�����������.
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Fresnel("Fresnel", Range(0, 1)) = 1  // ��������

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)]_Cull("Cull", Float) = 2.0
        [Enum(On, 1, Off, 0)] _ZWrite("Z Write", Float) = 1

            // Alpha Clipping
            _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
            [Toggle(_ALPHACLIPPING)] _Clip("Alpha Clipping", Float) = 0.0

        // Emission
        [NoScaleOffset] _EmissionMap("Emision map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,0)

    }
        SubShader
        {
            Tags { "RenderType" = "Opaque"}
            LOD 200

            Pass{
                Name "Shadow Caster"
                Tags {"LightMode" = "ShadowCaster"}
                ColorMask 0
                
                HLSLPROGRAM
                #pragma target 3.5
                #pragma shader_feature _ALPHACLIPPING
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
                Name "Custom Lit"
                Tags {"LightMode" = "CustomLit"}
                
                Blend[_SrcBlend][_DstBlend]
                Cull[_Cull]
                ZWrite[_ZWrite]

                HLSLPROGRAM
                // #pragma multi_compile_instancing
                #pragma target 3.5
                #pragma shader_feature _ALPHACLIPPING
                #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7  // ���ݹؼ�����ѡ��ͬ�ġ��汾����_ for no keyword..?
                #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER              // �л������ȷ���
                #pragma multi_compile _ LIGHTMAP_ON    // ���������ȫ�ֹ��գ����������.
                #pragma vertex vert
                #pragma fragment frag
                #include "./StandardLitPass.hlsl"
                ENDHLSL
            }

        }

        CustomEditor "GIShaderGUI"
}
