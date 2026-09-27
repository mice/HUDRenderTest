using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

namespace UnityEngine.UI
{
    public static class VertexHelperUtils
    {
        private static readonly ProfilerMarker CopyVerticesMarker = new ProfilerMarker("UIData.VertexHelper.CopyVertices");
        private static readonly ProfilerMarker CopyIndicesMarker = new ProfilerMarker("UIData.VertexHelper.CopyIndices");

        public static (int, int) FillData2(this VertexHelper vertexHelper, ref Vector3[] vertList, ref Color32[] colors, ref Vector4[] uvs, ref int[] triangles, int textureIndex, int flags)
        {
            var vertexCount = vertexHelper.currentVertCount;
            var indexCount = vertexHelper.currentIndexCount;

            ResizeExact(ref vertList, vertexCount);
            ResizeExact(ref colors, vertexCount);
            ResizeExact(ref uvs, vertexCount);
            ResizeExact(ref triangles, indexCount);

            CopyVertices(vertexHelper, vertList, colors, uvs, 0, textureIndex, flags);
            CopyIndices(vertexHelper, triangles, 0);

            return (vertexCount, indexCount);
        }

        public static void FillData3(this VertexHelper vertexHelper, ref Vector3[] vertList, ref Color32[] colors, ref Vector4[] uvs, ref int[] triangles, int vertexOffset, int indicesOffset, int textureIndex, int flags)
        {
            var vertexCount = vertexHelper.currentVertCount;
            var indexCount = vertexHelper.currentIndexCount;

            EnsureCapacity(ref vertList, vertexOffset + vertexCount);
            EnsureCapacity(ref colors, vertexOffset + vertexCount);
            EnsureCapacity(ref uvs, vertexOffset + vertexCount);
            EnsureCapacity(ref triangles, indicesOffset + indexCount);

            CopyVertices(vertexHelper, vertList, colors, uvs, vertexOffset, textureIndex, flags);
            CopyIndices(vertexHelper, triangles, indicesOffset, vertexOffset);
        }

        private static void CopyVertices(VertexHelper vertexHelper, Vector3[] vertList, Color32[] colors, Vector4[] uvs, int vertexOffset, int textureIndex, int flags)
        {
            using (CopyVerticesMarker.Auto())
            {
            var vertexCount = vertexHelper.currentVertCount;
            var vertex = default(UIVertex);

            for (int i = 0; i < vertexCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                var targetIndex = vertexOffset + i;
                vertList[targetIndex] = vertex.position;
                colors[targetIndex] = vertex.color;

                var uv = vertex.uv0;
                uv.z = textureIndex;
                uv.w = flags;
                uvs[targetIndex] = uv;
            }
            }
        }

        private static void CopyIndices(VertexHelper vertexHelper, int[] triangles, int indicesOffset, int vertexOffset = 0)
        {
            using (CopyIndicesMarker.Auto())
            {
                vertexHelper.CopyIndices(triangles, indicesOffset, vertexOffset);
            }
        }

        private static void ResizeExact<T>(ref T[] array, int length)
        {
            if (array == null || array.Length != length)
            {
                Array.Resize(ref array, length);
            }
        }

        private static void EnsureCapacity<T>(ref T[] array, int minimumLength)
        {
            if (array == null)
            {
                array = new T[minimumLength];
            }
            else if (array.Length < minimumLength)
            {
                Array.Resize(ref array, minimumLength);
            }
        }
    }
}
