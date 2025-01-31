using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

namespace Obi
{
    [CreateAssetMenu(fileName = "skinmap", menuName = "Obi/Skinmap", order = 123)]
    public class ObiSkinMap : ScriptableObject
    {
        [HideInInspector] public ObiInfluenceMap particlesOnVertices = new ObiInfluenceMap(); /**< for each vertex, stores particle influences.*/
        [HideInInspector] public ObiInfluenceMap bonesOnParticles = new ObiInfluenceMap();    /**< for each particle, stores bone influences (cloth only)*/

        [HideInInspector] public ObiNativeMatrix4x4List bindPoses = new ObiNativeMatrix4x4List(); /**< for cloth, stores particle bind poses. For softbodies, stores both bone and particle bind poses.*/

        [HideInInspector] public uint checksum;     /**< this skinmap's checksum, used to determine if the data it was generated from is no longer valid.*/

        private struct ParticleInfluence
        {
            public int index;
            public Vector3 position;
        }

        /// <summary>  
        /// Creates an influence map from particles to vertices: each vertex will be influenced by multiple particles.
        /// </summary>  
        /// <param name="mesh"> the mesh to get vertices from.</param>
        /// <param name="collection"> the particle collection.</param>
        /// <param name="verticesToWorld"> transform from mesh space to world space.</param>
        /// <param name="worldToCollection"> transform from world space to particle collection space.</param>
        /// <param name="radius"> influence radius.</param>
        /// <param name="falloff"> influence falloff.</param>
        /// <param name="maxInfluences"> maximum amount of particle influences per vertex.</param>
        /// 
        public void MapParticlesToVertices(Mesh mesh, IObiParticleCollection collection, Matrix4x4 verticesToWorld, Matrix4x4 worldToCollection, float radius = 0.25f, float falloff = 1, uint maxInfluences = 4, bool influencedByBones = false, float particleInfluence = 1, float[] particleInfuenceMap = null)
        {
            particlesOnVertices.Clear();
            bindPoses.Clear();

            if (mesh == null || collection == null)
                return;

            // guard against zero or negative radius:
            radius = Mathf.Max(radius, ObiUtils.epsilon);
            var verticesToCollection = worldToCollection * verticesToWorld;

            // get any pre-existing mesh bind poses:
            if (influencedByBones)
            {
                List<Matrix4x4> poses = new List<Matrix4x4>();
                mesh.GetBindposes(poses);
                bindPoses.AddRange(poses);
            }

            // store amount of pre-existing bones:
            int boneCount = bindPoses.count;

            // get mesh info:
            var boneWeights = mesh.GetAllBoneWeights();
            var bonesPerVertex = mesh.GetBonesPerVertex();
            var vertices = mesh.vertices;

            // append particle bind poses to the bone bind poses:
            for (int i = 0; i < collection.particleCount; ++i)
            {
                var restPos = collection.GetParticleRestPosition(i);
                var restRot = collection.GetParticleRestOrientation(i);

                Matrix4x4 collectionToParticle = Matrix4x4.Rotate(Quaternion.Inverse(restRot)) * Matrix4x4.Translate(-restPos);
                bindPoses.Add(collectionToParticle * worldToCollection * verticesToWorld);
            }

            // add particles to a grid for fast fixed-radius neighbor queries:
            var grid = new RegularGrid<ParticleInfluence>(radius, (ParticleInfluence c) => { return c.position; });
            for (int i = 0; i < collection.particleCount; ++i)
                grid.AddElement(new ParticleInfluence { index = i, position = collection.GetParticleRestPosition(i) });

            // add first influence offset:
            particlesOnVertices.influenceOffsets.Add(0);

            // calculate influences for each vertex in the mesh:
            int boneWeightIndex = 0;
            for (int i = 0; i < vertices.Length; ++i)
            {
                int offset = particlesOnVertices.influences.count;
                var vertex = verticesToCollection.MultiplyPoint3x4(vertices[i]);

                float particleWeight = particleInfuenceMap != null ? particleInfuenceMap[i] * particleInfluence : particleInfluence;

                // add existing bone influences:
                if (influencedByBones && i < bonesPerVertex.Length)
                {
                    for (int j = 0; j < bonesPerVertex[i]; ++j)
                    {
                        particlesOnVertices.influences.Add(new ObiInfluenceMap.Influence
                        {
                            index = boneWeights[boneWeightIndex].boneIndex,
                            weight = boneWeights[boneWeightIndex].weight * (1 - particleWeight)
                        });
                        boneWeightIndex++;
                    }
                }

                // find particles closer that the given radius and add their influences.
                // Offset indices by amount of bones, since particle influences always appear after bone influences.
                foreach (var c in grid.GetNeighborsEnumerator(vertex))
                {
                    float distance = Vector3.Distance(vertex, c.position);
                    float weight = Mathf.Pow(1 - distance / radius, falloff);

                    particlesOnVertices.influences.Add(new ObiInfluenceMap.Influence
                    {
                        index = boneCount + c.index,
                        weight = weight * particleWeight
                    });
                }

                int count = particlesOnVertices.influences.count - offset;

                // sort influences by weight:
                var slice = particlesOnVertices.influences.AsNativeArray<ObiInfluenceMap.Influence>().Slice(offset, count);
                var sorted = slice.OrderByDescending(x => x.weight).ToList();
                for (int k = 0; k < count; ++k)
                    particlesOnVertices.influences[offset + k] = sorted[k];

                // discard all but the first maxInfluences:
                count = Mathf.Min(count, (int)maxInfluences);
                int end = offset + count;
                particlesOnVertices.influences.RemoveRange(end, particlesOnVertices.influences.count - end);

                // add influence offset:
                int previous = particlesOnVertices.influenceOffsets[particlesOnVertices.influenceOffsets.count - 1];
                particlesOnVertices.influenceOffsets.Add(previous + count);

                // normalize weights:
                particlesOnVertices.NormalizeWeights(offset, count);
            }
        }

        public void MapBonesToParticles(Mesh mesh, ObiMesh particleMesh, Matrix4x4 verticesToWorld, Matrix4x4 worldToParticles, float falloff = 1, uint maxInfluences = 4)
        {
            bonesOnParticles.Clear();

            if (mesh == null || particleMesh == null)
                return;

            var bonesPerVertex = mesh.GetBonesPerVertex();
            var boneWeights = mesh.GetAllBoneWeights();

            // no bones at all.
            if (bonesPerVertex.Length == 0)
                return;

            Matrix4x4 verticesToCollection = worldToParticles * verticesToWorld;
            Vector3[] vertices = mesh.vertices;

            // calculate bone offset for each vertex:
            int[] boneOffsets = new int[bonesPerVertex.Length];
            for (int i = 1; i < boneOffsets.Length; ++i)
                boneOffsets[i] = boneOffsets[i - 1] + bonesPerVertex[i - 1];

            // add first influence offset:
            bonesOnParticles.influenceOffsets.Add(0);

            // for each cluster in the mesh:
            for (int i = 0; i < particleMesh.clusters.Count; ++i)
            {
                // find maximum distance to the input vertices represented by it:
                float maxDistance = 0;
                foreach (var vIndex in particleMesh.clusters[i].vertexIndices)
                {
                    float distance = Vector3.Distance(particleMesh.clusters[i].centroid, verticesToCollection.MultiplyPoint3x4(vertices[vIndex]));
                    maxDistance = Mathf.Max(maxDistance, distance);
                }

                int offset = bonesOnParticles.influences.count;

                // iterate over all vertices in this cluster:
                foreach (var vIndex in particleMesh.clusters[i].vertexIndices)
                {
                    // calculate vertex weight based on distance to cluster:
                    float distance = Vector3.Distance(particleMesh.clusters[i].centroid, verticesToCollection.MultiplyPoint3x4(vertices[vIndex]));

                    float weight = 1;

                    // add 0.001 to the distance, to ensure vertices with a single nearby bone don't get zero weight (1 - 1)
                    if (maxDistance > ObiUtils.epsilon)
                        weight = Mathf.Pow(Mathf.Clamp01(1.001f - distance / maxDistance), falloff);

                    // iterate over all bones for this vertex:
                    for (int b = boneOffsets[vIndex]; b < boneOffsets[vIndex] + bonesPerVertex[vIndex]; ++b)
                    {
                        int bestInfluence = -1;

                        // find existing bone influence, if any:
                        for (int j = offset; j < bonesOnParticles.influences.count; ++j)
                        {
                            if (bonesOnParticles.influences[j].index == boneWeights[b].boneIndex)
                            {
                                bestInfluence = j;
                                break;
                            }
                        }

                        // if there's an existing influence by this bone, add to its weight:
                        if (bestInfluence >= 0)
                        {
                            var influence = bonesOnParticles.influences[bestInfluence];
                            influence.weight += boneWeights[b].weight * weight;
                            bonesOnParticles.influences[bestInfluence] = influence;
                        }
                        else
                        // if no existing influence, create a new one:
                        {
                            bonesOnParticles.influences.Add(new ObiInfluenceMap.Influence(boneWeights[b].boneIndex, boneWeights[b].weight * weight));
                        }
                    }
                }

                int count = bonesOnParticles.influences.count - offset;

                // sort influences by weight:
                var slice = bonesOnParticles.influences.AsNativeArray<ObiInfluenceMap.Influence>().Slice(offset, count);
                var sorted = slice.OrderByDescending(x => x.weight).ToList();
                for (int k = 0; k < count; ++k)
                    bonesOnParticles.influences[offset + k] = sorted[k];

                // discard all but the first maxInfluences:
                count = Mathf.Min(count, (int)maxInfluences);
                int end = offset + count;
                bonesOnParticles.influences.RemoveRange(end, bonesOnParticles.influences.count - end);

                // add influence offset:
                int previous = bonesOnParticles.influenceOffsets[bonesOnParticles.influenceOffsets.count - 1];
                bonesOnParticles.influenceOffsets.Add(previous + count);

                // normalize weights:
                bonesOnParticles.NormalizeWeights(offset, count);
            }
        }
    }
}
