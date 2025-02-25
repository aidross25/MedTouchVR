#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine.Rendering;

namespace Obi
{
    public class BurstMeshRopeRenderSystem : ObiMeshRopeRenderSystem
    {
        public BurstMeshRopeRenderSystem(ObiSolver solver) : base(solver)
        {
        }

        protected override void CloseBatches()
        {
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Initialize(sortedRenderers, meshData, meshIndices, layout, false);

            base.CloseBatches();
        }

        public override void Render()
        {
            if (pathSmootherSystem == null)
                return;

            using (m_RenderMarker.Auto())
            {
                var handles = new NativeArray<JobHandle>(batchList.Count, Allocator.Temp);

                for (int i = 0; i < batchList.Count; ++i)
                {
                    var batch = batchList[i];

                    var meshJob = new BuildRopeMeshJob
                    {
                        chunkOffsets = pathSmootherSystem.chunkOffsets.AsNativeArray<int>(),
                        pathSmootherIndices = pathSmootherIndices.AsNativeArray<int>(),

                        frames = pathSmootherSystem.smoothFrames.AsNativeArray<BurstPathFrame>(),
                        frameOffsets = pathSmootherSystem.smoothFrameOffsets.AsNativeArray<int>(),
                        frameCounts = pathSmootherSystem.smoothFrameCounts.AsNativeArray<int>(),

                        vertexOffsets = vertexOffsets.AsNativeArray<int>(),

                        meshIndices = meshIndices.AsNativeArray<int>(),
                        meshData = meshData.meshData.AsNativeArray<MeshDataBatch.MeshData>(),

                        rendererData = rendererData.AsNativeArray<BurstMeshData>(),
                        pathData = pathSmootherSystem.pathData.AsNativeArray<BurstPathSmootherData>(),

                        sortedIndices = sortedIndices.AsNativeArray<int>(),
                        sortedOffsets = sortedOffsets.AsNativeArray<int>(),

                        positions = meshData.restPositions.AsNativeArray<float3>(),
                        normals = meshData.restNormals.AsNativeArray<float3>(),
                        tangents = meshData.restTangents.AsNativeArray<float4>(),
                        colors = meshData.restColors.AsNativeArray<float4>(),

                        vertices = batch.dynamicVertexData.AsNativeArray<RopeMeshVertex>(),

                        firstRenderer = batch.firstRenderer

                    };
                    handles[i] = meshJob.Schedule(batch.rendererCount, 1);
                }

                JobHandle.CombineDependencies(handles).Complete();
                handles.Dispose();

                for (int i = 0; i < batchList.Count; ++i)
                {
                    var batch = batchList[i];

                    batch.mesh.SetVertexBufferData(batch.dynamicVertexData.AsNativeArray<DynamicBatchVertex>(), 0, 0, batch.vertexCount, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontNotifyMeshUsers);

                    var rp = batch.renderParams;
                    rp.worldBounds = m_Solver.bounds;

                    for (int m = 0; m < batch.materials.Length; ++m)
                    {
                        rp.material = batch.materials[m];
                        Graphics.RenderMesh(rp, batch.mesh, m, m_Solver.transform.localToWorldMatrix, m_Solver.transform.localToWorldMatrix);

                        // Unity bug: Graphics.RenderMesh consistently crashes when existing play mode (seems fixed in 2021.3.4f1)
                        // https://issuetracker.unity3d.com/issues/the-editor-crashes-on-exit-when-using-graphics-dot-rendermesh
                        //renderParams.material = batch.materials[m];
                        //renderParams.camera = null;
                        //Graphics.RenderMesh(renderParams, batch.mesh, m, m_Solver.transform.localToWorldMatrix);
                    }
                }
            }
        }

        [BurstCompile]
        struct BuildRopeMeshJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> pathSmootherIndices;
            [ReadOnly] public NativeArray<int> chunkOffsets;
            [ReadOnly] public NativeArray<BurstPathFrame> frames;
            [ReadOnly] public NativeArray<int> frameOffsets;
            [ReadOnly] public NativeArray<int> frameCounts;

            [ReadOnly] public NativeArray<int> vertexOffsets;

            [ReadOnly] public NativeArray<int> meshIndices;
            [ReadOnly] public NativeArray<MeshDataBatch.MeshData> meshData;

            [ReadOnly] public NativeArray<BurstMeshData> rendererData;
            [ReadOnly] public NativeArray<BurstPathSmootherData> pathData;

            [ReadOnly] public NativeArray<int> sortedIndices;
            [ReadOnly] public NativeArray<int> sortedOffsets;

            [ReadOnly] public NativeArray<float3> positions;
            [ReadOnly] public NativeArray<float3> normals;
            [ReadOnly] public NativeArray<float4> tangents;
            [ReadOnly] public NativeArray<float4> colors;

            [NativeDisableParallelForRestriction] public NativeArray<RopeMeshVertex> vertices;

            [ReadOnly] public int firstRenderer;

            public void Execute(int i)
            {
                int rendererIndex = firstRenderer + i;
                int pathIndex = pathSmootherIndices[rendererIndex];
                var renderer = rendererData[rendererIndex];

                // get mesh data:
                var mesh = meshData[meshIndices[rendererIndex]];
                var sortedOffset = sortedOffsets[rendererIndex];

                // get index of first output vertex:
                int firstOutputVertex = vertexOffsets[rendererIndex];

                // get index of first chunk, ignore others (no support for tearing):
                int chunkIndex = chunkOffsets[pathIndex]; 

                // get first frame and frame count:
                int firstFrame = frameOffsets[chunkIndex];
                int lastFrame = firstFrame + frameCounts[chunkIndex] - 1;

                // get mesh deform axis:
                int axis = (int)renderer.axis;

                // initialize scale vector:
                float3 actualScale = (Vector3)renderer.scale;

                // calculate stretch ratio:
                float stretchRatio = renderer.stretchWithRope == 1 ? pathData[chunkIndex].smoothLength / pathData[chunkIndex].restLength : 1;

                // squashing factor, makes mesh thinner when stretched and thicker when compresssed.
                float squashing = math.clamp(1 + renderer.volumeScaling * (1 / math.max(stretchRatio, 0.01f) - 1), 0.01f, 2);

                // calculate scale along swept axis so that the mesh spans the entire lenght of the rope if required.
                if (renderer.spanEntireLength == 1)
                {
                    float totalMeshLength = renderer.meshSizeAlongAxis * renderer.instances;
                    float totalSpacing = renderer.instanceSpacing * (renderer.instances - 1);
                    actualScale[axis] = pathData[chunkIndex].restLength / (totalMeshLength + totalSpacing);
                }

                // adjust axis lenght by stretch ratio:
                actualScale[axis] *= stretchRatio;

                // init loop variables:
                float lengthAlongAxis = renderer.offset;
                int index = firstFrame;
                int nextIndex = firstFrame + 1;
                int prevIndex = firstFrame;
                float nextMagnitude = math.distance(frames[index].position, frames[nextIndex].position);
                float prevMagnitude = nextMagnitude;

                for (int k = 0; k < renderer.instances; ++k)
                {
                    for (int j = 0; j < mesh.vertexCount; ++j)
                    {
                        int currVIndex = mesh.firstVertex + sortedIndices[sortedOffset + j]; 
                        int prevVIndex = mesh.firstVertex + sortedIndices[sortedOffset + math.max(0,j - 1)];

                        // calculate how much we've advanced in the sort axis since the last vertex:
                        lengthAlongAxis += (positions[currVIndex][axis] - positions[prevVIndex][axis]) * actualScale[axis];

                        // check if we have moved to a new section of the curve:
                        BurstPathFrame frame;
                        if (lengthAlongAxis < 0)
                        {
                            while (-lengthAlongAxis > prevMagnitude && index > firstFrame)
                            {
                                lengthAlongAxis += prevMagnitude;
                                index = math.max(index - 1, firstFrame);
                                nextIndex = math.min(index + 1, lastFrame);
                                prevIndex = math.max(index - 1, firstFrame);
                                nextMagnitude = math.distance(frames[index].position, frames[nextIndex].position);
                                prevMagnitude = math.distance(frames[index].position, frames[prevIndex].position);
                            }

                            var offset = float3.zero;
                            if (index == prevIndex)
                            {
                                offset = frames[index].position - frames[nextIndex].position;
                                prevMagnitude = math.length(offset);
                            }

                            frame = InterpolateFrames(frames[index], frames[prevIndex], offset, -lengthAlongAxis / prevMagnitude);
                        }
                        else
                        {
                            while (lengthAlongAxis > nextMagnitude && index < lastFrame)
                            {
                                lengthAlongAxis -= nextMagnitude;
                                index = math.min(index + 1, lastFrame);
                                nextIndex = math.min(index + 1, lastFrame);
                                prevIndex = math.max(index - 1, firstFrame);
                                nextMagnitude = math.distance(frames[index].position, frames[nextIndex].position);
                                prevMagnitude = math.distance(frames[index].position, frames[prevIndex].position);
                            }

                            var offset = float3.zero;
                            if (index == nextIndex)
                            {
                                offset = frames[index].position - frames[prevIndex].position;
                                nextMagnitude = math.length(offset);
                            }

                            frame = InterpolateFrames(frames[index], frames[nextIndex], offset, lengthAlongAxis / nextMagnitude);
                        }

                        // update basis matrix:
                        var basis = frame.ToMatrix(axis);

                        // calculate vertex offset from curve:
                        float3 offsetFromCurve = positions[currVIndex] * actualScale * frame.thickness * squashing;
                        offsetFromCurve[axis] = 0;

                        // write modified vertex data:
                        vertices[firstOutputVertex + sortedIndices[sortedOffset + j]] = new RopeMeshVertex
                        {
                            pos = frame.position + math.mul(basis, offsetFromCurve),
                            normal = math.mul(basis, normals[currVIndex]),
                            tangent = new float4(math.mul(basis, tangents[currVIndex].xyz), tangents[currVIndex].w),
                            color = colors[currVIndex] * frame.color,
                        };
                    }

                    firstOutputVertex += mesh.vertexCount;
                    lengthAlongAxis += renderer.instanceSpacing * actualScale[axis];
                }
            }

            BurstPathFrame InterpolateFrames(BurstPathFrame a, BurstPathFrame b, float3 bOffset, float t)
            {
                // this offset is used to displace a copy of the first and last frames of the path,
                // to ensure meshes extrude correctly prior to the first or past the last frame. 
                b.position += bOffset;
                var interp = (1 - t) * a + t * b;

                // (no need to renormalize tangent, since offsetFromCurve[axis] = 0)
                interp.normal = math.normalize(interp.normal);
                interp.binormal = math.normalize(interp.binormal);
                return interp;
            }
        }
    }
}
#endif

