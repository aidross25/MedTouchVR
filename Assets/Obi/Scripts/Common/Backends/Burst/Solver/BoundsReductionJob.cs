#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using System;
using System.Collections;

namespace Obi
{
    [BurstCompile]
    struct CalculateSimplexBoundsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float4> radii;
        [ReadOnly] public NativeArray<float4> fluidMaterials;
        [ReadOnly] public NativeArray<float4> positions;
        [ReadOnly] public NativeArray<float4> velocities;

        // simplex arrays:
        [ReadOnly] public NativeArray<int> simplices;
        [ReadOnly] public SimplexCounts simplexCounts;

        [ReadOnly] public NativeArray<int> particleMaterialIndices;
        [ReadOnly] public NativeArray<BurstCollisionMaterial> collisionMaterials;
        public NativeArray<BurstAabb> simplexBounds;
        public NativeArray<BurstAabb> reducedBounds;

        [ReadOnly] public Oni.SolverParameters parameters;
        [ReadOnly] public float dt;

        public void Execute(int i)
        {
            int simplexStart = simplexCounts.GetSimplexStartAndSize(i, out int simplexSize);

            var sxBounds = new BurstAabb(float.MaxValue, float.MinValue);
            var soBounds = new BurstAabb(float.MaxValue, float.MinValue);

            for (int j = 0; j < simplexSize; ++j)
            {
                int p = simplices[simplexStart + j];

                int m = particleMaterialIndices[p];
                float solidRadius = radii[p].x + parameters.collisionMargin + (m >= 0 ? collisionMaterials[m].stickDistance : 0);

                // Expand simplex bounds, using both the particle's original position and its velocity.
                sxBounds.EncapsulateParticle(positions[p],
                                             BurstIntegration.IntegrateLinear(positions[p], velocities[p], dt * parameters.particleCCD),
                                             math.max(solidRadius, fluidMaterials[p].x * 0.5f));

                soBounds.EncapsulateParticle(positions[p],
                                             BurstIntegration.IntegrateLinear(positions[p], velocities[p], dt),
                                             solidRadius);
            }

            simplexBounds[i] = sxBounds;
            reducedBounds[i] = soBounds;
        }
    }

    [BurstCompile]
    struct BoundsReductionJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] public NativeArray<BurstAabb> bounds; // the length of bounds must be a multiple of size.
        [ReadOnly] public int stride;
        [ReadOnly] public int size;

        public void Execute(int first)
        {
            int baseIndex = first * size;
            for (int i = 1; i < size; ++i)
            {
                int dest = baseIndex * stride;
                int source = (baseIndex + i) * stride;

                if (source < bounds.Length)
                {
                    var v = bounds[dest];
                    v.EncapsulateBounds(bounds[source]);
                    bounds[dest] = v;
                }
            }
        }
    }
}
#endif