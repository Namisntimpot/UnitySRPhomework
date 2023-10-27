// ��������Unity�ı���.

#ifndef CUSTOM_UNITYINPUT_INCLUDED
#define CUSTOM_UNITYINPUT_INCLUDED

// ��������� URP ����һ��.

CBUFFER_START(UnityPerDraw) // ÿ�λ��Ʋ�ͬ.
float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
real4 unity_WorldTransformParams;
float4 unity_LODFade;

// ȫ�ֹ�����ص�.
float4 unity_LightmapST;
float4 unity_DynamicLightmapST;  // ����.��Ϊ��SRP batch�ļ����Լӽ���.

// probe��صģ���гϵ��---��ţ�
float4 unity_SHAr;
float4 unity_SHAg;
float4 unity_SHAb;
float4 unity_SHBr;
float4 unity_SHBg;
float4 unity_SHBb;
float4 unity_SHC;

// LightProbeVolume��ص�.
float4 unity_ProbeVolumeParams;
float4x4 unity_ProbeVolumeWorldToObject;
float4 unity_ProbeVolumeSizeInv;
float4 unity_ProbeVolumeMin;

// �����У�����ָʾ�����Ƿ���Ҫflip
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