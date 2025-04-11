
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace Obi
{

    public class ProceduralRenderBatch<T> : IRenderBatch where T : struct
    {
        private RenderBatchParams renderBatchParams;
        public RenderParams renderParams { get; private set; }

        public Material material;
        public Mesh mesh;

        public int firstRenderer;
        public int rendererCount;

        public int firstParticle;

        public NativeArray<T> vertices;
        public NativeArray<int> triangles;

        public GraphicsBuffer gpuVertexBuffer;
        public GraphicsBuffer gpuIndexBuffer;

        public int vertexCount;
        public int triangleCount;

        public ProceduralRenderBatch(int rendererIndex, Material material, RenderBatchParams param)
        {
            this.renderBatchParams = param;

            this.material = material;
            this.firstRenderer = rendererIndex;
            this.firstParticle = 0;
            this.rendererCount = 1;
            this.vertexCount = 0;
            this.triangleCount = 0;
        }

        public void Initialize(VertexAttributeDescriptor[] layout, bool gpu = false)
        {
            var rp = renderBatchParams.ToRenderParams();
            rp.material = material;
            renderParams = rp;

            mesh = new Mesh();

            mesh.SetVertexBufferParams(vertexCount, layout);
            mesh.SetIndexBufferParams(triangleCount * 3, IndexFormat.UInt32);

            vertices = new NativeArray<T>(vertexCount, Allocator.Persistent);
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            triangles = new NativeArray<int>(triangleCount * 3, Allocator.Persistent);
            mesh.SetIndexBufferData(triangles, 0, 0, triangles.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            mesh.subMeshCount = 1;
            SubMeshDescriptor subMeshDescriptor = new SubMeshDescriptor();
            subMeshDescriptor.indexCount = triangleCount * 3;
            mesh.SetSubMesh(0, subMeshDescriptor, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            if (gpu)
            {
                vertices.Dispose();
                triangles.Dispose();

                mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;
                mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;

                // particles with no vertices will have no vertex buffer, and Unity will throw an exception.
                if (mesh.vertexCount > 0)
                {
                    gpuVertexBuffer ??= mesh.GetVertexBuffer(0);
                    gpuIndexBuffer ??= mesh.GetIndexBuffer();
                }
            }
        }

        public void Dispose()
        {
            gpuVertexBuffer?.Dispose();
            gpuIndexBuffer?.Dispose();

            gpuVertexBuffer = null;
            gpuIndexBuffer = null;

            if (vertices.IsCreated)
                vertices.Dispose();
            if (triangles.IsCreated)
                triangles.Dispose();

            GameObject.DestroyImmediate(mesh);
        }

        public bool TryMergeWith(IRenderBatch other)
        {
            var pbatch = other as ProceduralRenderBatch<T>;
            if (pbatch != null)
            {
                if (CompareTo(pbatch) == 0 &&
                    vertexCount + pbatch.vertexCount < Constants.maxVertsPerMesh)
                {
                    rendererCount += pbatch.rendererCount;
                    triangleCount += pbatch.triangleCount;
                    vertexCount += pbatch.vertexCount;
                    return true;
                }
            }
            return false;
        }

        public int CompareTo(IRenderBatch other)
        {
            var pbatch = other as ProceduralRenderBatch<T>;            int idA = material != null ? material.GetInstanceID() : 0;            int idB = (pbatch != null && pbatch.material != null) ? pbatch.material.GetInstanceID() : 0;            int result = idA.CompareTo(idB);            if (result == 0)                return renderBatchParams.CompareTo(pbatch.renderBatchParams);            return result;
        }

        public void BakeMesh(int vertexOffset, int vertexCount, int triangleOffset, int triangleCount,
                             Matrix4x4 transform,
                             ref Mesh bakedMesh, bool transformVertices = false)
        {

            // if the data is not available in the CPU (such as when the batch is intended for GPU use), read it back:
            bool gpu = !vertices.IsCreated;
            if (gpu)
            {
                vertices = new NativeArray<T>(this.vertexCount, Allocator.Persistent);
                triangles = new NativeArray<int>(this.triangleCount * 3, Allocator.Persistent);
                AsyncGPUReadback.RequestIntoNativeArray(ref vertices, gpuVertexBuffer, this.vertexCount * UnsafeUtility.SizeOf<T>(), 0).WaitForCompletion();
                AsyncGPUReadback.RequestIntoNativeArray(ref triangles, gpuIndexBuffer, this.triangleCount * 3 * 4, 0).WaitForCompletion();
            }

            bakedMesh.Clear();

            bakedMesh.SetVertexBufferParams(vertexCount, mesh.GetVertexAttributes());
            bakedMesh.SetVertexBufferData(vertices, vertexOffset, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            // transform vertices from solver space to actor space:
            if (transformVertices)
            {
                var solver2Actor = transform; 
                var verts = bakedMesh.vertices;
                for (int v = 0; v < verts.Length; ++v)
                    verts[v] = solver2Actor.MultiplyPoint3x4(verts[v]);
                bakedMesh.vertices = verts;
            }

            ObiNativeList<int> indices = new ObiNativeList<int>(triangleCount * 3);

            // offset indices:
            for (int i = 0; i < triangleCount * 3; ++i)
            {
                int index = triangles[triangleOffset * 3 + i] - vertexOffset;

                // clamp indices to zero, since decimated ropes have unused triangles
                // that reference vertex 0. Subtracting the vertex offset from these results in a negative index.
                indices.Add(Mathf.Max(0,index));
            }

            bakedMesh.SetIndexBufferParams(triangleCount * 3, IndexFormat.UInt32);
            bakedMesh.SetIndexBufferData(indices.AsNativeArray<int>(), 0, 0, triangleCount * 3, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

            bakedMesh.subMeshCount = 1;
            SubMeshDescriptor subMeshDescriptor = new SubMeshDescriptor();
            subMeshDescriptor.indexCount = triangleCount * 3; // mesh triangle count.
            bakedMesh.SetSubMesh(0, subMeshDescriptor, MeshUpdateFlags.DontValidateIndices);

            if (gpu)
            {
                if (vertices.IsCreated)
                    vertices.Dispose();
                if (triangles.IsCreated)
                    triangles.Dispose();
            }

            bakedMesh.RecalculateBounds();
            return;

        }
    }

}