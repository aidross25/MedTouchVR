using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Obi
{
    [Serializable]
    public class ObiMesh : ISerializationCallbackReceiver
    {
        public List<Cluster> clusters = new List<Cluster>();
        public List<Triangle> triangles = new List<Triangle>();
        public List<Vector3Int> edges = new List<Vector3Int>();

        private Vector3[] sourceVertices;
        private int[] sourceTriangles;

        private HashSet<Triangle> triangleSet = new HashSet<Triangle>();
        private TriangleComparer triangleComparer = new TriangleComparer();

        public int sourceVertexCount
        {
            get { return sourceVertices != null ? sourceVertices.Length : 0; }
        }

        public int sourceTriangleCount
        {
            get { return sourceTriangles != null ? sourceTriangles.Length/3 : 0; }
        }

        [Serializable]
        public class Triangle : ISerializationCallbackReceiver
        {
            // the three clusters that make up this triangle:
            private Cluster cluster0;
            private Cluster cluster1;
            private Cluster cluster2;

            // indices of the clusters (for serialization purposes only):
            public int[] clusterIndices = new int[3];

            // index of the triangle in the triangles array.
            public int index;

            // this triangle's normal and tangent vectors:
            public Vector3 normal;
            public Vector4 tangent;

            public Cluster this[int key]
            {
                get
                {
                    switch (key)
                    {
                        case 0: return cluster0;
                        case 1: return cluster1;
                        case 2: return cluster2;
                    }
                    return null;
                }
                set
                {
                    switch (key)
                    {
                        case 0: cluster0 = value; break;
                        case 1: cluster1 = value; break;
                        case 2: cluster2 = value; break;
                    }
                }

            }

            public Triangle(Cluster v1, Cluster v2, Cluster v3)
            {
                cluster0 = v1;
                cluster1 = v2;
                cluster2 = v3;
                UpdateTangentSpace();
            }

            private Vector3 CalculateNormal()
            {
                return Vector3.Cross(cluster1.centroid - cluster0.centroid, cluster2.centroid - cluster0.centroid).normalized;
            }

            public Vector3 CalculateCentroid()
            {
                return (cluster0.centroid + cluster1.centroid + cluster2.centroid) / 3.0f;
            }

            public int SharedVertexCount(Triangle other)
            {
                int sharedVerts = 0;
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        sharedVerts += (this[i] == other[j]) ? 1 : 0;

                return sharedVerts;
            }

            public float GetAngleDot(Triangle other)
            {
                return Vector3.Dot(CalculateNormal(), other.CalculateNormal());
            }

            public void GetAdjacentTriangles(HashSet<Triangle> adjacent)
            {
                adjacent.Clear();
                for (int i = 0; i < 3; ++i)
                {
                    foreach (var t in this[i].incidentTriangles)
                        if (t != this && t.SharedVertexCount(this) > 1)
                            adjacent.Add(t);
                }
            }

            public void Flip()
            {
                var aux = cluster2;
                cluster2 = cluster0;
                cluster0 = aux;
            }

            public bool IsDegenerate()
            {
                return cluster0 == cluster1 || cluster0 == cluster2 || cluster1 == cluster2;
            }

            public void UpdateTangentSpace()
            {
                var v1 = cluster0.centroid;
                var v2 = cluster1.centroid; 
                var v3 = cluster2.centroid;

                ObiUtils.BestTriangleAxisProjection(v1, v2, v3, out Vector2 w1, out Vector2 w2, out Vector2 w3);

                Vector3 m1 = v2 - v1;
                Vector3 m2 = v3 - v1;

                Vector2 s = w2 - w1;
                Vector2 t = w3 - w1;

                normal = Vector3.Cross(m1, m2);
                tangent = Vector4.zero;

                float area = s.x * t.y - t.x * s.y;

                if (Mathf.Abs(area) > ObiUtils.epsilon)
                {
                    float r = 1.0f / area;
                    tangent = new Vector4((t.y * m1.x - s.y * m2.x) * r,
                                          (t.y * m1.y - s.y * m2.y) * r,
                                          (t.y * m1.z - s.y * m2.z) * r, 0);
                }
            }

            public void OnBeforeSerialize()
            {
                for (int i = 0; i < 3; ++i)
                    clusterIndices[i] = this[i] != null ? this[i].index : -1;
            }

            public void OnAfterDeserialize() { }
        }

        public class TriangleComparer : IEqualityComparer<Triangle>
        {
            public bool Equals(Triangle one, Triangle two)
            {
                return one.SharedVertexCount(two) == 3;

            }

            public int GetHashCode(Triangle item)
            {
                // bitwise XOR is commutative, so order of vertices (winding) doesn't matter.
                return item[0].GetHashCode() ^ item[1].GetHashCode() ^ item[2].GetHashCode();
            }
        }

        [Serializable]
        public class Cluster : IEquatable<Cluster>
        {
            // list of incident triangles:
            [NonSerialized] public List<Triangle> incidentTriangles = new List<Triangle>();

            // indices of the input vertices represented by this cluster:
            public List<int> vertexIndices = new List<int>();

            // centroid (average of represented vertices positions), and orientation:
            [field: SerializeField] public Vector3 centroid { get; private set; } = Vector3.zero;
            [field: SerializeField] public Quaternion orientation { get; private set; } = Quaternion.identity;

            // index of the cluster in the cluster arrays:
            public int index;

            // whether it is part of a border edge:
            public bool isBorder;

            // best pair to collapse with, and the cost of performing the collapse:
            [NonSerialized] public Cluster pair;
            [NonSerialized] public float cost;

            public Cluster(Vector3[] vertexPositions, int vertexIndex)
            {
                index = vertexIndex;
                vertexIndices = new List<int> { vertexIndex };
                centroid = vertexPositions[vertexIndex];
            }

            public Cluster(Cluster other, int index)
            {
                incidentTriangles = new List<Triangle>(other.incidentTriangles);
                vertexIndices = new List<int>(other.vertexIndices);
                centroid = other.centroid;
                orientation = other.orientation;
                this.index = index;
                isBorder = other.isBorder; 
            }

            public void AddVertex(int index)
            {
                vertexIndices.Add(index);
            }

            public void UpdateCentroid(Vector3[] vertexPositions)
            {
                centroid = Vector3.zero;
                if (vertexIndices.Count > 0)
                {
                    foreach (int i in vertexIndices)
                        centroid += vertexPositions[i];

                    centroid /= vertexIndices.Count;
                }
            }

            public void UpdateOrientation()
            {
                Vector4 tangent = Vector4.zero;
                Vector3 normal = Vector3.zero;
                foreach (var t in incidentTriangles)
                {
                    normal += t.normal;
                    tangent += t.tangent;
                }

                float normalM = normal.magnitude;
                float tangentM = tangent.magnitude;

                if (normalM > ObiUtils.epsilon &&
                    tangentM > ObiUtils.epsilon)
                {
                    normal /= normalM;
                    tangent /= tangentM;
                    orientation = Quaternion.LookRotation(normal, tangent);
                }
                else orientation = Quaternion.identity;
            }

            private float GetCollapsePenalty(Cluster neighbor)
            {
                // folding a border cluster to a non-border cluster (or viceversa) is not allowed.
                if (neighbor.isBorder != isBorder)
                    return -1;

                // uncomment to keep original border edges intact
                //if (neighbor.isBorder || isBorder)
                    //return -1;

                float penalty = 0;
                if (neighbor.incidentTriangles.Count > 0)
                {
                    foreach (var nt in neighbor.incidentTriangles)
                    {
                        var v1 = nt[0] == neighbor ? centroid : nt[0].centroid;
                        var v2 = nt[1] == neighbor ? centroid : nt[1].centroid;
                        var v3 = nt[2] == neighbor ? centroid : nt[2].centroid;

                        float a = Vector3.Distance(v1, v2);
                        float b = Vector3.Distance(v2, v3);
                        float c = Vector3.Distance(v3, v1);

                        // cost increases with aspect ratio of resulting triangles:
                        penalty += Mathf.Max(0, ObiUtils.TriangleAspectRatio(a, b, c) - 1);

                        // calculate post-collapse normal and compare it to original normal:
                        var postCollapseNrm = ObiUtils.TriangleNormal(v1, v2, v3);
                        float dot = Vector3.Dot(nt.normal.normalized, postCollapseNrm);

                        // cost increases with normal change:
                        penalty += 1 - dot;

                        // flipping faces is not allowed:
                        if (dot < 0)
                            return -1;
                    }
                    penalty /= neighbor.incidentTriangles.Count;
                }
                return penalty;
            }

            public void UpdateCollapseCost(float decimation)
            {
                pair = null;
                cost = float.MaxValue;

                // Check connected neighbors:
                foreach (var t in incidentTriangles)
                {
                    for (int i = 0; i < 3; ++i)
                    {
                        var neighbor = t[i];

                        if (neighbor != this)
                        {
                            // get penalty to collapse this pair,
                            // a negative value means we must avoid collapsing:
                            float penalty = GetCollapsePenalty(neighbor);
                            if (penalty < 0)
                                continue;

                            float dist = Vector3.Distance(centroid, neighbor.centroid);
                            if (dist < decimation && dist + penalty < cost)
                            {
                                cost = dist + penalty;
                                pair = neighbor;
                            }
                        }
                    }
                }
            }

            public void UpdateWeldCost(RegularGrid<Cluster> grid)
            {
                pair = null;
                cost = float.MaxValue;

                foreach (var neighbor in grid.GetNeighborsEnumerator(this))
                {
                    float dist = Vector3.Distance(centroid, neighbor.centroid);
                    if (dist < cost)
                    {
                        cost = dist;
                        pair = neighbor;
                    }
                }
            }

            public void GetNeighbourVertices(HashSet<Cluster> neighbors)
            {
                neighbors.Clear();

                foreach (var t in incidentTriangles)
                {
                    for (int i = 0; i < 3; ++i)
                        if (t[i] != this)
                            neighbors.Add(t[i]);
                }
            }

            public void Collapse(Vector3[] vertexPositions)
            {
                // reconnect pair's incident triangles to us, absorbing those
                // that aren't also adjacent to us:
                foreach (var t in pair.incidentTriangles)
                {
                    for (int i = 0; i < 3; ++i)
                        if (t[i] == pair)
                            t[i] = this;

                    if (!incidentTriangles.Contains(t))
                        incidentTriangles.Add(t);
                }
                pair.incidentTriangles.Clear();

                // get rid of any resulting degenerate tris:
                DropDegenerateIncidentTriangles();

                // absorb pair's vertex indices and update centroid:
                vertexIndices.AddRange(pair.vertexIndices);
                UpdateCentroid(vertexPositions);
            }

            public void DropDegenerateIncidentTriangles()
            {
                for (int i = incidentTriangles.Count - 1; i >= 0; --i)
                {
                    if (incidentTriangles[i].IsDegenerate())
                        incidentTriangles.RemoveAt(i);
                }
            }


            public bool Equals(Cluster other)
            {
                return this == other;
            }
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            foreach (var t in triangles)
            {
                if (t.clusterIndices != null)
                {
                    for (int i = 0; i < 3; ++i)
                    {
                        int cIndex = t.clusterIndices[i];
                        if (cIndex < 0 || cIndex >= clusters.Count)
                            continue;

                        t[i] = clusters[cIndex];

                        if (t[i].incidentTriangles == null)
                            t[i].incidentTriangles = new List<Triangle>();

                        t[i].incidentTriangles.Add(t);
                    }
                }
            }
        }

        private static int CompareEdges(Vector3Int e1, Vector3Int e2)
        {
            int c = e1.x.CompareTo(e2.x);
            if (c == 0)
                return e1.y.CompareTo(e2.y);
            return c;
        }

        private Cluster GetCheapestPair()
        {
            float lowestCost = float.MaxValue;
            Cluster best = null;
            for (int j = 0; j < clusters.Count; ++j)
            {
                float cost = clusters[j].cost;
                if (cost < lowestCost)
                {
                    lowestCost = cost;
                    best = clusters[j];
                }
            }
            return best;
        }

        private IEnumerator DetectBorders(List<Vector3Int> sortedEdges)
        {
            int repeats = 0;
            for (int i = 0; i < sortedEdges.Count; ++i)
            {
                int next = i + 1;
                if (next == sortedEdges.Count || CompareEdges(sortedEdges[i], sortedEdges[next]) != 0)
                {
                    if (repeats == 0)
                    {
                        clusters[sortedEdges[i].x].isBorder = true;
                        clusters[sortedEdges[i].y].isBorder = true;
                    }
                    repeats = 0;
                }
                else
                    repeats++;

                if (i % 250 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiMesh: border detection...", i / (float)sortedEdges.Count);
            }
        }

        public void SwapClusters(int indexA, int indexB)
        {
            clusters.Swap(indexA, indexB);
            clusters[indexA].index = indexA;
            clusters[indexB].index = indexB;
        }

        private IEnumerator RemoveInvalidTriangles()
        {
            triangleSet = new HashSet<Triangle>(triangleComparer);

            for (int i = 0; i < triangles.Count; ++i)
            {
                var t = triangles[i];

                if (t.IsDegenerate() || !triangleSet.Add(t))
                {
                    // remove references to this triangle from incident vertices.
                    for (int j = 0; j < 3; ++j)
                        t[j].incidentTriangles.Remove(t);
                }

                if (i % 1000 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiMesh: removing invalid triangles...", i / (float)triangles.Count);
            }

            triangles = new List<Triangle>(triangleSet);
        }

        private void EnsureConsistentTriangleWinding(float maxDot = -0.2f)
        {
            if (maxDot <= -1)
                return;

            // insert all triangles in a set.
            triangleSet = new HashSet<Triangle>(triangles, triangleComparer);

            // create a queue to keep track of triangles to be checked,
            Queue<Triangle> pending = new Queue<Triangle>();
            HashSet<Triangle> adjacent = new HashSet<Triangle>();

            // while there's triangles in the set:
            while (triangleSet.Count > 0)
            {
                // put all unchecked triangles into a queue.
                // this ensures we eventually check all triangles even if they form
                // multiple disconnected islands.
                using (var iter = triangleSet.GetEnumerator())
                {
                    iter.MoveNext();
                    var start = iter.Current;
                    triangleSet.Remove(start);

                    pending.Enqueue(start);
                }

                while (pending.Count > 0)
                {
                    // take out one triangle and enqueue it to start checking adjacencies.
                    var tri = pending.Dequeue();
                    tri.GetAdjacentTriangles(adjacent);

                    // check all adjacent triangles
                    foreach (var adj in adjacent)
                    {
                        // if the triangle needs to be checked:
                        if (triangleSet.Remove(adj))
                        {
                            // flip the triangle if it has incorrect winding.
                            // we can't use edge information (if an edge is AB, the neighboring one should have BA)
                            // because we don't enforce 2-manifold surfaces. Instead, use relative normal orientation
                            // heuristic.
                            if (adj.GetAngleDot(tri) < maxDot)
                                adj.Flip();

                            // add it to the queue to check its neighbors.
                            pending.Enqueue(adj);
                        }
                    }
                }
            }
        }

        private IEnumerator RemoveIsolatedClusters()
        {
            for (int i = clusters.Count - 1; i >= 0; --i)
            {
                if (clusters[i].incidentTriangles.Count == 0)
                    clusters.RemoveAt(i);

                if (i % 500 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiMesh: removing isolated clusters...", i / (float)clusters.Count);
            }
        }

        private void ReindexClusters()
        {
            for (int i = 0; i < clusters.Count; ++i)
                clusters[i].index = i;
        }

        private void ReindexTriangles()
        {
            for (int i = 0; i < triangles.Count; ++i)
                triangles[i].index = i;
        }

        private IEnumerator UpdateClusterOrientations()
        {
            for (int i = 0; i < triangles.Count; ++i)
            {
                triangles[i].UpdateTangentSpace();

                if (i % 500 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiMesh: updating tangent space...", i / (float)triangles.Count);
            }

            for (int i = 0; i < clusters.Count; ++i)
            {
                clusters[i].UpdateOrientation();

                if (i % 500 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiMesh: updating cluster orientations...", i / (float)clusters.Count);
            }
        }

        private IEnumerator UpdateEdges()
        {
            edges.Clear();

            for (int i = 0; i < triangles.Count; ++i)
            {
                int t1 = triangles[i][0].index;
                int t2 = triangles[i][1].index;
                int t3 = triangles[i][2].index;

                edges.Add(new Vector3Int(Mathf.Min(t1, t2), Mathf.Max(t1, t2), i * 3));
                edges.Add(new Vector3Int(Mathf.Min(t2, t3), Mathf.Max(t2, t3), i * 3 + 1));
                edges.Add(new Vector3Int(Mathf.Min(t3, t1), Mathf.Max(t3, t1), i * 3 + 2));

                if (i % 500 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiMesh: creating edges..", i / (float)triangles.Count);
            }

            // Sort edges:
            edges.Sort(CompareEdges);

            // Detect borders (edges that only appear once in the sorted list):
            var b = DetectBorders(edges);
            while (b.MoveNext()) yield return b.Current;
        }

        private IEnumerator Cleanup(float maxNormalDot = -0.2f)
        {
            var c = RemoveInvalidTriangles();
            while (c.MoveNext()) yield return c.Current;

            c = RemoveIsolatedClusters();
            while (c.MoveNext()) yield return c.Current;

            ReindexTriangles();
            ReindexClusters();

            EnsureConsistentTriangleWinding(maxNormalDot);

            c = UpdateClusterOrientations();
            while (c.MoveNext()) yield return c.Current;

            c = UpdateEdges();
            while (c.MoveNext()) yield return c.Current;
        }

        public IEnumerator Build(Vector3[] vertexPositions, int[] tris)
        {
            this.sourceVertices = vertexPositions;
            this.sourceTriangles = tris;

            // Initialize clusters:
            clusters.Clear();
            for (int i = 0; i < sourceVertices.Length; ++i)
            {
                var c = new Cluster(sourceVertices, i);
                clusters.Add(c);

                if (i % 1000 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiMesh: initializing clusters...", i / (float)sourceVertices.Length);
            }

            // Initialize triangles:
            triangles.Clear();
            for (int i = 0; i < sourceTriangles.Length; i += 3)
            {
                int t1 = tris[i];
                int t2 = tris[i + 1];
                int t3 = tris[i + 2];

                var t = new Triangle(clusters[t1], clusters[t2], clusters[t3]);
                clusters[t1].incidentTriangles.Add(t);
                clusters[t2].incidentTriangles.Add(t);
                clusters[t3].incidentTriangles.Add(t);

                triangles.Add(t);

                if (i % 500 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiMesh: initializing triangles...", i / (float)sourceTriangles.Length);
            }

            var clean = Cleanup(-1);
            while (clean.MoveNext()) yield return clean.Current;
        }

        public IEnumerator Weld(float weldDistance, float normalWindingThreshold = -0.9f)
        {
            var grid = new RegularGrid<Cluster>(weldDistance, (Cluster c) => { return c.centroid; });

            // Add all clusters into the grid:
            foreach (var cluster in clusters)
                grid.AddElement(cluster);

            // Initialize welding cost of all clusters:
            foreach (var cluster in clusters)
            {
                cluster.isBorder = false;
                cluster.UpdateWeldCost(grid);
            }

            // Perform collapses while there's pairs to collapse.
            int i = 0;
            Cluster c;
            var neighbors = new HashSet<Cluster>();
            while ((c = GetCheapestPair()) != null)
            {
                // remove pair cluster:
                clusters.Remove(c.pair);
                grid.RemoveElement(c.pair);

                // remove cluster from grid:
                grid.RemoveElement(c);

                // collapse its pair onto it:
                c.Collapse(sourceVertices);

                // update our and our neighbor's costs:
                c.GetNeighbourVertices(neighbors);
                foreach (var n in neighbors)
                {
                    n.DropDegenerateIncidentTriangles();
                    n.UpdateWeldCost(grid);
                }
                c.UpdateWeldCost(grid);

                // re-add cluster to grid, position might have changed during collapse:
                grid.AddElement(c);

                ++i;
                if (i % 100 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiMesh: welding...", i / (float)sourceVertices.Length);
            }

            var clean = Cleanup(normalWindingThreshold);
            while (clean.MoveNext()) yield return clean.Current;
        }

        public IEnumerator Decimate(float decimation, float normalWindingThreshold = -0.9f)
        {
            if (decimation < ObiUtils.epsilon)
                yield break;

            // Initialize collapsing cost of all clusters:
            foreach (var cluster in clusters)
                cluster.UpdateCollapseCost(decimation);

            // Perform collapses while there's pairs to collapse.
            int i = 0;
            Cluster c;
            var neighbors = new HashSet<Cluster>();
            while ((c = GetCheapestPair()) != null)
            {
                // remove pair cluster:
                clusters.Remove(c.pair);

                // collapse its pair onto it
                c.Collapse(sourceVertices);

                // update our and our neighbor's costs:
                c.GetNeighbourVertices(neighbors);
                foreach (var n in neighbors)
                {
                    n.DropDegenerateIncidentTriangles();
                    n.UpdateCollapseCost(decimation);
                }
                c.UpdateCollapseCost(decimation);

                ++i;
                if (i % 100 == 0)
                    yield return new CoroutineJob.ProgressInfo("ObiMesh: decimating...", i / (float)sourceVertices.Length);
            }

            var clean = Cleanup(normalWindingThreshold);
            while (clean.MoveNext()) yield return clean.Current;
        }

        public int GetMaxClusterNeighborhoodSize()
        {
            int size = 0;
            foreach (var cluster in clusters)
                size = Mathf.Max(size, cluster.vertexIndices.Count);
            return size;
        }

        public float GetMaxDistanceFromCluster()
        {
            float maxDist = 0;
            foreach (var v in sourceVertices)
            {
                float closestCluster = float.MaxValue;
                foreach (var cluster in clusters)
                    closestCluster = Mathf.Min(closestCluster, Vector3.SqrMagnitude(cluster.centroid - v));

                maxDist = Mathf.Max(maxDist, closestCluster);
            }
            return Mathf.Sqrt(maxDist);
        }

        public List<Vector3Int> GetUniqueEdges()
        {
            List<Vector3Int> uniqueEdges = new List<Vector3Int>(edges);

            // make the list unique (linear time since it's presorted):
            int resultIndex = uniqueEdges.Unique((e1, e2) => { return e1.x == e2.x && e1.y == e2.y; });

            // remove excess edges at the end of the list:
            if (resultIndex < uniqueEdges.Count)
                uniqueEdges.RemoveRange(resultIndex, uniqueEdges.Count - resultIndex);

            return uniqueEdges;
        }

        public void SplitCluster(List<Triangle> incidentFaces, int clusterIndex)
        {
            // create a new cluster:
            var newCluster = new Cluster(clusters[clusterIndex],clusters.Count);
            newCluster.incidentTriangles = new List<Triangle>(incidentFaces);
            clusters.Add(newCluster);

            for (int j = 0; j < incidentFaces.Count; ++j)
            {
                // remove face from incidence list for the old cluster:
                clusters[clusterIndex].incidentTriangles.Remove(incidentFaces[j]);

                // update face to reference new vertex:
                for (int i = 0; i < 3; ++i)
                {
                    if (incidentFaces[j][i] == clusters[clusterIndex])
                        incidentFaces[j][i] = newCluster;
                }
            }
        }

        public int GetBorderClusterCount()
        {
            int borderCount = 0;
            foreach(var cluster in clusters)
                if (cluster.isBorder) borderCount++;
            return borderCount;
        }
    }
}
