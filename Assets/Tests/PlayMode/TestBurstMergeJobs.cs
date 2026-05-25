using System.Collections;
using NUnit.Framework;
using UIData;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// TC-JOB-03: BurstMergeJob output matches the expected SoA merge result.
/// </summary>
public class TestBurstMergeJobs
{
    private static readonly int[] QuadIndices = { 0, 1, 2, 2, 3, 0 };

    [UnityTest]
    public IEnumerator BurstMerge_EquivalentToSoAMerge()
    {
        const int meshCount = 2;
        const int totalVerts = 8;
        const int totalIndices = 12;

        var meshes = new NativeArray<MeshSlimSoA>(meshCount, Allocator.TempJob);
        var positions = new NativeArray<Vector3>(meshCount, Allocator.TempJob);
        var sourceVertices = new NativeArray<Vector3>(totalVerts, Allocator.TempJob);
        var sourceUvs = new NativeArray<Vector4>(totalVerts, Allocator.TempJob);
        var sourceColors = new NativeArray<Color32>(totalVerts, Allocator.TempJob);
        var sourceIndices = new NativeArray<int>(totalIndices, Allocator.TempJob);
        var resultVertices = new NativeArray<Vector3>(totalVerts, Allocator.TempJob);
        var resultUvs = new NativeArray<Vector4>(totalVerts, Allocator.TempJob);
        var resultColors = new NativeArray<Color32>(totalVerts, Allocator.TempJob);
        var resultIndices = new NativeArray<int>(totalIndices, Allocator.TempJob);
        var resultCount = new NativeArray<int>(2, Allocator.TempJob);

        try
        {
            FillQuad(sourceVertices, sourceUvs, sourceIndices, 0, Vector3.zero);
            FillQuad(sourceVertices, sourceUvs, sourceIndices, 4, new Vector3(2, 0, 0));

            meshes[0] = new MeshSlimSoA { VertexOffset = 0, VertexCount = 4, IndicesOffset = 0, IndicesCount = 6 };
            meshes[1] = new MeshSlimSoA { VertexOffset = 4, VertexCount = 4, IndicesOffset = 6, IndicesCount = 6 };
            positions[0] = new Vector3(1, 0, 0);
            positions[1] = new Vector3(0, 2, 0);

            for (int i = 0; i < totalVerts; i++)
            {
                sourceColors[i] = new Color32((byte)(10 + i), 20, 30, 255);
            }

            new BurstMergeJob
            {
                Meshes = meshes,
                Positions = positions,
                SourceVertices = sourceVertices,
                SourceUvs = sourceUvs,
                SourceColors = sourceColors,
                SourceIndices = sourceIndices,
                ResultVertices = resultVertices,
                ResultUvs = resultUvs,
                ResultColors = resultColors,
                ResultIndices = resultIndices,
                ResultCount = resultCount,
            }.Schedule().Complete();

            yield return null;

            Assert.AreEqual(totalVerts, resultCount[0]);
            Assert.AreEqual(totalIndices, resultCount[1]);

            for (int i = 0; i < 4; i++)
            {
                Assert.That(resultVertices[i].x, Is.EqualTo(sourceVertices[i].x + positions[0].x).Within(1e-4f));
                Assert.That(resultVertices[i + 4].y, Is.EqualTo(sourceVertices[i + 4].y + positions[1].y).Within(1e-4f));
                Assert.AreEqual(sourceUvs[i], resultUvs[i]);
                Assert.AreEqual(sourceColors[i + 4], resultColors[i + 4]);
            }

            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(QuadIndices[i], resultIndices[i]);
                Assert.AreEqual(QuadIndices[i] + 4, resultIndices[i + 6]);
            }
        }
        finally
        {
            meshes.Dispose();
            positions.Dispose();
            sourceVertices.Dispose();
            sourceUvs.Dispose();
            sourceColors.Dispose();
            sourceIndices.Dispose();
            resultVertices.Dispose();
            resultUvs.Dispose();
            resultColors.Dispose();
            resultIndices.Dispose();
            resultCount.Dispose();
        }
    }

    [UnityTest]
    public IEnumerator BurstMerge_UsesWhiteWhenSourceColorsAreEmpty()
    {
        var meshes = new NativeArray<MeshSlimSoA>(1, Allocator.TempJob);
        var positions = new NativeArray<Vector3>(1, Allocator.TempJob);
        var sourceVertices = new NativeArray<Vector3>(4, Allocator.TempJob);
        var sourceUvs = new NativeArray<Vector4>(4, Allocator.TempJob);
        var sourceColors = new NativeArray<Color32>(0, Allocator.TempJob);
        var sourceIndices = new NativeArray<int>(6, Allocator.TempJob);
        var resultVertices = new NativeArray<Vector3>(4, Allocator.TempJob);
        var resultUvs = new NativeArray<Vector4>(4, Allocator.TempJob);
        var resultColors = new NativeArray<Color32>(4, Allocator.TempJob);
        var resultIndices = new NativeArray<int>(6, Allocator.TempJob);
        var resultCount = new NativeArray<int>(2, Allocator.TempJob);

        try
        {
            FillQuad(sourceVertices, sourceUvs, sourceIndices, 0, Vector3.zero);
            meshes[0] = new MeshSlimSoA { VertexOffset = 0, VertexCount = 4, IndicesOffset = 0, IndicesCount = 6 };

            new BurstMergeJob
            {
                Meshes = meshes,
                Positions = positions,
                SourceVertices = sourceVertices,
                SourceUvs = sourceUvs,
                SourceColors = sourceColors,
                SourceIndices = sourceIndices,
                ResultVertices = resultVertices,
                ResultUvs = resultUvs,
                ResultColors = resultColors,
                ResultIndices = resultIndices,
                ResultCount = resultCount,
            }.Schedule().Complete();

            yield return null;

            Color32 white = Color.white;
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(white, resultColors[i]);
            }
        }
        finally
        {
            meshes.Dispose();
            positions.Dispose();
            sourceVertices.Dispose();
            sourceUvs.Dispose();
            sourceColors.Dispose();
            sourceIndices.Dispose();
            resultVertices.Dispose();
            resultUvs.Dispose();
            resultColors.Dispose();
            resultIndices.Dispose();
            resultCount.Dispose();
        }
    }

    private static void FillQuad(
        NativeArray<Vector3> vertices,
        NativeArray<Vector4> uvs,
        NativeArray<int> indices,
        int vertexOffset,
        Vector3 offset)
    {
        vertices[vertexOffset] = new Vector3(0, 0, 0) + offset;
        vertices[vertexOffset + 1] = new Vector3(1, 0, 0) + offset;
        vertices[vertexOffset + 2] = new Vector3(1, 1, 0) + offset;
        vertices[vertexOffset + 3] = new Vector3(0, 1, 0) + offset;

        uvs[vertexOffset] = new Vector4(0, 0, 0, 0);
        uvs[vertexOffset + 1] = new Vector4(0, 1, 0, 0);
        uvs[vertexOffset + 2] = new Vector4(1, 1, 0, 0);
        uvs[vertexOffset + 3] = new Vector4(1, 0, 0, 0);

        int indexOffset = vertexOffset / 4 * QuadIndices.Length;
        for (int i = 0; i < QuadIndices.Length; i++)
        {
            indices[indexOffset + i] = vertexOffset + QuadIndices[i];
        }
    }
}
