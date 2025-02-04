#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using Unity.Jobs;
using Unity.Mathematics;

namespace Obi
{
    public class BurstPathSmootherRenderSystem : ObiPathSmootherRenderSystem
    {
        public BurstPathSmootherRenderSystem(ObiSolver solver) : base(solver)
        {
        }

        public override void Render()
        {
            using (m_RenderMarker.Auto())
            {
                base.Render();

                // generate raw frames using parallel transport
                var parallelTransportJob = new ParallelTransportJob
                {
                    pathFrames = rawFrames.AsNativeArray<BurstPathFrame>(),
                    frameOffsets = rawFrameOffsets.AsNativeArray<int>(),
                    particleIndices = particleIndices.AsNativeArray<int>(),
                    renderablePositions = m_Solver.renderablePositions.AsNativeArray<float4>(),
                    renderableOrientations = m_Solver.renderableOrientations.AsNativeArray<quaternion>(),
                    principalRadii = m_Solver.principalRadii.AsNativeArray<float4>(),
                    colors = m_Solver.colors.AsNativeArray<float4>(),
                    pathData = pathData.AsNativeArray<BurstPathSmootherData>()
                };

                var handle = parallelTransportJob.Schedule(rawFrameOffsets.count, 4);

                // throw away unneeded frames using decimation
                var decimationJob = new DecimateChunksJob
                {
                    inputFrames = rawFrames.AsNativeArray<BurstPathFrame>(),
                    inputFrameOffsets = rawFrameOffsets.AsNativeArray<int>(),
                    outputFrameCounts = decimatedFrameCounts.AsNativeArray<int>(),
                    pathData = pathData.AsNativeArray<BurstPathSmootherData>()
                };

                handle = decimationJob.Schedule(rawFrameOffsets.count, 4, handle);

                // smooth chunks:
                var chaikinJob = new ChaikinSmoothChunksJob()
                {
                    inputFrames = rawFrames.AsNativeArray<BurstPathFrame>(),
                    inputFrameOffsets = rawFrameOffsets.AsNativeArray<int>(),
                    inputFrameCounts = decimatedFrameCounts.AsNativeArray<int>(),
                    outputFrames = smoothFrames.AsNativeArray<BurstPathFrame>(),
                    outputFrameOffsets = smoothFrameOffsets.AsNativeArray<int>(),
                    outputFrameCounts = smoothFrameCounts.AsNativeArray<int>(),
                    pathData = pathData.AsNativeArray<BurstPathSmootherData>()
                };

                chaikinJob.Schedule(rawFrameOffsets.count, 4, handle).Complete();
            }
        }
    }
}
#endif

