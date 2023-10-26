// ����Ԥ�������unity�ṩ�ĸ��ֱ�����һЩ����������任��������Щ����ֱ������RP Core�⣬���Ժ�URPһ�����ȵ�.
// ���ң��������ֺ�Unity�ṩ�� ���ֺ��UnityInput����Ĳ�ͬ��(SpaceTransforms����Щ�����õĶ��Ǻ������)���µ�����.

#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

// Core RP �ṩ��Common
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

// ����궨�����Ƶ�ת��
#include "UnityInput.hlsl"

// ������SpaceTransforms.hlsl�г��ֵ�Unity��������Ƹ�Ϊ UnityInput �г��ֵ���Щ.
#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"   // Core RP�Դ�������任

// һЩ�ͼ�����ص�.
float DistanceSquared(float3 posA, float3 posB)
{
    return dot(posA - posB, posA - posB);
}

#endif