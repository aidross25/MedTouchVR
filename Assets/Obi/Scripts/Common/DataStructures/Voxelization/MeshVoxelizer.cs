
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Obi
{

    /**
     * Helper class that voxelizes a mesh.
     */
    [Serializable]
    public class MeshVoxelizer
    {
        [Flags]
        public enum Voxel
        {
            Empty = 0,
            Inside = 1 << 0,
            Boundary = 1 << 1,
            Outside = 1 << 2,
        }

        public readonly static Vector3Int[] fullNeighborhood =
        {
            // face neighbors: 
            new Vector3Int(-1,0,0),
            new Vector3Int(1,0,0),
            new Vector3Int(0,-1,0),
            new Vector3Int(0,1,0),
            new Vector3Int(0,0,-1),
            new Vector3Int(0,0,1),

            // edge neighbors:
            new Vector3Int(-1,-1,0),
            new Vector3Int(-1,0,-1),
            new Vector3Int(-1,0,1),
            new Vector3Int(-1,1,0),
            new Vector3Int(0,-1,-1),
            new Vector3Int(0,-1,1),
            new Vector3Int(0,1,-1),
            new Vector3Int(0,1,1),
            new Vector3Int(1,-1,0),
            new Vector3Int(1,0,-1),
            new Vector3Int(1,0,1),
            new Vector3Int(1,1,0),

            // vertex neighbors:
            new Vector3Int(-1,-1,-1),
            new Vector3Int(-1,-1,1),
            new Vector3Int(-1,1,-1),
            new Vector3Int(-1,1,1),
            new Vector3Int(1,-1,-1),
            new Vector3Int(1,-1,1),
            new Vector3Int(1,1,-1),
            new Vector3Int(1,1,1)
        };

        public readonly static Vector3Int[] edgefaceNeighborhood =
        {
            new Vector3Int(-1,-1,0),
            new Vector3Int(-1,0,-1),
            new Vector3Int(-1,0,0),
            new Vector3Int(-1,0,1),
            new Vector3Int(-1,1,0),
            new Vector3Int(0,-1,-1),
            new Vector3Int(0,-1,0),
            new Vector3Int(0,-1,1),
            new Vector3Int(0,0,-1),
            new Vector3Int(0,0,1),
            new Vector3Int(0,1,-1),
            new Vector3Int(0,1,0),
            new Vector3Int(0,1,1),
            new Vector3Int(1,-1,0),
            new Vector3Int(1,0,-1),
            new Vector3Int(1,0,0),
            new Vector3Int(1,0,1),
            new Vector3Int(1,1,0)
        };

        public readonly static Vector3Int[] faceNeighborhood =
        {
            new Vector3Int(-1,0,0),
            new Vector3Int(1,0,0),
            new Vector3Int(0,-1,0),
            new Vector3Int(0,1,0),
            new Vector3Int(0,0,-1),
            new Vector3Int(0,0,1)
        };

        public readonly static Vector3Int[] edgeNeighborhood =
        {
            new Vector3Int(-1,-1,0),
            new Vector3Int(-1,0,-1),
            new Vector3Int(-1,0,1),
            new Vector3Int(-1,1,0),
            new Vector3Int(0,-1,-1),
            new Vector3Int(0,-1,1),
            new Vector3Int(0,1,-1),
            new Vector3Int(0,1,1),
            new Vector3Int(1,-1,0),
            new Vector3Int(1,0,-1),
            new Vector3Int(1,0,1),
            new Vector3Int(1,1,0)
        };

        public readonly static Vector3Int[] vertexNeighborhood =
        {
            new Vector3Int(-1,-1,-1),
            new Vector3Int(-1,-1,1),
            new Vector3Int(-1,1,-1),
            new Vector3Int(-1,1,1),
            new Vector3Int(1,-1,-1),
            new Vector3Int(1,-1,1),
            new Vector3Int(1,1,-1),
            new Vector3Int(1,1,1)
        };

        [NonSerialized] public Mesh input;

        [HideInInspector][SerializeField] private Voxel[] voxels;
        public float voxelSize;
        public Vector3Int resolution;

        private List<int>[] triangleIndices; // temporary structure to hold triangles overlapping each voxel.
        private Vector3Int origin;

        public Vector3Int Origin
        {
            get { return origin; }
        }

        public int voxelCount
        {
            get { return resolution.x * resolution.y * resolution.z; }
        }

        public MeshVoxelizer(Mesh input, float voxelSize)
        {
            this.input = input;
            this.voxelSize = voxelSize;
        }

        public Voxel this[int x, int y, int z]
        {
            get { return voxels[GetVoxelIndex(x, y, z)]; }
            set { voxels[GetVoxelIndex(x, y, z)] = value; }
        }

        public float GetDistanceToNeighbor(int i)
        {
            if (i > 17) return ObiUtils.sqrt3 * voxelSize;
            if (i > 5) return ObiUtils.sqrt2 * voxelSize;
            return voxelSize;
        }

        public int GetVoxelIndex(int x, int y, int z)
        {
            return x + resolution.x * (y + resolution.y * z);
        }

        public Vector3 GetVoxelCenter(in Vector3Int coords)
        {
            return new Vector3(Origin.x + coords.x + 0.5f,
                               Origin.y + coords.y + 0.5f,
                               Origin.z + coords.z + 0.5f) * voxelSize;
        }

        private Bounds GetTriangleBounds(in Vector3 v1, in Vector3 v2, in Vector3 v3)
        {
            Bounds b = new Bounds(v1, Vector3.zero);
            b.Encapsulate(v2);
            b.Encapsulate(v3);
            return b;
        }

        public List<int> GetTrianglesOverlappingVoxel(int voxelIndex)
        {
            if (voxelIndex >= 0 && voxelIndex < triangleIndices.Length)
                return triangleIndices[voxelIndex];
            return null;
        }

        public Vector3Int GetPointVoxel(in Vector3 point)
        {
            return new Vector3Int(Mathf.FloorToInt(point.x / voxelSize),
                                  Mathf.FloorToInt(point.y / voxelSize),
                                  Mathf.FloorToInt(point.z / voxelSize));
        }

        public bool VoxelExists(in Vector3Int coords)
        {
            return VoxelExists(coords.x, coords.y, coords.z);
        }

        public bool VoxelExists(int x, int y, int z)
        {
            return x >= 0 && y >= 0 && z >= 0 &&
                   x < resolution.x &&
                   y < resolution.y &&
                   z < resolution.z;
        }

        private void AppendOverlappingVoxels(in Bounds bounds, in Vector3 v1, in Vector3 v2, in Vector3 v3, int triangleIndex)
        {

            Vector3Int min = GetPointVoxel(bounds.min);
            Vector3Int max = GetPointVoxel(bounds.max);

            for (int x = min.x; x <= max.x; ++x)
                for (int y = min.y; y <= max.y; ++y)
                    for (int z = min.z; z <= max.z; ++z)
                    {
                        Bounds voxel = new Bounds(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * voxelSize, Vector3.one * voxelSize);

                        if (IsIntersecting(voxel, v1, v2, v3))
                        {
                            int index = GetVoxelIndex(x - origin.x, y - origin.y, z - origin.z);
                            voxels[index] = Voxel.Boundary;

                            if (triangleIndices != null)
                                triangleIndices[index].Add(triangleIndex);
                        }
                    }
        }


        public IEnumerator Voxelize(Matrix4x4 transform, bool generateTriangleIndices = false)
        {
            voxelSize = Mathf.Max(0.0001f, voxelSize);

            var xfBounds = input.bounds.Transform(transform);

            // Calculate min and max voxels, adding a 1-voxel margin.
            origin = GetPointVoxel(xfBounds.min) - new Vector3Int(1, 1, 1);
            Vector3Int max = GetPointVoxel(xfBounds.max) + new Vector3Int(1, 1, 1); 

            resolution = new Vector3Int(max.x - origin.x + 1, max.y - origin.y + 1, max.z - origin.z + 1);

            // Allocate voxels array, and initialize them to "inside" the mesh:
            voxels = new Voxel[resolution.x * resolution.y * resolution.z];

            for (int x = 0; x < resolution.x; ++x)
                for (int y = 0; y < resolution.y; ++y)
                    for (int z = 0; z < resolution.z; ++z)
                        this[x, y, z] = Voxel.Inside;

            // Allocate triangle lists:
            if (generateTriangleIndices)
            {
                triangleIndices = new List<int>[voxels.Length];
                for (int i = 0; i < triangleIndices.Length; ++i)
                    triangleIndices[i] = new List<int>(4);
            }
            else
                triangleIndices = null;

            // Get input triangles and vertices:
            int[] triIndices = input.triangles;
            Vector3[] vertices = input.vertices;

            // Generate surface voxels:
            for (int i = 0; i < triIndices.Length; i += 3)
            {
                Vector3 v1 = transform.MultiplyPoint3x4(vertices[triIndices[i]]);
                Vector3 v2 = transform.MultiplyPoint3x4(vertices[triIndices[i + 1]]);
                Vector3 v3 = transform.MultiplyPoint3x4(vertices[triIndices[i + 2]]);

                Bounds triBounds = GetTriangleBounds(v1, v2, v3);

                AppendOverlappingVoxels(triBounds, v1, v2, v3, i/3);

                if (i % 150 == 0)
                    yield return new CoroutineJob.ProgressInfo("Voxelizing mesh...", i / (float)triIndices.Length);
            }

            // Flood fill outside the mesh. This deals with multiple disjoint regions, and non-watertight models.
            var fillCoroutine = FloodFill();
            while (fillCoroutine.MoveNext())
                yield return fillCoroutine.Current;
        }

        // Ensures boundary is only one voxel thick.
        public void BoundaryThinning()
        {
            for (int x = 0; x < resolution.x; ++x)
                for (int y = 0; y < resolution.y; ++y)
                    for (int z = 0; z < resolution.z; ++z)
                        if (this[x, y, z] == Voxel.Boundary)
                            this[x, y, z] = Voxel.Inside;

            for (int x = 0; x < resolution.x; ++x)
                for (int y = 0; y < resolution.y; ++y)
                    for (int z = 0; z < resolution.z; ++z)
                    {
                        int sum = 0;
                        for (int j = 0; j < faceNeighborhood.Length; ++j)
                        {
                            var index = faceNeighborhood[j];
                            if (VoxelExists(index.x + x, index.y + y, index.z + z) && this[index.x + x, index.y + y, index.z + z] != Voxel.Outside)
                            {
                                sum++;
                            }
                        }

                        if (sum % faceNeighborhood.Length != 0 && this[x, y, z] == Voxel.Inside) 
                            this[x, y, z] = Voxel.Boundary;
                    }
        }

        public void CreateMesh(ref Mesh mesh, int smoothingIterations)
        {
            if (mesh == null)
                mesh = new Mesh();

            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.Clear();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> vertices2 = new List<Vector3>();
            List<int> tris = new List<int>();
            vertices.Clear();
            vertices2.Clear();
            tris.Clear();

            int[] vtxIndex = new int[voxelCount];
            for (int i = 0; i < vtxIndex.Length; ++i)
                vtxIndex[i] = -1;

            // create vertices:
            for (int x = 0; x < resolution.x; ++x)
                for (int y = 0; y < resolution.y; ++y)
                    for (int z = 0; z < resolution.z; ++z)
                        if (this[x, y, z] == Voxel.Boundary)
                        {
                            vtxIndex[GetVoxelIndex(x, y, z)] = vertices.Count;
                            var vtx = new Vector3(Origin.x + x + 0.5f, Origin.y + y + 0.5f, Origin.z + z + 0.5f) * voxelSize;
                            vertices.Add(vtx);
                            vertices2.Add(vtx);
                        }

            List<Vector3> inputVertices = vertices;
            List<Vector3> outputVertices = vertices2;
            for (int i = 0; i < smoothingIterations; ++i)
            {
                for (int x = 0; x < resolution.x; ++x)
                    for (int y = 0; y < resolution.y; ++y)
                        for (int z = 0; z < resolution.z; ++z)
                            if (this[x, y, z] == Voxel.Boundary)
                            {
                                Vector3 avg = Vector3.zero;

                                int count = 0;
                                for (int j = 0; j < faceNeighborhood.Length; ++j)
                                {
                                    var index = faceNeighborhood[j];
                                    if (VoxelExists(index.x + x, index.y + y, index.z + z) && this[index.x + x, index.y + y, index.z + z] == Voxel.Boundary)
                                    {
                                        avg += inputVertices[vtxIndex[GetVoxelIndex(index.x + x, index.y + y, index.z + z)]];
                                        count++;
                                    }
                                }

                                if (count > 0)
                                    outputVertices[vtxIndex[GetVoxelIndex(x, y, z)]] = avg / count;
                            }

                var aux = inputVertices;
                inputVertices = outputVertices;
                outputVertices = aux;
            }

            // triangulate
            for (int x = 0; x < resolution.x; ++x)
                for (int y = 0; y < resolution.y; ++y)
                    for (int z = 0; z < resolution.z; ++z)
                        if (this[x, y, z] == Voxel.Boundary)
                        {
                            int x0y0z0 = GetVoxelIndex(x, y, z);

                            int x1y0z0 = VoxelExists(x + 1, y, z) ? GetVoxelIndex(x+1, y, z) : -1;
                            int x1y1z0 = VoxelExists(x + 1, y + 1, z) ? GetVoxelIndex(x+1, y+1, z) : -1;
                            int x0y1z0 = VoxelExists(x, y + 1, z) ? GetVoxelIndex(x, y+1, z) : -1;

                            int x0y0z1 = VoxelExists(x, y, z + 1) ? GetVoxelIndex(x, y , z + 1) : -1;
                            int x0y1z1 = VoxelExists(x, y + 1, z + 1) ? GetVoxelIndex(x, y + 1, z + 1) : -1;
                            int x1y0z1 = VoxelExists(x + 1, y, z + 1) ? GetVoxelIndex(x+1, y, z + 1) : -1;

                            int x1y1z1 = VoxelExists(x + 1, y + 1, z + 1) ? GetVoxelIndex(x + 1, y + 1, z + 1) : -1;

                            // XY plane
                            if (x1y0z0 >= 0 && x1y1z0 >= 0 && x0y1z0 >= 0 &&
                                voxels[x1y0z0] == Voxel.Boundary &&
                                voxels[x1y1z0] == Voxel.Boundary &&
                                voxels[x0y1z0] == Voxel.Boundary)
                            {
                                if (x0y1z1 < 0 || voxels[x0y1z1] == Voxel.Outside ||
                                    x0y0z1 < 0 || voxels[x0y0z1] == Voxel.Outside ||
                                    x1y0z1 < 0 || voxels[x1y0z1] == Voxel.Outside ||
                                    x1y1z1 < 0 || voxels[x1y1z1] == Voxel.Outside)
                                {
                                    tris.Add(vtxIndex[x0y0z0]);
                                    tris.Add(vtxIndex[x1y0z0]);
                                    tris.Add(vtxIndex[x0y1z0]);

                                    tris.Add(vtxIndex[x0y1z0]);
                                    tris.Add(vtxIndex[x1y0z0]);
                                    tris.Add(vtxIndex[x1y1z0]);
                                }
                                else
                                {
                                    tris.Add(vtxIndex[x1y0z0]);
                                    tris.Add(vtxIndex[x0y0z0]);
                                    tris.Add(vtxIndex[x0y1z0]);

                                    tris.Add(vtxIndex[x1y0z0]);
                                    tris.Add(vtxIndex[x0y1z0]);
                                    tris.Add(vtxIndex[x1y1z0]);
                                }
                            }

                            // XZ plane
                            if (x1y0z0 >= 0 && x1y0z1 >= 0 && x0y0z1 >= 0 &&
                                voxels[x1y0z0] == Voxel.Boundary &&
                                voxels[x1y0z1] == Voxel.Boundary &&
                                voxels[x0y0z1] == Voxel.Boundary)
                            {
                                if (x0y1z0 < 0 || voxels[x0y1z0] == Voxel.Outside ||
                                    x0y1z1 < 0 || voxels[x0y1z1] == Voxel.Outside ||
                                    x1y1z0 < 0 || voxels[x1y1z0] == Voxel.Outside ||
                                    x1y1z1 < 0 || voxels[x1y1z1] == Voxel.Outside)
                                {
                                    tris.Add(vtxIndex[x1y0z0]);
                                    tris.Add(vtxIndex[x0y0z0]);
                                    tris.Add(vtxIndex[x0y0z1]);

                                    tris.Add(vtxIndex[x1y0z0]);
                                    tris.Add(vtxIndex[x0y0z1]);
                                    tris.Add(vtxIndex[x1y0z1]);
                                }
                                else
                                {
                                    tris.Add(vtxIndex[x0y0z0]);
                                    tris.Add(vtxIndex[x1y0z0]);
                                    tris.Add(vtxIndex[x0y0z1]);

                                    tris.Add(vtxIndex[x0y0z1]);
                                    tris.Add(vtxIndex[x1y0z0]);
                                    tris.Add(vtxIndex[x1y0z1]);
                                }
                            }

                            // XY plane
                            if (x0y0z1 >= 0 && x0y1z1 >= 0 && x0y1z0 >= 0 &&
                                voxels[x0y0z1] == Voxel.Boundary &&
                                voxels[x0y1z1] == Voxel.Boundary &&
                                voxels[x0y1z0] == Voxel.Boundary)
                            {
                                if (x1y0z0 < 0 || voxels[x1y0z0] == Voxel.Outside ||
                                    x1y0z1 < 0 || voxels[x1y0z1] == Voxel.Outside ||
                                    x1y1z0 < 0 || voxels[x1y1z0] == Voxel.Outside ||
                                    x1y1z1 < 0 || voxels[x1y1z1] == Voxel.Outside)
                                {
                                    tris.Add(vtxIndex[x0y0z1]);
                                    tris.Add(vtxIndex[x0y0z0]);
                                    tris.Add(vtxIndex[x0y1z0]);

                                    tris.Add(vtxIndex[x0y0z1]);
                                    tris.Add(vtxIndex[x0y1z0]);
                                    tris.Add(vtxIndex[x0y1z1]);
                                }
                                else
                                {
                                    tris.Add(vtxIndex[x0y0z0]);
                                    tris.Add(vtxIndex[x0y0z1]);
                                    tris.Add(vtxIndex[x0y1z0]);

                                    tris.Add(vtxIndex[x0y1z0]);
                                    tris.Add(vtxIndex[x0y0z1]);
                                    tris.Add(vtxIndex[x0y1z1]);
                                }
                            }
                        }

            mesh.SetVertices(outputVertices);
            mesh.SetIndices(tris, MeshTopology.Triangles, 0);
            mesh.RecalculateNormals();
        }

        private IEnumerator FloodFill()
        {
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(new Vector3Int(0, 0, 0));

            this[0, 0, 0] = Voxel.Outside;

            int i = 0;
            while (queue.Count > 0)
            {
                Vector3Int c = queue.Dequeue();
                Vector3Int v;

                if (c.x < resolution.x - 1 && this[c.x + 1, c.y, c.z] == Voxel.Inside)
                {
                    v = new Vector3Int(c.x + 1, c.y, c.z);
                    this[v.x, v.y, v.z] = Voxel.Outside;
                    queue.Enqueue(v);
                }
                if (c.x > 0 && this[c.x - 1, c.y, c.z] == Voxel.Inside)
                {
                    v = new Vector3Int(c.x - 1, c.y, c.z);
                    this[v.x, v.y, v.z] = Voxel.Outside;
                    queue.Enqueue(v);
                }
                if (c.y < resolution.y - 1 && this[c.x, c.y + 1, c.z] == Voxel.Inside)
                {
                    v = new Vector3Int(c.x, c.y + 1, c.z);
                    this[v.x, v.y, v.z] = Voxel.Outside;
                    queue.Enqueue(v);
                }
                if (c.y > 0 && this[c.x, c.y - 1, c.z] == Voxel.Inside )
                {
                    v = new Vector3Int(c.x, c.y - 1, c.z);
                    this[v.x, v.y, v.z] = Voxel.Outside;
                    queue.Enqueue(v);
                }

                if (c.z < resolution.z - 1 && this[c.x, c.y, c.z + 1] == Voxel.Inside)
                {
                    v = new Vector3Int(c.x, c.y, c.z + 1);
                    this[v.x, v.y, v.z] = Voxel.Outside;
                    queue.Enqueue(v);
                }
                if (c.z > 0 && this[c.x, c.y, c.z - 1] == Voxel.Inside)
                {
                    v = new Vector3Int(c.x, c.y, c.z - 1);
                    this[v.x, v.y, v.z] = Voxel.Outside;
                    queue.Enqueue(v);
                }

                if (++i % 150 == 0)
                    yield return new CoroutineJob.ProgressInfo("Filling mesh...", i / (float)voxels.Length);
            }
        }

        public static bool IsIntersecting(in Bounds box, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            v1 -= box.center;
            v2 -= box.center;
            v3 -= box.center;

            var ab = v2 - v1;
            var bc = v3 - v2;
            var ca = v1 - v3;

            //cross with (1, 0, 0)
            var a00 = new Vector3(0, -ab.z, ab.y);
            var a01 = new Vector3(0, -bc.z, bc.y);
            var a02 = new Vector3(0, -ca.z, ca.y);

            //cross with (0, 1, 0)
            var a10 = new Vector3(ab.z, 0, -ab.x);
            var a11 = new Vector3(bc.z, 0, -bc.x);
            var a12 = new Vector3(ca.z, 0, -ca.x);

            //cross with (0, 0, 1)
            var a20 = new Vector3(-ab.y, ab.x, 0);
            var a21 = new Vector3(-bc.y, bc.x, 0);
            var a22 = new Vector3(-ca.y, ca.x, 0);

            if (
                !TriangleAabbSATTest(v1, v2, v3, box.extents, a00) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, a01) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, a02) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, a10) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, a11) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, a12) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, a20) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, a21) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, a22) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, Vector3.right) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, Vector3.up) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, Vector3.forward) ||
                !TriangleAabbSATTest(v1, v2, v3, box.extents, Vector3.Cross(ab, bc))
            )
                return false;

            return true;
        }

        static bool TriangleAabbSATTest(in Vector3 v0, in Vector3 v1, in Vector3 v2, in Vector3 aabbExtents, in Vector3 axis)
        {
            float p0 = Vector3.Dot(v0, axis);
            float p1 = Vector3.Dot(v1, axis);
            float p2 = Vector3.Dot(v2, axis);

            float r = aabbExtents.x * Mathf.Abs(axis.x) +
                      aabbExtents.y * Mathf.Abs(axis.y) +
                      aabbExtents.z * Mathf.Abs(axis.z);

            float maxP = Mathf.Max(p0, Mathf.Max(p1, p2));
            float minP = Mathf.Min(p0, Mathf.Min(p1, p2));

            return !(Mathf.Max(-maxP, minP) > r);
        }
    }
}