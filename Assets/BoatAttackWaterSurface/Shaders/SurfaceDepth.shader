Shader "Hidden/BoatAttackSurface/TerrainDepth"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _SurfaceDepthEncode;
            struct Attributes { float3 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float height : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.height = world.y;
                return o;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float encoded = saturate(1 - (_SurfaceDepthEncode.x - input.height) * _SurfaceDepthEncode.y);
                return encoded.xxxx;
            }
            ENDHLSL
        }
    }
}
