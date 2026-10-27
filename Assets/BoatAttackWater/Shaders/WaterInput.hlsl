#ifndef WATER_INPUT_INCLUDE
#define WATER_INPUT_INCLUDE

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
half _BumpScale;
half4 _DitherPattern_TexelSize;
CBUFFER_END
half _MaxDepth;
half _MaxWaveHeight;
int _DebugPass;
half4 _Water_DepthCamParams; // 水面深度图相机参数, x = camera height, y = half edge
float4x4 _InvViewProjection;

// Screen Effects textures
SAMPLER(sampler_ScreenTextures_linear_clamp); // 给屏幕相关的纹理使用的采样器

#if defined(_REFLECTION_PLANARREFLECTION)
TEXTURE2D(_PlanarReflectionTexture);
#elif defined(_REFLECTION_CUBEMAP)
TEXTURECUBE(_CubemapTexture);
SAMPLER(sampler_CubemapTexture);
#endif

TEXTURE2D(_WaterFXMap);
TEXTURE2D(_CameraDepthTexture);
TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture_linear_clamp);

TEXTURE2D(_WaterDepthMap); SAMPLER(sampler_WaterDepthMap_linear_clamp);

// Surface textures
TEXTURE2D(_AbsorptionScatteringRamp); SAMPLER(sampler_AbsorptionScatteringRamp);
TEXTURE2D(_SurfaceMap); SAMPLER(sampler_SurfaceMap);
TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);
TEXTURE2D(_DitherPattern); SAMPLER(sampler_DitherPattern);

#endif