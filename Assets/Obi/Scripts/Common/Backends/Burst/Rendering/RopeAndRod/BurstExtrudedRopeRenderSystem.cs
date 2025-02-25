#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using UnityEngine.Rendering;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace Obi
{
    public class BurstExtrudedRopeRenderSystem : ObiExtrudedRopeRenderSystem
    {
        public BurstExtrudedRopeRenderSystem(ObiSolver solver) : base(solver)
        {
        }

        public override void Setup()
        {
            base.Setup();

            // Initialize each batch:
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Initialize(layout, false);
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

                    var meshJob = new BuildExtrudedMesh
                    {
                        pathSmootherIndices = pathSmootherIndices.AsNativeArray<int>(),
                        chunkOffsets = pathSmootherSystem.chunkOffsets.AsNativeArray<int>(),

                        frames = pathSmootherSystem.smoothFrames.AsNativeArray<BurstPathFrame>(),
                        frameOffsets = pathSmootherSystem.smoothFrameOffsets.AsNativeArray<int>(),
                        frameCounts = pathSmootherSystem.smoothFrameCounts.AsNativeArray<int>(),

                        sectionData = sectionData.AsNativeArray<float2>(),
                        sectionOffsets = sectionOffsets.AsNativeArray<int>(),
                        sectionIndices = sectionIndices.AsNativeArray<int>(),

                        vertexOffsets = vertexOffsets.AsNativeArray<int>(),
                        triangleOffsets = triangleOffsets.AsNativeArray<int>(),
                        triangleCounts = triangleCounts.AsNativeArray<int>(),

                        pathData = pathSmootherSystem.pathData.AsNativeArray<BurstPathSmootherData>(),
                        rendererData = rendererData.AsNativeArray<BurstExtrudedMeshData>(),

                        vertices = batch.vertices,
                        tris = batch.triangles,

                        firstRenderer = batch.firstRenderer
                    };
                    handles[i] = meshJob.Schedule(batch.rendererCount, 1);
                }

                JobHandle.CombineDependencies(handles).Complete();
                handles.Dispose();

                for (int i = 0; i < batchList.Count; ++i)
                {
                    var batch = batchList[i];

                    batch.mesh.SetVertexBufferData(batch.vertices, 0, 0, batch.vertexCount, 0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
                    batch.mesh.SetIndexBufferData(batch.triangles, 0, 0, batch.triangleCount * 3, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);

                    var rp = batch.renderParams;
                    rp.worldBounds = m_Solver.bounds;

                    Graphics.RenderMesh(rp, batch.mesh, 0, m_Solver.transform.localToWorldMatrix, m_Solver.transform.localToWorldMatrix);
                }
            }
        }

        [BurstCompile]
        struct BuildExtrudedMesh : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> pathSmootherIndices;
            [ReadOnly] public NativeArray<int> chunkOffsets;

            [ReadOnly] public NativeArray<BurstPathFrame> frames;
            [ReadOnly] public NativeArray<int> frameOffsets;
            [ReadOnly] public NativeArray<int> frameCounts;

            [ReadOnly] public NativeArray<float2> sectionData;
            [ReadOnly] public NativeArray<int> sectionOffsets;
            [ReadOnly] public NativeArray<int> sectionIndices;

            [ReadOnly] public NativeArray<int> vertexOffsets;
            [ReadOnly] public NativeArray<int> triangleOffsets;
            [ReadOnly] public NativeArray<int> triangleCounts;

            [ReadOnly] public NativeArray<BurstExtrudedMeshData> rendererData;
            [ReadOnly] public NativeArray<BurstPathSmootherData> pathData;

            [NativeDisableParallelForRestriction] public NativeArray<ProceduralRopeVertex> vertices;
            [NativeDisableParallelForRestriction] public NativeArray<int> tris;

            [ReadOnly] public int firstRenderer;

            public void Execute(int u)
            {
                int k = firstRenderer + u;
                int s = pathSmootherIndices[k];

                float3 vertex = float3.zero;
                float3 normal = float3.zero;
                float4 texTangent = float4.zero;

                int tri = 0;
                int sectionIndex = 0;
                int sectionStart = sectionOffsets[sectionIndices[k]];
                int sectionSegments = (sectionOffsets[sectionIndices[k] + 1] - sectionStart) - 1;
                int verticesPerSection = sectionSegments + 1;   // the last vertex in each section must be duplicated, due to uv wraparound.

                float smoothLength = 0;
                for (int i = chunkOffsets[s]; i < chunkOffsets[s + 1]; ++i)
                    smoothLength += pathData[i].smoothLength;

                float vCoord = -rendererData[k].uvScale.y * pathData[chunkOffsets[s]].restLength * rendererData[k].uvAnchor;
                float actualToRestLengthRatio = smoothLength / pathData[chunkOffsets[s]].restLength;

                int firstVertex = vertexOffsets[k];
                int firstTriangle = triangleOffsets[k];

                // clear out triangle indices for this rope:
                for (int i = firstTriangle; i < firstTriangle + triangleCounts[k]; ++i)
                {
                    int offset = i * 3;
                    tris[offset] = 0;
                    tris[offset+1] = 0;
                    tris[offset+2] = 0;
                }

                // for each chunk in the rope:
                for (int i = chunkOffsets[s]; i < chunkOffsets[s + 1]; ++i)
                {
                    int firstFrame = frameOffsets[i];
                    int frameCount = frameCounts[i];

                    for (int f = 0; f < frameCount; ++f)
                    {
                        // Calculate previous and next curve indices:
                        int prevIndex = firstFrame + math.max(f - 1, 0);
                        int index = firstFrame + f;

                        // advance v texcoord:
                        vCoord += rendererData[k].uvScale.y * (math.distance(frames[index].position, frames[prevIndex].position) /
                                                          (rendererData[k].normalizeV == 1 ? smoothLength : actualToRestLengthRatio));

                        // calculate section thickness and scale the basis vectors by it:
                        float sectionThickness = frames[index].thickness * rendererData[k].thicknessScale;

                        // Loop around each segment:
                        int nextSectionIndex = sectionIndex + 1;
                        for (int j = 0; j <= sectionSegments; ++j)
                        {
                            // make just one copy of the section vertex:
                            float2 sectionVertex = sectionData[sectionStart + j];

                            // calculate normal using section vertex, curve normal and binormal:
                            normal.x = (sectionVertex.x * frames[index].normal.x + sectionVertex.y * frames[index].binormal.x) * sectionThickness;
                            normal.y = (sectionVertex.x * frames[index].normal.y + sectionVertex.y * frames[index].binormal.y) * sectionThickness;
                            normal.z = (sectionVertex.x * frames[index].normal.z + sectionVertex.y * frames[index].binormal.z) * sectionThickness;

                            // offset curve position by normal:
                            vertex.x = frames[index].position.x + normal.x;
                            vertex.y = frames[index].position.y + normal.y;
                            vertex.z = frames[index].position.z + normal.z;

                            // cross(normal, curve tangent)
                            texTangent.xyz = math.cross(normal, frames[index].tangent);
                            texTangent.w = -1;

                            vertices[firstVertex + sectionIndex * verticesPerSection + j] = new ProceduralRopeVertex
                            {
                                pos = vertex,
                                normal = normal,
                                tangent = texTangent,
                                color = frames[index].color,
                                uv = new float2(j / (float)sectionSegments * rendererData[k].uvScale.x, vCoord)
                            };

                            if (j < sectionSegments && f < frameCount - 1)
                            {
                                int offset = firstTriangle * 3;
                                tris[offset + tri++] = (firstVertex + sectionIndex * verticesPerSection + j);
                                tris[offset + tri++] = (firstVertex + nextSectionIndex * verticesPerSection + j);
                                tris[offset + tri++] = (firstVertex + sectionIndex * verticesPerSection + (j + 1));

                                tris[offset + tri++] = (firstVertex + sectionIndex * verticesPerSection + (j + 1));
                                tris[offset + tri++] = (firstVertex + nextSectionIndex * verticesPerSection + j);
                                tris[offset + tri++] = (firstVertex + nextSectionIndex * verticesPerSection + (j + 1));
                            }
                        }
                        sectionIndex++;
                    }
                }
            }
        }
    }
}
#endif

