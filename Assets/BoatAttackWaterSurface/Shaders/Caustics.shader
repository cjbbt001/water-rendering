Shader "Hidden/BoatAttackSurface/Caustics"
{
    Properties
    {
        //Vector1_F3303B3C("Speed", Float) = 0.5
        _Size("Size", Float) = 0.5
        [NoScaleOffset]_CausticMap("Caustics", 2D) = "white" {}
        _WaterLevel("WaterLevel", Float) = 0
        _BlendDistance("BlendDistance", Float) = 3
        //Vector1_CD857B77("CausticsRGB Split", Float) = 2

        //Color blends
        [HideInInspector] _SrcBlend("__src", Float) = 2.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
    }
    SubShader
    {
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Blend [_SrcBlend] [_DstBlend], One Zero

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #pragma multi_compile _ _DEBUG
            #pragma multi_compile _ _STATIC_SHADER

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 screenpos : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_CausticMap); SAMPLER(sampler_CausticMap);
            TEXTURE2D(_AbsorptionScatteringRamp); SAMPLER(sampler_AbsorptionScatteringRamp);

            half _Size;
            half _WaterLevel;
            half _MaxDepth;
            half _BlendDistance;
            half4x4 _MainLightDir;

            // Can be done per-vertex
            float2 CausticUVs(float2 rawUV, float2 offset)
            {
                float2 uv = rawUV * _Size;
                return uv + offset * 0.1;
            }

            Varyings vert (Attributes input)
            {
                Varyings output;
                
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.screenpos = float4(GetFullScreenTriangleTexCoord(input.vertexID), 0, 1);
                return output;
            }
            
            real4 frag (Varyings input) : SV_Target
            {
                float4 screenPos = input.screenpos / input.screenpos.w;
                
                // Get depth
                real depth = SampleSceneDepth(screenPos.xy);
                
                // Get main light
                Light MainLight = GetMainLight();
                
                // Reconstruct Position of objects in depth map
                float4 WorldPos = ComputeWorldSpacePosition(
                    screenPos.xy, depth, UNITY_MATRIX_I_VP).xyzz;
                
                // Get light direction and use it to rotate the world position
                float3 LightUVs = mul(WorldPos, _MainLightDir).xyz;

#if defined(_STATIC_SHADER)
	            float time = 0;
#else
	            float time = _Time.x;
#endif

                // Read wave texture for noise to offset cautics UVs
                float2 uv = WorldPos.xz * 0.025 + time * 0.25;
                float waveOffset = SAMPLE_TEXTURE2D(_CausticMap, sampler_CausticMap, uv).w - 0.5;

                float2 causticUV = CausticUVs(LightUVs.xy, waveOffset);

                float LodLevel = abs(WorldPos.y - _WaterLevel) * 4 / _BlendDistance;
                float4 A = SAMPLE_TEXTURE2D_LOD(_CausticMap, sampler_CausticMap, causticUV + time, LodLevel);
                float4 B = SAMPLE_TEXTURE2D_LOD(_CausticMap, sampler_CausticMap, causticUV * 2.0, LodLevel);
                
                float CausticsDriver = (A.z * B.z) * 10 + A.z + B.z;
                
                // Mask caustics from above water and fade below
                half upperMask = saturate(-WorldPos.y + _WaterLevel);
                half lowerMask = saturate((WorldPos.y - _WaterLevel) / _BlendDistance + _BlendDistance);
                CausticsDriver *= min(upperMask, lowerMask);
                
                // Fake light dispersion
                half3 Caustics = CausticsDriver * half3(A.w * 0.5, B.w * 0.75, B.x) * MainLight.color;
                
#ifdef _DEBUG
                return real4(Caustics, 1.0);
#endif
                // Add 1 for blending level to work nicely
                return real4(Caustics + 1.0, 1.0);
            }
            ENDHLSL
        }
    }
}
