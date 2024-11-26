#ifndef PROCEDURAL_INCLUDED
#define PROCEDURAL_INCLUDED

#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"

StructuredBuffer<float4x4> _InstanceTransforms;
StructuredBuffer<float4x4> _InvInstanceTransforms;
StructuredBuffer<float4> _Colors;

#if UNITY_ANY_INSTANCING_ENABLED
    // Based on : 
    // https://github.com/Unity-Technologies/Graphics/blob/master/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ParticlesInstancing.hlsl
    // and/or
    // https://github.com/TwoTailsGames/Unity-Built-in-Shaders/blob/master/CGIncludes/UnityStandardParticleInstancing.cginc

    void vertInstancingSetup() {
    }
#endif

void GetInstanceData_float(uint svInstanceID, float3 pos, out float3 outPos, out float4 color)
{
    InitIndirectDrawArgs(0);
    uint instanceID = GetIndirectInstanceID_Base(svInstanceID);
        
    unity_ObjectToWorld = _InstanceTransforms[instanceID];
    unity_WorldToObject = _InvInstanceTransforms[instanceID];
    color = _Colors[instanceID];

    outPos = pos;
}

#endif