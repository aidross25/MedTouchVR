#ifndef SIMPLEX_INCLUDE
#define SIMPLEX_INCLUDE

#include "Optimization.cginc"

uint pointCount;
uint edgeCount;
uint triangleCount;

int GetSimplexStartAndSize(in uint index, out uint size)
{
    size = 0;
    int start = 0;

    if (index < triangleCount)
    {
        size = 3;
        start = index * 3;
    }
    else if (index < triangleCount + edgeCount)
    {
        size = 2;
        start = triangleCount * 3 + (index - triangleCount) * 2;
    }
    else if (index < triangleCount + edgeCount + pointCount)
    {
        size = 1;
        start = triangleCount * 3 + edgeCount * 2 + (index - triangleCount - edgeCount);
    }
    return start;
}

float4 BarycenterForSimplexOfSize(in int simplexSize)
{
    switch(simplexSize)
    {
        case 1: return float4(1,0,0,0);
        case 2: return float4(0.5,0.5,0,0);
        case 3: return float4(1/3.0,1/3.0,1/3.0,0);
        case 4: return float4(0.25,0.25,0.25,0.25);
        default: return float4(1,0,0,0);
    }
}

struct Simplex : IDistanceFunction
{
    StructuredBuffer<float4> positions;
    StructuredBuffer<float4> radii;
    StructuredBuffer<int> simplices;

    int simplexStart;
    int simplexSize;

    void Evaluate(in float4 pos, in float4 radii, in quaternion orientation, inout SurfacePoint projectedPoint)
    {
        switch (simplexSize)
        {
            case 1:
            default:
                {
                    float4 p1 = positions[simplices[simplexStart]]; p1.w = 0;
                    projectedPoint.bary = float4(1, 0, 0, 0);
                    projectedPoint.pos = p1;
                }
                break;
            case 2:
                {
                    float4 p1 = positions[simplices[simplexStart]]; p1.w = 0;
                    float4 p2 = positions[simplices[simplexStart + 1]]; p2.w = 0;
                    float mu;
                    NearestPointOnEdge(p1, p2, pos, mu);
                    projectedPoint.bary = float4(1 - mu, mu, 0, 0);
                    projectedPoint.pos = p1 * projectedPoint.bary[0] + p2 * projectedPoint.bary[1];
        
                }break;
            case 3:
                {
                    CachedTri tri;
                    tri.Cache(float4(positions[simplices[simplexStart]].xyz,0),
                              float4(positions[simplices[simplexStart + 1]].xyz,0),
                              float4(positions[simplices[simplexStart + 2]].xyz,0));
                    projectedPoint.pos = NearestPointOnTri(tri, pos, projectedPoint.bary);
                }break;
        }
        projectedPoint.normal = normalizesafe(pos - projectedPoint.pos);
    }

};
#endif