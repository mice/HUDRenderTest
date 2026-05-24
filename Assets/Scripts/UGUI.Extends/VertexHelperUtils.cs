using System;
using UnityEngine;

namespace UnityEngine.UI
{
    public static class VertexHelperUtils
    {
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
            var vertexCount = vertexHelper.currentVertCount;
            var positions = vertexHelper.m_Positions;
            var sourceColors = vertexHelper.m_Colors;
            var sourceUvs = vertexHelper.m_Uv0S;
            var white = (Color32)Color.white;

            for (int i = 0; i < vertexCount; i++)
            {
                var targetIndex = vertexOffset + i;
                vertList[targetIndex] = positions[i];
                colors[targetIndex] = sourceColors != null && i < sourceColors.Count ? sourceColors[i] : white;

                var uv = sourceUvs != null && i < sourceUvs.Count ? sourceUvs[i] : Vector4.zero;
                uv.z = textureIndex;
                uv.w = flags;
                uvs[targetIndex] = uv;
            }
        }

        private static void CopyIndices(VertexHelper vertexHelper, int[] triangles, int indicesOffset, int vertexOffset = 0)
        {
            var indexCount = vertexHelper.currentIndexCount;
            var sourceIndices = vertexHelper.m_Indices;

            for (int i = 0; i < indexCount; i++)
            {
                triangles[indicesOffset + i] = sourceIndices[i] + vertexOffset;
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
