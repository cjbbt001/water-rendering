#ifndef WATER_COMMON_INCLUDED
#define WATER_COMMON_INCLUDED

#define SHADOWS_SCREEN 0

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "WaterInput.hlsl"
#include "CommonUtilities.hlsl"
#include "GerstnerWaves.hlsl"
#include "WaterLighting.hlsl"

struct WaterVertexInput
{
    float4 vertex : POSITION;
    float2 texcoord : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct WaterVertexOutput
{
    float4 uv : TEXCOORD0; // Geometric UVs stored in xy, and world(pre-waves) in zw
    float3 posWS : TEXCOORD1;
    half3 normal : NORMAL;
    float3 viewDir : TEXCOORD2;
    float3 preWaveSP : TEXCOORD3; // screen position of the verticies before wave distortion
    half2 fogFactorNoise : TEXCOORD4; // x: fogFactor, y: noise
    float4 additionalData : TEXCOORD5; // x = distance to opaque surface, y = distance to watersurface, z = normalized wave height, w = horizontal movement
    half4 shadowCoord : TEXCOORD6; // for ssshadows

    float4 clipPos : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

// Debug WaterFX
half3 DebugWaterFX(half3 input, half4 waterFX, half screenUV)
{
    input = lerp(input, half3(waterFX.y, 1, waterFX.z), saturate(floor(screenUV + 0.7)));
    input = lerp(input, waterFX.xxx, saturate(floor(screenUV + 0.5)));
    half3 disp = lerp(0, half3(1, 0, 0), saturate((waterFX.www - 0.5) * 4));
    disp += lerp(0, half3(0, 0, 1), saturate(((1 - waterFX.www) - 0.5) * 4));
    input = lerp(input, disp, saturate(floor(screenUV + 0.3)));
    return input;
}

// Functions //
    
// 散射
half3 Scattering(half depth) 
{
    return SAMPLE_TEXTURE2D(_AbsorptionScatteringRamp, sampler_AbsorptionScatteringRamp, half2(depth, 0.375)).rgb;
}

// 吸收
half3 Absorption(half depth) 
{
    return SAMPLE_TEXTURE2D(_AbsorptionScatteringRamp, sampler_AbsorptionScatteringRamp, half2(depth, 0.0h)).rgb;
}

// 沿视线的水体深度
float AdjustedDepth(half2 uvs, half4 additionalData) 
{
    float rawD = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_ScreenTextures_linear_clamp, uvs);
    float d = LinearEyeDepth(rawD, _ZBufferParams);

    return d * additionalData.x - additionalData.y; // d * addtionalData.x = 相机到水底距离
}

// 水面深度图恢复深度
float WaterTextureDepth(float3 posWS)
{
    float d = SAMPLE_TEXTURE2D_LOD(
    _WaterDepthMap, sampler_WaterDepthMap_linear_clamp,
    posWS.xz * 0.002 + 0.5, 0).r;

#if UNITY_REVERSED_Z
    d = 1 - d;
#endif

    return lerp(0.01, _MaxDepth + 4, d) - _Water_DepthCamParams.x + posWS.y;
}
    
// 深度数据储存,
float2 WaterDepth(float3 posWS, half4 additionalData, half2 screenUVs)
{
    float2 outDepth = 0;
    outDepth.x = AdjustedDepth(screenUVs, additionalData); // 沿视线的水体深度
    outDepth.y = WaterTextureDepth(posWS); // 竖直水面深度
    return outDepth;
}
    
// Refraction
half3 Refraction(half2 distortionUV, half depth, half depthMulti)
{
    half3 output = SAMPLE_TEXTURE2D_LOD(
    _CameraOpaqueTexture, sampler_CameraOpaqueTexture_linear_clamp, distortionUV, depth * 0.25).rgb;
    output *= Absorption(depth * depthMulti);
    return output;
}
    
// DistortionUV
half2 DistortionUVs(half depth, float3 normalWS)
{
    half3 viewNormal = mul((float3x3) GetWorldToHClipMatrix(), -normalWS).xyz;

    return viewNormal.xz * saturate((depth) * 0.005);
}
    
// AddtionalData
half4 AdditionalData(float3 posWS, WaveStruct wave)
{
    half4 data = half4(0.0, 0.0, 0.0, 0.0);
    float3 viewPos = TransformWorldToView(posWS);
    data.x = length(viewPos / viewPos.z); // 1/cosα 矫正视线方向偏移的距离
    data.y = length(GetCameraPositionWS().xyz - posWS); // 相机到水面距离
    data.z = wave.position.y / _MaxWaveHeight * 0.5 + 0.5; // 映射波高
    data.w = wave.position.x + wave.position.z; // 水平位移
    return data;
}
    
// 顶点处理
WaterVertexOutput WaveVertexOperations(WaterVertexOutput input)
{
#ifdef _STATIC_SHADER
float time = 0;
#else
    float time = _Time.y;
#endif

    input.normal = float3(0, 1, 0);
    input.fogFactorNoise.y = (noise((input.posWS.xz * 0.5) + time) + noise((input.posWS.xz * 1) + time)) * 0.25 + 0.5;

    // Detail UVs
    input.uv.zw = input.posWS.xz * 0.1h + time.xx * 0.05h + (input.fogFactorNoise.y * 0.1);
    input.uv.xy = input.posWS.xz * 0.4h - time.xx * 0.1h + (input.fogFactorNoise.y * 0.2);

    half4 screenUV = ComputeScreenPos(TransformWorldToHClip(input.posWS));
    screenUV.xyz /= screenUV.w;

    // shallows mask
    half waterDepth = WaterTextureDepth(input.posWS);
    input.posWS.y += pow(saturate((-waterDepth + 1.5) * 0.4), 2); // 浅水抬高

    //Gerstner
    WaveStruct wave;
    SampleWaves(input.posWS, saturate((waterDepth * 0.1 + 0.05)), wave);
    input.normal = wave.normal;
    input.posWS += wave.position;

    // Dynamic displacement
    //half4 waterFX = SAMPLE_TEXTURE2D_LOD(_WaterFXMap, sampler_ScreenTextures_linear_clamp, screenUV.xy, 0);
    //input.posWS.y += waterFX.w * 2 - 1;

    // After waves
    input.clipPos = TransformWorldToHClip(input.posWS);
    input.shadowCoord = ComputeScreenPos(input.clipPos); // 屏幕空间坐标
    input.viewDir = SafeNormalize(_WorldSpaceCameraPos - input.posWS);

    // Fog
    input.fogFactorNoise.x = ComputeFogFactor(input.clipPos.z);
    input.preWaveSP = screenUV.xyz; // pre-displaced screenUVs

    // Additional data
    input.additionalData = AdditionalData(input.posWS, wave);

    // distance blend
    half distanceBlend = saturate(abs(length((_WorldSpaceCameraPos.xz - input.posWS.xz) * 0.005)) - 0.25);
    input.normal = lerp(input.normal, half3(0, 1, 0), distanceBlend);

    return input;
}
    
    
// Vertex and Fragment //
    
// Vertex: Used for Standard non-tessellated water
WaterVertexOutput WaterVertex(WaterVertexInput v)
{
    WaterVertexOutput o; 
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.uv.xy = v.texcoord; // geo uvs
    o.posWS = TransformObjectToWorld(v.vertex.xyz);

    o = WaveVertexOperations(o);
    return o;
}

// Fragment for water
half4 WaterFragment(WaterVertexOutput IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);
    half3 screenUV = IN.shadowCoord.xyz / IN.shadowCoord.w; //screen UVs

    //half4 waterFX = SAMPLE_TEXTURE2D(_WaterFXMap, sampler_ScreenTextures_linear_clamp, IN.preWaveSP.xy);

    // Depth
    float2 depth = WaterDepth(IN.posWS, IN.additionalData, screenUV.xy); // TODO - hardcoded shore depth UVs
    //return half4(0, frac(ceil(depth.y) / _MaxDepth), frac(IN.posWS.y), 1);
    half depthMulti = 1 / _MaxDepth;

    // Detail waves
    half2 detailBump1 = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, IN.uv.zw).xy * 2 - 1;
    half2 detailBump2 = SAMPLE_TEXTURE2D(_SurfaceMap, sampler_SurfaceMap, IN.uv.xy).xy * 2 - 1;
    half2 detailBump = (detailBump1 + detailBump2 * 0.5) * saturate(depth.x * 0.25 + 0.25);

    IN.normal += half3(detailBump.x, 0, detailBump.y) * _BumpScale;
    //IN.normal += half3(1 - waterFX.y, 0.5h, 1 - waterFX.z) - 0.5;
    IN.normal = normalize(IN.normal);

    // Distortion
    half2 distortion = DistortionUVs(depth.x, IN.normal);
    distortion = screenUV.xy + distortion; // * clamp(depth.x, 0, 5);
    float d = depth.x;
    depth.x = AdjustedDepth(distortion, IN.additionalData);
    distortion = depth.x < 0 ? screenUV.xy : distortion; // 去除地面扰动
    depth.x = depth.x < 0 ? d : depth.x;

    // Fresnel
    half fresnelTerm = CalculateFresnelTerm(IN.normal, IN.viewDir.xyz);
    //return fresnelTerm.xxxx;

    // Lighting
    Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.posWS));
    half shadow = SoftShadows(screenUV, IN.posWS, IN.viewDir.xyz, depth.x);
    half3 GI = SampleSH(IN.normal);

    // SSS
    half3 directLighting = dot(mainLight.direction, half3(0, 1, 0)) * mainLight.color;
    directLighting += saturate(pow(dot(IN.viewDir, -mainLight.direction) 
                        * IN.additionalData.z, 3)) * 5 * mainLight.color; // 波峰透亮
    half3 sss = directLighting * shadow + GI;
    sss *= Scattering(depth.x * depthMulti);
        
    // Foam
    half3 foamMap = SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, IN.uv.zw).rgb; //r=thick, g=medium, b=light
        
    half waveFoam = saturate(IN.additionalData.z - 0.75 * 0.5); // 波峰 wave tips foam
        
    half depthEdge = saturate(depth.x * 20); // 边缘泡沫衰减
    half depthAdd = saturate(1 - depth.x * 4) * 0.5; // 水深小于 0.25 加强泡沫
    half edgeFoam = saturate((1 - min(depth.x, depth.y) * 0.5 - 0.25) + depthAdd) * depthEdge; // 岸边
        
    //half foamBlendMask = max(max(waveFoam, edgeFoam), waterFX.r * 2); // 加入交互foam
    half foamBlendMask = max(waveFoam, edgeFoam);
        
    half3 foamBlend = SAMPLE_TEXTURE2D(_AbsorptionScatteringRamp, sampler_AbsorptionScatteringRamp, half2(foamBlendMask, 0.66)).rgb;
    half foamMask = saturate(length(foamMap * foamBlend) * 1.5 - 0.1); // 三通道乘权重
        
    // Foam lighting
    half3 foam = foamMask.xxx * (mainLight.shadowAttenuation * mainLight.color + GI);
    
    //Specular
    BRDFData brdfData;
    half alpha = 1;
    InitializeBRDFData(half3(0, 0, 0), 0, half3(1, 1, 1), 0.95, alpha, brdfData);
    half3 spec = DirectBDRF(brdfData, IN.normal, mainLight.direction, IN.viewDir, true) * shadow * mainLight.color;
#ifdef _ADDITIONAL_LIGHTS
uint pixelLightCount = GetAdditionalLightsCount();
for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
{
    Light light = GetAdditionalLight(lightIndex, IN.posWS);
    spec += LightingPhysicallyBased(brdfData, light, IN.normal, IN.viewDir);
    sss += light.distanceAttenuation * light.color;
}
#endif

    // Reflections
    half3 reflection = SampleReflections(IN.normal, IN.viewDir.xyz, screenUV.xy, 0.0);

    // Refraction
    half3 refraction = Refraction(distortion, depth.x, depthMulti);

    // Do compositing
    half3 comp = lerp(lerp(refraction, reflection, fresnelTerm) + sss + spec, foam, foamMask); //lerp(refraction, color + reflection + foam, 1-saturate(1-depth.x * 25));

    // Fog
    float fogFactor = IN.fogFactorNoise.x;
    comp = MixFog(comp, fogFactor);
        
#if defined(_DEBUG_FOAM)
return half4(foamMask.xxx, 1);
#elif defined(_DEBUG_SSS)
return half4(sss, 1);
#elif defined(_DEBUG_REFRACTION)
return half4(refraction, 1);
#elif defined(_DEBUG_REFLECTION)
return half4(reflection, 1);
#elif defined(_DEBUG_NORMAL)
return half4(IN.normal.x * 0.5 + 0.5, 0, IN.normal.z * 0.5 + 0.5, 1);
#elif defined(_DEBUG_FRESNEL)
return half4(fresnelTerm.xxx, 1);
#elif defined(_DEBUG_WATEREFFECTS)
return half4(0, 0.5, 0.5, 0.5); // WaterFX 暂未接入，显示中性值
#elif defined(_DEBUG_WATERDEPTH)
return half4(frac(depth), 0, 1);
#else
    return half4(comp, 1);
#endif
}
    
#endif
