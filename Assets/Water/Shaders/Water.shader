Shader "Water/Surface"
{
    Properties
    {
        _BumpScale("Detail Wave Amount", Range(0, 2)) = 0.2
        _DitherPattern ("Dithering Pattern", 2D) = "bump" {}
        [Toggle(_STATIC_SHADER)] _Static ("Static", Float) = 0
        [KeywordEnum(Off, SSS, Refraction, Reflection, Normal, Fresnel, Foam, WaterDepth)] _Debug ("Debug mode", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" "RenderPipeline" = "UniversalPipeline" }
        ZWrite On

        Pass
        {
            Name "WaterShading"
            Tags{"LightMode" = "UniversalForward"}

            HLSLPROGRAM
            #pragma target 3.5
            #pragma target 4.5 USE_STRUCTURED_BUFFER

            #pragma shader_feature _REFLECTION_CUBEMAP _REFLECTION_PROBES _REFLECTION_PLANARREFLECTION
            #pragma multi_compile _ USE_STRUCTURED_BUFFER
            #pragma multi_compile _ _STATIC_SHADER
            #pragma shader_feature _DEBUG_OFF _DEBUG_SSS _DEBUG_REFRACTION _DEBUG_REFLECTION _DEBUG_NORMAL _DEBUG_FRESNEL _DEBUG_FOAM _DEBUG_WATERDEPTH

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "WaterCommon.hlsl"

            #pragma vertex WaterVertex
            #pragma fragment WaterFragment

            ENDHLSL
        }
    }
    FallBack "Hidden/InternalErrorShader"
}
