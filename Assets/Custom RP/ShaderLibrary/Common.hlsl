// 包含预定义的由unity提供的各种变量，一些基本的坐标变换函数，这些函数直接来自RP Core库，所以和URP一样，等等.
// 并且，在这里弥合Unity提供的 各种宏和UnityInput定义的不同名(SpaceTransforms下这些矩阵用的都是宏的名称)导致的问题.

#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

// Core RP 提供的Common
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

// 矩阵宏定义名称的转换
#include "UnityInput.hlsl"

// 把下面SpaceTransforms.hlsl中出现的Unity矩阵宏名称改为 UnityInput 中出现的那些.
#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"   // Core RP自带的坐标变换

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

// 一些空间变换相关的
/// 切线空间TS到世界空间WS
float3 NormalTangentToWorld(float3 normalTS, float3 normalWS, float4 tangentWS)
{
    // normalTS: 切线空间的新法线; normalWS: 世界空间的旧法线；tangentWS: 世界空间的旧切线.
    float3x3 TS2WS = CreateTangentToWorld(normalWS, tangentWS.xyz, tangentWS.w);
    return TransformTangentToWorld(normalTS, TS2WS);
}

// 一些和计算相关的.
float DistanceSquared(float3 posA, float3 posB)
{
    return dot(posA - posB, posA - posB);
}

// 贴图解包
/// 法线
float3 DecodeNormal(float4 sample, float scale)
{
#if defined(UNITY_NO_DXT5nm)
	return normalize(UnpackNormalRGB(sample, scale));
#else
    return normalize(UnpackNormalmapRGorAG(sample, scale));
#endif
}

#endif