using System;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

namespace Obi
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DynamicBatchVertex
    {
        public Vector3 pos;
        public Vector3 normal;
        public Vector4 tangent;
        public Vector4 color;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StaticBatchVertex
    {
        public Vector2 uv;
        public Vector2 uv2;
        public Vector2 uv3;
        public Vector2 uv4;
    }

    public class DynamicRenderBatch<T> : IRenderBatch where T : IMeshDataProvider, IActorRenderer
    {
        private VertexAttributeDescriptor[] vertexLayout;

        private RenderBatchParams renderBatchParams;
        public RenderParams renderParams { get; private set; }

        public Material[] materials;
        public Mesh mesh;

        public int firstRenderer;
        public int rendererCount;

        public ObiNativeList<int> vertexToRenderer; // for each vertex in the batch, index of its renderer
        public ObiNativeList<int> particleToRenderer; // for each particle in the batch, index of its renderer

        public ObiNativeList<int> particleIndices; // solver indices for all renderers in the batch

        public ObiNativeList<DynamicBatchVertex> dynamicVertexData;
        public ObiNativeList<StaticBatchVertex> staticVertexData;
        public ObiNativeList<int> triangles;

        public GraphicsBuffer gpuVertexBuffer;

        public int vertexCount;
        public int triangleCount => triangles.count / 3;
        public int particleCount => particleIndices.count;

        public DynamicRenderBatch(int rendererIndex, int vertexCount, Material[] materials, RenderBatchParams param)
        {
            this.renderBatchParams = param;
            this.materials = materials;
            this.vertexCount = vertexCount;

            this.firstRenderer = rendererIndex;
            this.rendererCount = 1;
        }

        public void Initialize(List<T> renderers,
                               MeshDataBatch meshData,
                               ObiNativeList<int> meshIndices,
                               VertexAttributeDescriptor[] layout,
                               bool gpu = false)
        {
            renderParams = renderBatchParams.ToRenderParams();
            vertexLayout = layout;

            mesh = new Mesh();

            vertexToRenderer = new ObiNativeList<int>();
            particleToRenderer = new ObiNativeList<int>();
            particleIndices = new ObiNativeList<int>();

            dynamicVertexData = new ObiNativeList<DynamicBatchVertex>();
            staticVertexData = new ObiNativeList<StaticBatchVertex>();
            triangles = new ObiNativeList<int>();

            // there will be exactly one submesh per material in the output batch.
            // so we iterate trough materials, and for each one, build a submesh by merging the
            // renderer's submeshes. If a renderer has less submeshes than materials, reuse the last one.

            SubMeshDescriptor[] descriptors = new SubMeshDescriptor[materials.Length];

            for (int m = 0; m < materials.Length; ++m)
            {
                int vertexOffset = 0;
                var desc = new SubMeshDescriptor();
                desc.indexStart = triangles.count;

                for (int i = firstRenderer; i < firstRenderer + rendererCount; ++i)
                {
                    var renderer = renderers[i];
                    int meshIndex = meshIndices[i];
                    int submeshIndex = Mathf.Min(m, renderer.sourceMesh.subMeshCount - 1);
                    var submeshInfo = renderer.sourceMesh.GetSubMesh(submeshIndex);

                    var meshTriangles = meshData.GetTriangles(meshIndex);
                    for (int k = 0; k < renderer.meshInstances; ++k)
                    {
                        // append submesh triangles:
                        for (int t = submeshInfo.indexStart; t < submeshInfo.indexStart + submeshInfo.indexCount; ++t)
                            triangles.Add(vertexOffset + meshTriangles[t]);

                        vertexOffset += meshData.GetVertexCount(meshIndex);
                    }
                }

                desc.indexCount = triangles.count - desc.indexStart;
                descriptors[m] = desc;
            }

            // vertices:
            for (int i = firstRenderer; i < firstRenderer + rendererCount; ++i)
            {
                var renderer = renderers[i];
                int meshIndex = meshIndices[i];

                int vCount = meshData.GetVertexCount(meshIndex);

                for (int k = 0; k < renderer.meshInstances; ++k)
                {
                    vertexToRenderer.AddReplicate(i, vCount);
                    particleToRenderer.AddReplicate(i, renderer.actor.solverIndices.count);
                    particleIndices.AddRange(renderer.actor.solverIndices);

                    var verts = meshData.GetVertices(meshIndex);
                    var norms = meshData.GetNormals(meshIndex);
                    var tan = meshData.GetTangents(meshIndex);
                    var col = meshData.GetColors(meshIndex);

                    var uv = meshData.GetUV(meshIndex);
                    var uv2 = meshData.GetUV2(meshIndex);
                    var uv3 = meshData.GetUV3(meshIndex);
                    var uv4 = meshData.GetUV4(meshIndex);

                    for (int j = 0; j < vCount; ++j)
                    {
                        dynamicVertexData.Add(new DynamicBatchVertex
                        {
                            pos = verts[j],
                            normal = norms[j],
                            tangent = tan[j],
                            color = j < col.Length ? (Vector4)col[j] : Vector4.one
                        });

                        staticVertexData.Add(new StaticBatchVertex
                        {
                            uv = j < uv.Length ? uv[j] : Vector2.zero,
                            uv2 = j < uv2.Length ? uv2[j] : Vector2.zero,
                            uv3 = j < uv3.Length ? uv3[j] : Vector2.zero,
                            uv4 = j < uv4.Length ? uv4[j] : Vector2.zero,
                        });
                    }
                }
            }

            // setup combined mesh:
            mesh.SetVertexBufferParams(vertexCount, layout);
            mesh.SetIndexBufferParams(triangles.count, IndexFormat.UInt32);

            mesh.SetVertexBufferData(dynamicVertexData.AsNativeArray<DynamicBatchVertex>(), 0, 0, dynamicVertexData.count, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.SetVertexBufferData(staticVertexData.AsNativeArray<StaticBatchVertex>(), 0, 0, staticVertexData.count, 1, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            mesh.SetIndexBufferData(triangles.AsNativeArray<int>(), 0, 0, triangles.count, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            // set submeshes:
            mesh.subMeshCount = materials.Length;
            for (int m = 0; m < materials.Length; ++m)
                mesh.SetSubMesh(m, descriptors[m], MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            if (gpu)
            {
                dynamicVertexData.Dispose();

                mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

                // meshes with no vertices will have no vertex buffer, and Unity will throw an exception.
                try
                {
                    if (mesh.vertexCount > 0)
                    {
                        gpuVertexBuffer ??= mesh.GetVertexBuffer(0);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                particleIndices.AsComputeBuffer<int>();
                vertexToRenderer.AsComputeBuffer<int>();
                particleToRenderer.AsComputeBuffer<int>();
            }
        }

        public void Dispose()
        {
            if (vertexToRenderer != null)
                vertexToRenderer.Dispose();
            if (particleToRenderer != null)
                particleToRenderer.Dispose();

            if (particleIndices != null)
                particleIndices.Dispose();

            if (dynamicVertexData != null)
                dynamicVertexData.Dispose();
            if (staticVertexData != null)
                staticVertexData.Dispose();
            if (triangles != null)
                triangles.Dispose();

            gpuVertexBuffer?.Dispose();
            gpuVertexBuffer = null;

            GameObject.DestroyImmediate(mesh);
        }

        public bool TryMergeWith(IRenderBatch other)
        {
            var pbatch = other as DynamicRenderBatch<T>;
            if (pbatch != null)
            {
                if (CompareTo(pbatch) == 0 &&
                    vertexCount + pbatch.vertexCount < Constants.maxVertsPerMesh)
                {
                    rendererCount += pbatch.rendererCount;
                    vertexCount += pbatch.vertexCount;
                    return true;
                }
            }
            return false;
        }

        private static int CompareMaterialLists(Material[] a, Material[] b)
        {
            int l = Mathf.Min(a.Length, b.Length);
            for (int i = 0; i < l; ++i)
            {
                if (a[i] == null && b[i] == null)
                    return 0;

                if (a[i] == null) return -1;
                if (b[i] == null) return 1;

                int compare = a[i].GetInstanceID().CompareTo(b[i].GetInstanceID());
                if (compare != 0)
                    return compare;
            }
            return a.Length.CompareTo(b.Length);
        }

        public int CompareTo(IRenderBatch other)
        {
            var pbatch = other as DynamicRenderBatch<T>;
            int result = CompareMaterialLists(materials, pbatch.materials);
            if (result == 0)
                return renderBatchParams.CompareTo(pbatch.renderBatchParams);
            return result;
        }

        public void BakeMesh(List<T> renderers,
                             T renderer,
                             ref Mesh bakedMesh, bool transformToActorLocalSpace = false)
        {
            // if the dynamic data is not available (such as when the batch is intended for GPU use), read it back:
            bool gpu = !dynamicVertexData.isCreated || dynamicVertexData == null;
            if (gpu)
            {
                dynamicVertexData = new ObiNativeList<DynamicBatchVertex>();
                dynamicVertexData.ResizeUninitialized(this.vertexCount);
                var nativeArray = dynamicVertexData.AsNativeArray<DynamicBatchVertex>();
                AsyncGPUReadback.RequestIntoNativeArray(ref nativeArray, gpuVertexBuffer, this.vertexCount * dynamicVertexData.stride, 0).WaitForCompletion();
            }

            bakedMesh.Clear();

            int vOffset = 0;
            int tOffset = 0;

            for (int i = firstRenderer; i < firstRenderer + rendererCount; ++i)
            {
                // Count vertices of all instances:
                int vCount = 0;
                for (int k = 0; k < renderers[i].meshInstances; ++k)
                    vCount += renderers[i].sourceMesh.vertexCount;

                // Count triangles of all submeshes/instances:
                int tCount = 0;
                for (int m = 0; m < materials.Length; ++m)
                {
                    int submeshIndex = Mathf.Min(m, renderers[i].sourceMesh.subMeshCount - 1);
                    var submeshInfo = renderers[i].sourceMesh.GetSubMesh(submeshIndex);
                    tCount += submeshInfo.indexCount * (int)renderers[i].meshInstances;
                }

                // if this is the renderer we're interested in, populate the mesh:
                if (renderers[i].Equals(renderer))
                {
                    bakedMesh.SetVertexBufferParams(vCount, vertexLayout);
                    bakedMesh.SetVertexBufferData(dynamicVertexData.AsNativeArray<DynamicBatchVertex>(), vOffset, 0, vCount, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                    bakedMesh.SetVertexBufferData(staticVertexData.AsNativeArray<StaticBatchVertex>(), vOffset, 0, vCount, 1, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                    // transform vertices from solver space to actor space:
                    if (transformToActorLocalSpace)
                    {
                        var solver2Actor = renderer.actor.actorSolverToLocalMatrix;
                        var verts = bakedMesh.vertices;
                        for (int v = 0; v < verts.Length; ++v)
                            verts[v] = solver2Actor.MultiplyPoint3x4(verts[v]);
                        bakedMesh.vertices = verts;
                    }

                    ObiNativeList<int> indices = new ObiNativeList<int>(tCount);

                    // calculate submeshes (one submesh per material):
                    SubMeshDescriptor[] descriptors = new SubMeshDescriptor[materials.Length];
                    for (int m = 0; m < materials.Length; ++m)
                    {
                        int vertexOffset = 0;
                        var desc = new SubMeshDescriptor();
                        desc.indexStart = indices.count;

                        int submeshIndex = Mathf.Min(m, renderer.sourceMesh.subMeshCount - 1);
                        var submeshInfo = renderer.sourceMesh.GetSubMesh(submeshIndex);

                        for (int k = 0; k < renderer.meshInstances; ++k)
                        {
                            // append submesh triangles:
                            var meshTriangles = renderer.sourceMesh.triangles;
                            for (int t = submeshInfo.indexStart; t < submeshInfo.indexStart + submeshInfo.indexCount; ++t)
                                indices.Add(vertexOffset + meshTriangles[t]);

                            vertexOffset += renderer.sourceMesh.vertexCount;
                        }

                        desc.indexCount = indices.count - desc.indexStart;
                        descriptors[m] = desc;
                    }

                    bakedMesh.SetIndexBufferParams(tCount, IndexFormat.UInt32);
                    bakedMesh.SetIndexBufferData(indices.AsNativeArray<int>(), 0, 0, tCount, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

                    bakedMesh.subMeshCount = materials.Length;
                    for (int m = 0; m < materials.Length; ++m)
                        bakedMesh.SetSubMesh(m, descriptors[m], MeshUpdateFlags.DontValidateIndices);

                    bakedMesh.RecalculateBounds();
                    return;
                }

                vOffset += vCount;
                tOffset += tCount;
            }

            if (gpu)
            {
                dynamicVertexData.Dispose();
            }
        }
    }
}
