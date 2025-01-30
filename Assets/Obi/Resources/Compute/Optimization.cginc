#ifndef OPTIMIZATION_INCLUDE
#define OPTIMIZATION_INCLUDE

#include "MathUtils.cginc"
#include "SurfacePoint.cginc"

void GetInterpolatedSimplexData(in int simplexStart,
                                in int simplexSize,
                                StructuredBuffer<int> simplices,
                                StructuredBuffer<float4> positions,
                                StructuredBuffer<quaternion> orientations,
                                StructuredBuffer<float4> radii,
                                float4 convexBary,
                                inout float4 convexPoint,
                                inout float4 convexRadii,
                                inout float4 convexOrientation)
{
    convexPoint = FLOAT4_ZERO;
    convexRadii = FLOAT4_ZERO;
    convexOrientation = quaternion(0, 0, 0, 0);
    for (int j = 0; j < simplexSize; ++j)
    {
        int particle = simplices[simplexStart + j];
        convexPoint += positions[particle] * convexBary[j];
        convexRadii += radii[particle] * convexBary[j];
        convexOrientation += orientations[particle] * convexBary[j];
    }
    convexPoint.w = 0;
}

// Frank-Wolfe convex optimization algorithm. Returns closest point to a simplex in a signed distance function.
void FrankWolfe(in IDistanceFunction f,
                in int simplexStart,
                in int simplexSize,
                StructuredBuffer<float4>  positions,
                StructuredBuffer<quaternion>  orientations,
                StructuredBuffer<float4>  radii,
                StructuredBuffer<int>  simplices,
                inout float4 convexPoint,
                inout float4 convexThickness,
                inout quaternion convexOrientation,
                inout float4 convexBary,
                inout SurfacePoint pointInFunction,
                int maxIterations,
                float tolerance)
{
    for (int i = 0; i < maxIterations; ++i)
    {
        // sample target function:
        f.Evaluate(convexPoint, convexThickness, convexOrientation, pointInFunction);

        // find descent direction:
        int descent = 0;
        float gap = FLT_MIN;
        for (int j = 0; j < simplexSize; ++j)
        {
            int particle = simplices[simplexStart + j];
            float4 candidate = positions[particle] - convexPoint;
            candidate.w = 0;

            // here, we adjust the candidate by projecting it to the engrosed simplex's surface:
            candidate -= pointInFunction.normal * (radii[particle].x - convexThickness.x);

            float corr = dot(-pointInFunction.normal, candidate);
            if (corr > gap)
            {
                descent = j;
                gap = corr;
            }
        }

        // if the duality gap is below tolerance threshold, stop iterating.
        if (gap < tolerance)
            break;

        // update the barycentric coords using 2/(i+2) as  the step factor
        float stp = 0.3f * 2.0f / (i + 2);
        convexBary *= 1 - stp;
        switch(descent)
        {
            case 0: convexBary[0] += stp;break;
            case 1: convexBary[1] += stp;break;
            case 2: convexBary[2] += stp;break;
            case 3: convexBary[3] += stp;break;
        }

        // get cartesian coordinates of current solution:
        GetInterpolatedSimplexData(simplexStart, simplexSize, simplices, positions, orientations, radii, convexBary, convexPoint, convexThickness, convexOrientation);
    }   
}

SurfacePoint Optimize(in IDistanceFunction f,
                       StructuredBuffer<float4> positions,
                       StructuredBuffer<quaternion> orientations,
                       StructuredBuffer<float4> radii,
                       StructuredBuffer<int> simplices,
                       in int simplexStart,
                       in int simplexSize,
                       inout float4 convexBary,
                       out float4 convexPoint,
                       in int maxIterations = 16,
                       in float tolerance = 0.004f)
{
    SurfacePoint pointInFunction;

    // get cartesian coordinates of the initial guess:
    float4 convexThickness;
    quaternion convexOrientation;
    GetInterpolatedSimplexData(simplexStart, simplexSize, simplices, positions, orientations, radii, convexBary, convexPoint, convexThickness, convexOrientation);

    // for a 0-simplex (point), perform a single evaluation:
    if (simplexSize == 1 || maxIterations < 1)
        f.Evaluate(convexPoint, convexThickness, convexOrientation, pointInFunction);

    // for a 1-simplex (edge), perform golden ratio search:
    //else if (simplexSize == 2)
      //  GoldenSearch(ref function, simplexStart, simplexSize, positions, orientations, radii, simplices, ref convexPoint, ref convexThickness, ref convexOrientation, ref convexBary, ref pointInFunction, maxIterations, tolerance * 10);

    // for higher-order simplices, use general Frank-Wolfe convex optimization:
    else
       FrankWolfe(f, simplexStart, simplexSize, positions, orientations, radii, simplices, convexPoint, convexThickness, convexOrientation, convexBary, pointInFunction, maxIterations, tolerance);
    
    return pointInFunction;
}

#endif