#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
using UnityEngine;
using UnityEngine.Rendering;

using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;

namespace Obi
{
    public class BurstLineRopeRenderSystem : ObiLineRopeRenderSystem
    {
        public BurstLineRopeRenderSystem(ObiSolver solver) : base(solver)
        {
        }

        public override void Setup()
        {
            base.Setup();

            // Initialize each batch:
            for (int i = 0; i < batchList.Count; ++i)
                batchList[i].Initialize(layout, false);
        }

        public override void Render(){}

        public override void RenderFromCamera(Camera camera)
        {
            if (pathSmootherSystem == null)
                return;

            using (m_RenderMarker.Auto())
            {
                var handles = new NativeArray<JobHandle>(batchList.Count, Allocator.Temp);

                for (int i = 0; i < batchList.Count; ++i)
                {
                    var batch = batchList[i];

                    var meshJob = new BuildLineMesh
                    {
                        pathSmootherIndices = pathSmootherIndices.AsNativeArray<int>(),
                        chunkOffsets = pathSmootherSystem.chunkOffsets.AsNativeArray<int>(),

                        frames = pathSmootherSystem.smoothFrames.AsNativeArray<BurstPathFrame>(),
                        frameOffsets = pathSmootherSystem.smoothFrameOffsets.AsNativeArray<int>(),
                        frameCounts = pathSmootherSystem.smoothFrameCounts.AsNativeArray<int>(),

                        vertexOffsets = vertexOffsets.AsNativeArray<int>(),
                        triangleOffsets = triangleOffsets.AsNativeArray<int>(),
                        triangleCounts = triangleCounts.AsNativeArray<int>(),

                        pathData = pathSmootherSystem.pathData.AsNativeArray<BurstPathSmootherData>(),
                        rendererData = rendererData.AsNativeArray<BurstLineMeshData>(),

                        vertices = batch.vertices,
                        tris = batch.triangles,

                        firstRenderer = batch.firstRenderer,
                        localSpaceCamera = m_Solver.transform.InverseTransformPoint(camera.transform.position)
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
        struct BuildLineMesh : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int> pathSmootherIndices;
            [ReadOnly] public NativeArray<int> chunkOffsets;

            [ReadOnly] public NativeArray<BurstPathFrame> frames;
            [ReadOnly] public NativeArray<int> frameOffsets;
            [ReadOnly] public NativeArray<int> frameCounts;

            [ReadOnly] public NativeArray<int> vertexOffsets;
            [ReadOnly] public NativeArray<int> triangleOffsets;
            [ReadOnly] public NativeArray<int> triangleCounts;

            [ReadOnly] public NativeArray<BurstLineMeshData> rendererData;
            [ReadOnly] public NativeArray<BurstPathSmootherData> pathData;

            [NativeDisableParallelForRestriction] public NativeArray<ProceduralRopeVertex> vertices;
            [NativeDisableParallelForRestriction] public NativeArray<int> tris;

            [ReadOnly] public int firstRenderer;

            [ReadOnly] public float3 localSpaceCamera;

            public void Execute(int u)
            {
                int k = firstRenderer + u;
                int s = pathSmootherIndices[k];

                float3 vertex = float3.zero;
                float3 normal = float3.zero;
                float4 bitangent = float4.zero;

                int tri = 0;
                int sectionIndex = 0;
                int firstVertex = vertexOffsets[k];
                int firstTriangle = triangleOffsets[k];

                float smoothLength = 0;
                for (int i = chunkOffsets[s]; i < chunkOffsets[s + 1]; ++i)
                    smoothLength += pathData[i].smoothLength;

                float vCoord = -rendererData[k].uvScale.y * pathData[chunkOffsets[s]].restLength * rendererData[k].uvAnchor;
                float actualToRestLengthRatio = smoothLength / pathData[chunkOffsets[s]].restLength;

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

                        normal.x = frames[index].position.x - localSpaceCamera.x;
                        normal.y = frames[index].position.y - localSpaceCamera.y;
                        normal.z = frames[index].position.z - localSpaceCamera.z;
                        normal = math.normalize(normal);

                        bitangent.x = -(normal.y * frames[index].tangent.z - normal.z * frames[index].tangent.y);
                        bitangent.y = -(normal.z * frames[index].tangent.x - normal.x * frames[index].tangent.z);
                        bitangent.z = -(normal.x * frames[index].tangent.y - normal.y * frames[index].tangent.x);
                        bitangent.xyz = math.normalize(bitangent.xyz);
                        bitangent.w = 1;

                        vertex.x = frames[index].position.x - bitangent.x * sectionThickness;
                        vertex.y = frames[index].position.y - bitangent.y * sectionThickness;
                        vertex.z = frames[index].position.z - bitangent.z * sectionThickness;

                        vertices[firstVertex + sectionIndex * 2] = new ProceduralRopeVertex
                        {
                            pos = vertex,
                            normal = -normal,
                            tangent = bitangent,
                            color = frames[index].color,
                            uv = new float2(0, vCoord)
                        };

                        vertex.x = frames[index].position.x + bitangent.x * sectionThickness;
                        vertex.y = frames[index].position.y + bitangent.y * sectionThickness;
                        vertex.z = frames[index].position.z + bitangent.z * sectionThickness;

                        vertices[firstVertex + sectionIndex * 2 + 1] = new ProceduralRopeVertex
                        {
                            pos = vertex,
                            normal = -normal,
                            tangent = bitangent,
                            color = frames[index].color,
                            uv = new float2(1, vCoord)
                        };

                        if (f < frameCount - 1)
                        {
                            int offset = firstTriangle * 3;
                            tris[offset + tri++] = firstVertex + sectionIndex * 2;
                            tris[offset + tri++] = firstVertex + (sectionIndex + 1) * 2;
                            tris[offset + tri++] = firstVertex + sectionIndex * 2 + 1;

                            tris[offset + tri++] = firstVertex + sectionIndex * 2 + 1;
                            tris[offset + tri++] = firstVertex + (sectionIndex + 1) * 2;
                            tris[offset + tri++] = firstVertex + (sectionIndex + 1) * 2 + 1;
                        }

                        sectionIndex++;
                    }
                }
            }
        }
    }
}
#endif

