using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace UIData
{
    public struct MeshSlimSoA
    {
        public int VertexOffset;
        public int VertexCount;
        public int IndicesOffset;
        public int IndicesCount;
    }

    [BurstCompile]
    public struct BurstMergeJob : IJob
    {
        [ReadOnly] public NativeArray<MeshSlimSoA> Meshes;
        [ReadOnly] public NativeArray<Vector3> Positions;
        [ReadOnly] public NativeArray<Vector3> SourceVertices;
        [ReadOnly] public NativeArray<Vector4> SourceUvs;
        [ReadOnly] public NativeArray<Color32> SourceColors;
        [ReadOnly] public NativeArray<int> SourceIndices;

        [WriteOnly] public NativeArray<Vector3> ResultVertices;
        [WriteOnly] public NativeArray<Vector4> ResultUvs;
        [WriteOnly] public NativeArray<Color32> ResultColors;
        [WriteOnly] public NativeArray<int> ResultIndices;
        [WriteOnly] public NativeArray<int> ResultCount;

        public void Execute()
        {
            int vertexWrite = 0;
            int indexWrite = 0;
            Color32 white = new Color32(255, 255, 255, 255);

            for (int i = 0; i < Meshes.Length; i++)
            {
                MeshSlimSoA mesh = Meshes[i];
                Vector3 position = Positions[i];
                int vertexBase = vertexWrite;

                for (int k = 0; k < mesh.VertexCount; k++)
                {
                    int sourceIndex = mesh.VertexOffset + k;
                    ResultVertices[vertexWrite] = SourceVertices[sourceIndex] + position;
                    ResultUvs[vertexWrite] = SourceUvs[sourceIndex];
                    ResultColors[vertexWrite] = HasColor(sourceIndex) ? SourceColors[sourceIndex] : white;
                    vertexWrite++;
                }

                for (int k = 0; k < mesh.IndicesCount; k++)
                {
                    int sourceIndex = SourceIndices[mesh.IndicesOffset + k];
                    ResultIndices[indexWrite++] = vertexBase + sourceIndex - mesh.VertexOffset;
                }
            }

            ResultCount[0] = vertexWrite;
            ResultCount[1] = indexWrite;
        }

        private bool HasColor(int index)
        {
            return SourceColors.Length > index;
        }
    }

    public static class BurstMergeJobUtility
    {
        public static MeshSlimSoA ToSoA(MeshSlim mesh)
        {
            return new MeshSlimSoA
            {
                VertexOffset = mesh.VertexOffset,
                VertexCount = mesh.VertexCount,
                IndicesOffset = mesh.IndicesOffset,
                IndicesCount = mesh.IndicesCount,
            };
        }
    }
}
