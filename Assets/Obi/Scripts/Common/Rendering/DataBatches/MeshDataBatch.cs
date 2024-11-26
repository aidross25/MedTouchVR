using UnityEngine;
using Unity.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Obi
{
    public interface IMeshDataProvider
    {
        Mesh sourceMesh { get; }
        uint meshInstances { get; }

        int vertexCount { get; }
        int triangleCount { get; }

        void GetVertices(List<Vector3> vertices);
        void GetNormals(List<Vector3> normals);
        void GetTangents(List<Vector4> tangents);
        void GetColors(List<Color> colors);
        void GetUVs(int channel, List<Vector2> uvs);

        void GetTriangles(List<int> triangles);
    }

    public class MeshDataBatch
    {
        public struct MeshData
        {
            public int firstVertex;
            public int vertexCount;

            public int firstTriangle;
            public int triangleCount;
        }

        private Dictionary<Mesh, int> meshToIndex;

        // per mesh data:
        public ObiNativeList<MeshData> meshData;

        public ObiNativeList<Vector3> restPositions;
        public ObiNativeList<Vector3> restNormals;
        public ObiNativeList<Vector4> restTangents;
        public ObiNativeList<Color> restColors;
        public ObiNativeList<Vector2> uv;
        public ObiNativeList<Vector2> uv2;
        public ObiNativeList<Vector2> uv3;
        public ObiNativeList<Vector2> uv4;
        public ObiNativeList<int> triangles;

        private List<Vector3> tempVertices;
        private List<Vector3> tempNormals;
        private List<Vector4> tempTangents;
        private List<Color> tempColors;
        private List<Vector2> tempUV;
        private List<Vector2> tempUV2;
        private List<Vector2> tempUV3;
        private List<Vector2> tempUV4;
        private List<int> tempTriangles;

        public int Count { get { return meshData.count; } }

        public MeshDataBatch()
        {
            meshToIndex = new Dictionary<Mesh, int>();
            meshData = new ObiNativeList<MeshData>();

            InitializeTempData();
            InitializeDynamicData();
            InitializeStaticData();
        }

        public void InitializeTempData()
        {
            tempVertices = new List<Vector3>();
            tempNormals = new List<Vector3>();
            tempTangents = new List<Vector4>();
            tempColors = new List<Color>();

            tempUV = new List<Vector2>();
            tempUV2 = new List<Vector2>();
            tempUV3 = new List<Vector2>();
            tempUV4 = new List<Vector2>();
            tempTriangles = new List<int>();
        }

        public void InitializeDynamicData()
        {
            if (restPositions == null)
                restPositions = new ObiNativeList<Vector3>();

            if (restNormals == null)
                restNormals = new ObiNativeList<Vector3>();

            if (restTangents == null)
                restTangents = new ObiNativeList<Vector4>();

            if (restColors == null)
                restColors = new ObiNativeList<Color>();
        }

        public void InitializeStaticData()
        {
            if (uv == null)
                uv = new ObiNativeList<Vector2>();

            if (uv2 == null)
                uv2 = new ObiNativeList<Vector2>();

            if (uv3 == null)
                uv3 = new ObiNativeList<Vector2>();

            if (uv4 == null)
                uv4 = new ObiNativeList<Vector2>();

            if (triangles == null)
                triangles = new ObiNativeList<int>();
        }

        public void Dispose()
        {
            if (meshData != null) meshData.Dispose();
            DisposeOfTempData();
            DisposeOfDynamicData();
            DisposeOfStaticData();
        }

        public void DisposeOfTempData()
        {
            tempVertices = null;
            tempNormals = null;
            tempTangents = null;
            tempColors = null;

            tempUV = null;
            tempUV2 = null;
            tempUV3 = null;
            tempUV4 = null;
            tempTriangles = null;
        }

        public void DisposeOfDynamicData()
        {
            if (restPositions != null) restPositions.Dispose(); restPositions = null;
            if (restNormals != null)   restNormals.Dispose();   restNormals = null;
            if (restTangents != null)  restTangents.Dispose();  restTangents = null;
            if (restColors != null)    restColors.Dispose();    restColors = null;
        }

        public void DisposeOfStaticData()
        {
            if (uv != null)  uv.Dispose(); uv = null;
            if (uv2 != null) uv2.Dispose(); uv2 = null;
            if (uv3 != null) uv3.Dispose(); uv3 = null;
            if (uv4 != null) uv4.Dispose(); uv4 = null;
            if (triangles != null) triangles.Dispose(); triangles = null;
        }

        public void Clear()
        {
            if (meshToIndex != null) meshToIndex.Clear();
            if (meshData != null) meshData.Clear();

            if (restPositions != null) restPositions.Clear();
            if (restNormals != null) restNormals.Clear();
            if (restTangents != null) restTangents.Clear();
            if (restColors != null) restColors.Clear();
            if (uv != null) uv.Clear();
            if (uv2 != null) uv2.Clear();
            if (uv3 != null) uv3.Clear();
            if (uv4 != null) uv4.Clear();
            if (triangles != null) triangles.Clear();
        }

        public int AddMesh(IMeshDataProvider meshProvider)
        {
            if (!meshToIndex.TryGetValue(meshProvider.sourceMesh, out int index))
            {
                index = meshData.count;
                meshToIndex[meshProvider.sourceMesh] = index;

                meshProvider.GetVertices(tempVertices);
                meshProvider.GetNormals(tempNormals);
                meshProvider.GetTangents(tempTangents);
                meshProvider.GetColors(tempColors);

                meshProvider.GetUVs(0, tempUV);
                meshProvider.GetUVs(1, tempUV2);
                meshProvider.GetUVs(2, tempUV3);
                meshProvider.GetUVs(3, tempUV4);

                meshProvider.GetTriangles(tempTriangles);

                if (tempTangents.Count == 0)
                    tempTangents.AddRange(Enumerable.Repeat(Vector4.zero, tempVertices.Count));
               
                if (tempColors.Count == 0)
                    tempColors.AddRange(Enumerable.Repeat(Color.white, tempVertices.Count));
                
                if (tempUV.Count == 0)
                    tempUV.AddRange(Enumerable.Repeat(Vector2.zero, tempVertices.Count));

                if (tempUV2.Count == 0)
                    tempUV2.AddRange(Enumerable.Repeat(Vector2.zero, tempVertices.Count));
                
                if (tempUV3.Count == 0)
                    tempUV3.AddRange(Enumerable.Repeat(Vector2.zero, tempVertices.Count));
                
                if (tempUV4.Count == 0)
                    tempUV4.AddRange(Enumerable.Repeat(Vector2.zero, tempVertices.Count));

                meshData.Add(new MeshData
                {
                    firstVertex = restPositions.count,
                    vertexCount = tempVertices.Count,

                    firstTriangle = triangles.count,
                    triangleCount = tempTriangles.Count
                });

                restPositions.AddRange(tempVertices);
                restNormals.AddRange(tempNormals);
                restTangents.AddRange(tempTangents);
                restColors.AddRange(tempColors);
                uv.AddRange(tempUV);
                uv2.AddRange(tempUV2);
                uv3.AddRange(tempUV3);
                uv4.AddRange(tempUV4);
                triangles.AddRange(tempTriangles);
            }
            return index;
        }

        public void PrepareForCompute()
        {
            meshData.AsComputeBuffer<MeshData>();
            restPositions.AsComputeBuffer<Vector3>();
            restNormals.AsComputeBuffer<Vector3>();
            restTangents.AsComputeBuffer<Vector4>();
            restColors.AsComputeBuffer<Color>();
        }

        public int GetVertexCount(int meshIndex)
        {
            return meshData[meshIndex].vertexCount;
        }

        public int GetTriangleCount(int meshIndex)
        {
            return meshData[meshIndex].triangleCount;
        }

        public NativeSlice<Vector3> GetVertices(int meshIndex)
        {
            int start = meshData[meshIndex].firstVertex;
            int count = meshData[meshIndex].vertexCount;
            return restPositions.AsNativeArray<Vector3>().Slice(start,count);
        }

        public NativeSlice<Vector3> GetNormals(int meshIndex)
        {
            int start = meshData[meshIndex].firstVertex;
            int count = meshData[meshIndex].vertexCount;
            return restNormals.AsNativeArray<Vector3>().Slice(start, count);
        }

        public NativeSlice<Vector4> GetTangents(int meshIndex)
        {
            int start = meshData[meshIndex].firstVertex;
            int count = meshData[meshIndex].vertexCount;
            return restTangents.AsNativeArray<Vector4>().Slice(start, count);
        }

        public NativeSlice<Color> GetColors(int meshIndex)
        {
            int start = meshData[meshIndex].firstVertex;
            int count = meshData[meshIndex].vertexCount;
            return restColors.AsNativeArray<Color>().Slice(start, count);
        }

        public NativeSlice<Vector2> GetUV(int meshIndex)
        {
            int start = meshData[meshIndex].firstVertex;
            int count = meshData[meshIndex].vertexCount;
            return uv.AsNativeArray<Vector2>().Slice(start, count);
        }

        public NativeSlice<Vector2> GetUV2(int meshIndex)
        {
            int start = meshData[meshIndex].firstVertex;
            int count = meshData[meshIndex].vertexCount;
            return uv2.AsNativeArray<Vector2>().Slice(start, count);
        }

        public NativeSlice<Vector2> GetUV3(int meshIndex)
        {
            int start = meshData[meshIndex].firstVertex;
            int count = meshData[meshIndex].vertexCount;
            return uv3.AsNativeArray<Vector2>().Slice(start, count);
        }

        public NativeSlice<Vector2> GetUV4(int meshIndex)
        {
            int start = meshData[meshIndex].firstVertex;
            int count = meshData[meshIndex].vertexCount;
            return uv4.AsNativeArray<Vector2>().Slice(start, count);
        }

        public NativeSlice<int> GetTriangles(int meshIndex)
        {
            int start = meshData[meshIndex].firstTriangle;
            int count = meshData[meshIndex].triangleCount;
            return triangles.AsNativeArray<int>().Slice(start, count);
        }
    }
}