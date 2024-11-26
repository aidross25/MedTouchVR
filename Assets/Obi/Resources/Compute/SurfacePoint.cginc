#ifndef SURFACEPOINT_INCLUDE
#define SURFACEPOINT_INCLUDE

/**
 * point in the surface of a signed distance field.
 */
struct SurfacePoint
{
    float4 bary;
    float4 pos;
    float4 normal;
};

interface IDistanceFunction
{
    void Evaluate(in float4 pos, in float4 radii, in quaternion orientation, inout SurfacePoint projectedPoint);
};

#endif