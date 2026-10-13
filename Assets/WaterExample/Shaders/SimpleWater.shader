// Simplified URP implementation of https://zhuanlan.zhihu.com/p/2081124056691105936
Shader "WaterExample/Simple Water"
{
    Properties
    {
        [Header(Depth and Color)]
        _ShallowColor ("Shallow Color", Color) = (0.12, 0.72, 0.65, 1)
        _DeepColor ("Deep Color", Color) = (0.015, 0.15, 0.24, 1)
        _DepthRange ("Depth Color Range (meters)", Range(0.1, 12)) = 4
        _Absorption ("Water Absorption", Range(0, 3)) = 0.45

        [Header(Normal and Refraction)]
        [Normal] _NormalMap ("Wave Normal", 2D) = "bump" {}
        _NormalTiling ("Normal Tiling (world space)", Range(0.02, 2)) = 0.28
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.5
        _FlowA ("First Layer Speed (XY)", Vector) = (0.035, 0.015, 0, 0)
        _FlowB ("Second Layer Speed (XY)", Vector) = (-0.02, 0.03, 0, 0)
        _Distortion ("Refraction Distortion", Range(0, 0.1)) = 0.025

        [Header(Caustics)]
        _CausticsMap ("Caustics", 2D) = "black" {}
        _CausticsTiling ("Caustics Tiling", Range(0.05, 3)) = 0.32
        _CausticsIntensity ("Caustics Intensity", Range(0, 3)) = 1.1

        [Header(Reflection and Specular)]
        _ReflectionTex ("Sky Reflection", Cube) = "" {}
        _ReflectionStrength ("Reflection Strength", Range(0, 2)) = 0.75
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 3
        _SpecularColor ("Specular Color", Color) = (1, 0.96, 0.82, 1)
        _SpecularIntensity ("Specular Intensity", Range(0, 5)) = 1.4
        _Shininess ("Specular Exponent", Range(8, 512)) = 180

        [Header(Shore Foam)]
        _FoamMap ("Foam Noise", 2D) = "white" {}
        _FoamColor ("Foam Color", Color) = (0.92, 1, 0.96, 1)
        _FoamWidth ("Foam Width (eye depth meters)", Range(0.01, 2)) = 0.6
        _FoamTiling ("Foam Tiling", Range(0.1, 5)) = 1.2
        _FoamContrast ("Foam Contrast", Range(0.2, 4)) = 1.3
        _EdgeFade ("Contact Fade (meters)", Range(0.001, 0.5)) = 0.07
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }
            // The opaque scene is already composited below. Only alpha-blend the contact edge.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_CausticsMap); SAMPLER(sampler_CausticsMap);
            TEXTURE2D(_FoamMap); SAMPLER(sampler_FoamMap);
            TEXTURECUBE(_ReflectionTex); SAMPLER(sampler_ReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor, _FlowA, _FlowB, _SpecularColor, _FoamColor;
                float _DepthRange, _Absorption, _NormalTiling, _NormalStrength, _Distortion;
                float _CausticsTiling, _CausticsIntensity, _ReflectionStrength, _FresnelPower;
                float _SpecularIntensity, _Shininess, _FoamWidth, _FoamTiling, _FoamContrast, _EdgeFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = n.normalWS;
                output.tangentWS = half4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.fogFactor = ComputeFogFactor(p.positionCS.z);
                return output;
            }
           
            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceDepth = -TransformWorldToView(input.positionWS).z;

                // 1. 水深和水色
                float rawDepth = SampleSceneDepth(screenUV);
                float waterDepth = max(0.0, LinearEyeDepth(rawDepth, _ZBufferParams) - surfaceDepth);
                half depthMask = saturate(waterDepth / max(_DepthRange, 0.001));
                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthMask);

                // 2. 法线波纹
                float2 waveUV = input.positionWS.xz * _NormalTiling;
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                    waveUV + _Time.y * _FlowA.xy), _NormalStrength);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                    waveUV * 1.37 + _Time.y * _FlowB.xy + 0.31), _NormalStrength);
                half3 normalTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                half3 geometricNormal = normalize(input.normalWS);
                half3 tangent = normalize(input.tangentWS.xyz);
                half3 bitangent = cross(geometricNormal, tangent) * input.tangentWS.w;
                half3 N = normalize(tangent * normalTS.x + bitangent * normalTS.y + geometricNormal * normalTS.z);
                half3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // 3. 法线偏移UV做折射
                float2 offset = TransformWorldToViewDir(N, false).xy * _Distortion;
                offset *= saturate(waterDepth / 0.5);
                float2 refractedUV = saturate(screenUV + offset);
                float refractedRawDepth = SampleSceneDepth(refractedUV);
                if (LinearEyeDepth(refractedRawDepth, _ZBufferParams) <= surfaceDepth)
                {
                    refractedUV = screenUV;
                    refractedRawDepth = rawDepth;
                }
                half3 underwater = SampleSceneColor(refractedUV);

                // 4. 焦散Caustic、水面水底混合
                float3 bottomWS = ComputeWorldSpacePosition(refractedUV, refractedRawDepth, UNITY_MATRIX_I_VP);
                float2 causticUV = bottomWS.xz * _CausticsTiling + bottomWS.y * 0.12;
                half ca = SAMPLE_TEXTURE2D(_CausticsMap, sampler_CausticsMap, causticUV + _Time.y * float2(0.025, 0.018)).r;
                half cb = SAMPLE_TEXTURE2D(_CausticsMap, sampler_CausticsMap, causticUV * 1.13 + _Time.y * float2(-0.023, 0.029)).r;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half caustics = min(ca, cb) * _CausticsIntensity * exp(-waterDepth * 0.35);
                caustics *= saturate(waterDepth * 3.0) * mainLight.shadowAttenuation;
                underwater += caustics * mainLight.color * saturate(mainLight.direction.y);
                half transmission = exp(-waterDepth * _Absorption);
                half3 color = lerp(waterColor, underwater * lerp(half3(1, 1, 1), _ShallowColor.rgb, 0.3), transmission);

                // 5. 反射 reflection、高光
                half fresnel = 0.02 + 0.98 * pow(saturate(1.0 - saturate(dot(N, V))), _FresnelPower);
                half3 reflection = SAMPLE_TEXTURECUBE(_ReflectionTex, sampler_ReflectionTex, reflect(-V, N)).rgb;
                color = lerp(color, reflection, saturate(fresnel * _ReflectionStrength));
                half3 H = SafeNormalize(mainLight.direction + V);
                half specular = pow(saturate(dot(N, H)), _Shininess) * _SpecularIntensity;
                color += specular * _SpecularColor.rgb * mainLight.color * mainLight.shadowAttenuation;

                // 6. Foam
                float2 foamUV = input.positionWS.xz * _FoamTiling + _Time.y * float2(0.04, -0.025);
                half noise = pow(abs(SAMPLE_TEXTURE2D(_FoamMap, sampler_FoamMap, foamUV).r), _FoamContrast);
                half threshold = waterDepth / max(_FoamWidth, 0.001);
                half aa = max(fwidth(noise - threshold), 0.015);
                half foam = smoothstep(threshold - aa, threshold + aa, noise) * _FoamColor.a;
                color = lerp(color, _FoamColor.rgb, foam);
                color = MixFog(color, input.fogFactor);
                return half4(color, saturate(waterDepth / max(_EdgeFade, 0.001))); // 边缘淡出
            }
            ENDHLSL
        }
    }
    Fallback Off
}
