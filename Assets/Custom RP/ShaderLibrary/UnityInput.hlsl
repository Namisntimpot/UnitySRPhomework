// 定义来自Unity的变量.

#ifndef CUSTOM_UNITYINPUT_INCLUDED
#define CUSTOM_UNITYINPUT_INCLUDED

// 变量定义和 URP 保持一致.

CBUFFER_START(UnityPerDraw) // 每次绘制不同.
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
real4 unity_WorldTransformParams;
float4 unity_LODFade;

// 全局光照相关的.
float4 unity_LightmapST;
float4 unity_DynamicLightmapST;  // 弃用.但为了SRP batch的兼容性加进来.

// probe相关的（球谐系数---大概）
float4 unity_SHAr;
float4 unity_SHAg;
float4 unity_SHAb;
float4 unity_SHBr;
float4 unity_SHBg;
float4 unity_SHBb;
float4 unity_SHC;

// LightProbeVolume相关的.
float4 unity_ProbeVolumeParams;
float4x4 unity_ProbeVolumeWorldToObject;
float4 unity_ProbeVolumeSizeInv;
float4 unity_ProbeVolumeMin;

// 后处理中，用于指示纹理是否需要flip
float4 _ProjectionParams;

float4 unity_SpecCube0_HDR;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;

#endif