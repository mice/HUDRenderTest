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
        private static Mesh _indexScratchMesh;
        private static List<int> _indexScratchIndices;

        public static (int, int) FillData2(this VertexHelper vertexHelper, ref Vector3[] vertList, ref Color32[] colors, ref Vector4[] uvs, ref int[] triangles, int textureIndex, int flags)
        {
            var vertexCount = vertexHelper.currentVertCount;
            var indexCount = vertexHelper.currentIndexCount;

            ResizeExact(ref vertList, vertexCount);
            ResizeExact(ref colors, vertexCount);
            ResizeExact(ref uvs, vertexCount);
            ResizeExact(ref triangles, indexCount);

            CopyVertices(vertexHelper, vertList, colors, uvs, 0, textureIndex, flags);
            CopyIndicesForExtraction(vertexHelper, triangles, 0);

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
            CopyIndicesForExtraction(vertexHelper, triangles, indicesOffset, vertexOffset);
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

        private static void CopyIndicesForExtraction(VertexHelper vertexHelper, int[] triangles, int indicesOffset, int vertexOffset = 0)
        {
            using (CopyIndicesMarker.Auto())
            {
                // A compatible custom UGUI instance method takes precedence over the
                // package extension. Stock UGUI resolves to the fallback below.
                vertexHelper.CopyIndices(triangles, indicesOffset, vertexOffset);
            }
        }

        /// <summary>
        /// Copies indexed topology through stock UGUI public APIs. Main thread only.
        /// Nonempty extraction inherits FillMesh's limit of fewer than 65,000 vertices.
        /// Scratch storage is reused; larger geometry may allocate while growing it.
        /// </summary>
        public static void CopyIndices(this VertexHelper vertexHelper, int[] destination, int destinationOffset = 0, int vertexOffset = 0)
        {
            if (vertexHelper == null) throw new ArgumentNullException(nameof(vertexHelper));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            var indexCount = vertexHelper.currentIndexCount;
            if (destinationOffset < 0 || destinationOffset > destination.Length ||
                indexCount > destination.Length - destinationOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(destinationOffset));
            }

            if (indexCount == 0) return;

            if (_indexScratchMesh == null)
            {
                _indexScratchMesh = new Mesh
                {
                    name = "UIData.VertexHelper.IndexScratch",
                    hideFlags = HideFlags.HideAndDontSave
                };
                _indexScratchIndices = new List<int>(indexCount);
            }

            vertexHelper.FillMesh(_indexScratchMesh);
            _indexScratchIndices.Clear();
            _indexScratchMesh.GetTriangles(_indexScratchIndices, 0);
            for (var i = 0; i < indexCount; i++)
            {
                destination[destinationOffset + i] = _indexScratchIndices[i] + vertexOffset;
            }
        }

        /// <summary>
        /// Releases shared fallback storage on the main thread. The next extraction
        /// recreates it. Mesh destruction is deferred while playing.
        /// </summary>
        public static void ReleaseIndexScratch()
        {
            if (_indexScratchMesh != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_indexScratchMesh);
                else UnityEngine.Object.DestroyImmediate(_indexScratchMesh);
            }

            _indexScratchMesh = null;
            _indexScratchIndices = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetIndexScratch()
        {
            ReleaseIndexScratch();
            Application.quitting -= ReleaseIndexScratch;
            Application.quitting += ReleaseIndexScratch;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterIndexScratchCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseIndexScratch;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseIndexScratch;
            UnityEditor.EditorApplication.quitting -= ReleaseIndexScratch;
            UnityEditor.EditorApplication.quitting += ReleaseIndexScratch;
        }
#endif

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
