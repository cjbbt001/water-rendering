#ifndef WATER_COMMON_INCLUDED
#define WATER_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
CBUFFER_END

struct WaterVertexInput
{
    float4 positionOS : POSITION;
    
};

struct WaterVertexOutput
{
    float4 positionCS : SV_POSITION;
};

WaterVertexOutput WaterVertex(WaterVertexInput input)
{
    WaterVertexOutput output;

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

    return output;
}

half4 WaterFragment(WaterVertexOutput input) : SV_Target
{
    return _BaseColor;
}

#endif
