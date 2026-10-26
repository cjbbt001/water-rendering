Shader "BoatAttackWater/Water"
{
    Properties
    {
         _BaseColor("Water Color", Color) = (0.05, 0.4, 0.5, 0.8)
    }

    SubShader
    {
        Tags
        {
           "RenderPipeline" = "UniversalPipeline"
           "RenderType" = "Transparent"
           "Queue" = "Transparent" 
        }

        Pass
        {
            Name "WaterShading"
            Tags {"LightMode" = "UniversalForward"}

            ZWrite On
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex WaterVertex
            #pragma fragment WaterFragment

            #include "WaterCommon.hlsl"

            ENDHLSL
        }
    }
}
