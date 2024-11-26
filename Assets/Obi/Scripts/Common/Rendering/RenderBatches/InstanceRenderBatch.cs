using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Obi
{

    [StructLayout(LayoutKind.Sequential)]
    public struct ChunkData
    {
        public int rendererIndex;
        public int offset; // index of the first element for each chunk.

        public ChunkData(int rendererIndex, int offset)
        {
            this.rendererIndex = rendererIndex;
            this.offset = offset;
        }
    }

    public class InstancedRenderBatch : IRenderBatch
    {
        private RenderBatchParams renderBatchParams;
        public RenderParams renderParams { get; private set; }

        public Mesh mesh;
        public Material material;

        public int firstRenderer;
        public int rendererCount;

        public int firstInstance;
        public int instanceCount;

        public GraphicsBuffer argsBuffer; 

        public InstancedRenderBatch(int rendererIndex, Mesh mesh, Material material, RenderBatchParams renderBatchParams)
        {
            this.renderBatchParams = renderBatchParams;
            this.firstRenderer = rendererIndex;
            this.rendererCount = 1;
            this.mesh = mesh;
            this.material = material;
            this.firstInstance = 0;
            this.instanceCount = 0;
        }

        public void Initialize(bool gpu = false)
        {
            renderParams = renderBatchParams.ToRenderParams();

            if (gpu)
                argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
        }

        public void Dispose()
        {
            argsBuffer?.Dispose();
            argsBuffer = null;
        }

        public bool TryMergeWith(IRenderBatch other)
        {
            var ibatch = other as InstancedRenderBatch;
            if (ibatch != null)
            {
                if (CompareTo(ibatch) == 0 &&
                    instanceCount + ibatch.instanceCount < Constants.maxInstancesPerBatch)
                {
                    rendererCount += ibatch.rendererCount;
                    instanceCount += ibatch.instanceCount;
                    return true;
                }
            }
            return false;
        }

        public int CompareTo(IRenderBatch other)
        {
            var ibatch = other as InstancedRenderBatch;

            int idA = material != null ? material.GetInstanceID() : 0;            int idB = (ibatch != null && ibatch.material != null) ? ibatch.material.GetInstanceID() : 0;

            int compareMat = idA.CompareTo(idB);
            if (compareMat == 0)
            {
                idA = mesh != null ? mesh.GetInstanceID() : 0;
                idB = (ibatch != null && ibatch.mesh != null) ? ibatch.mesh.GetInstanceID() : 0;
                compareMat = idA.CompareTo(idB);

                if (compareMat == 0)
                    return renderBatchParams.CompareTo(ibatch.renderBatchParams);
            }

            return compareMat;
        }

        public void BakeMesh<T>(RendererSet<T> renderers, T renderer, ObiNativeList<ChunkData> chunkData,
                             ObiNativeList<Matrix4x4> instanceTransforms,
                             Matrix4x4 transform,
                             ref Mesh bakedMesh, bool transformVertices = false) where T:ObiRenderer<T>
        {
             
            // if the data is not available in the CPU (such as when the batch is intended for GPU use), read it back:
            bool gpu = argsBuffer != null && argsBuffer.IsValid();
            if (gpu)
            {
                instanceTransforms.Readback(false);
            }

            List<CombineInstance> combineInstances = new List<CombineInstance>();

            bakedMesh.Clear();

            for (int i = 0; i < chunkData.count; ++i)
            {
                // if this chunk's renderer is the renderer we are interested in,
                // append its instances to the mesh.
                if (renderers[chunkData[i].rendererIndex].Equals(renderer))
                {
                    int firstIndex = i > 0 ? chunkData[i - 1].offset : 0;
                    int elementCount = chunkData[i].offset - firstIndex;

                    for (int m = 0; m < elementCount; ++m)
                    {
                        combineInstances.Add(new CombineInstance
                        {
                            mesh = mesh,
                            transform = transformVertices ? transform * instanceTransforms[firstIndex + m] : instanceTransforms[firstIndex + m]
                        });
                    }
                }
            }

            bakedMesh.CombineMeshes(combineInstances.ToArray(), true, true, false);
            bakedMesh.RecalculateBounds();

        }
    }
}
