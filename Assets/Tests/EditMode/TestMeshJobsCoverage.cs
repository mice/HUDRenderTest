using NUnit.Framework;
using UIData;
using Unity.Collections;
using UnityEngine;

public class TestMeshJobsCoverage
{
    // TestRecord: Documentation~/Testing/Unit/Jobs/UT_JOB_006.md
    [Test]
    [Category("UT_JOB_006")]
    public void LegacyMergeJobs_WriteExpectedArrays()
    {
        var meshes = new[] { CreateMeshData(Vector3.zero, new Color32(10, 20, 30, 255)) };
        var positions = new NativeArray<Vector3>(2, Allocator.Temp);
        var resultPos = new NativeArray<Vector3>(8, Allocator.Temp);
        var resultColors = new NativeArray<Color32>(8, Allocator.Temp);
        var resultUvs = new NativeArray<Vector4>(8, Allocator.Temp);
        var resultIndices = new NativeArray<int>(12, Allocator.Temp);
        var resultCount = new NativeArray<int>(2, Allocator.Temp);
        try
        {
            positions[0] = new Vector3(1, 0, 0);
            positions[1] = new Vector3(0, 2, 0);

            new MergeVertexJob { arr = meshes, MeshCount = 1, pos = positions, result_pos = resultPos }.Execute();
            new MergeColorJob { arr = meshes, MeshCount = 1, totalCount = 2, result_colors = resultColors }.Execute();
            new MergeUVJob { arr = meshes, MeshCount = 1, totalCount = 2, result_uv = resultUvs }.Execute();
            new MergeIndicsJob { arr = meshes, MeshCount = 1, totalCount = 2, result_triangle = resultIndices, result_count = resultCount }.Execute();

            Assert.AreEqual(new Vector3(1, 0, 0), resultPos[0]);
            Assert.AreEqual(new Vector3(0, 2, 0), resultPos[4]);
            Assert.AreEqual((Color32)Color.white, resultColors[0]);
            Assert.AreEqual(meshes[0].uvs[0], resultUvs[0]);
            Assert.AreEqual(0, resultIndices[0]);
            Assert.AreEqual(4, resultIndices[6]);
            Assert.AreEqual(8, resultCount[0]);
            Assert.AreEqual(12, resultCount[1]);
        }
        finally
        {
            positions.Dispose();
            resultPos.Dispose();
            resultColors.Dispose();
            resultUvs.Dispose();
            resultIndices.Dispose();
            resultCount.Dispose();
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Jobs/UT_JOB_007.md
    [Test]
    [Category("UT_JOB_007")]
    public void XMergeJobs_WriteExpectedArrays()
    {
        var meshes = new[]
        {
            CreateMeshData(Vector3.zero, new Color32(10, 20, 30, 255)),
            CreateMeshData(new Vector3(2, 0, 0), default)
        };
        meshes[1].colors = System.Array.Empty<Color32>();
        var positions = new NativeArray<Vector3>(2, Allocator.Temp);
        var resultPos = new NativeArray<Vector3>(8, Allocator.Temp);
        var resultColors = new NativeArray<Color32>(8, Allocator.Temp);
        var resultUvs = new NativeArray<Vector4>(8, Allocator.Temp);
        var resultIndices = new NativeArray<int>(12, Allocator.Temp);
        var resultCount = new NativeArray<int>(2, Allocator.Temp);
        try
        {
            positions[0] = new Vector3(1, 0, 0);
            positions[1] = new Vector3(0, 2, 0);

            new MergeXVertexJob { arr = meshes, UIMeshCount = 2, pos = positions, result_pos = resultPos }.Execute();
            new MergeXColorJob { arr = meshes, UIMeshCount = 2, result_colors = resultColors }.Execute();
            new MergeXUVJob { arr = meshes, UIMeshCount = 2, result_uv = resultUvs }.Execute();
            new MergeXIndicsJob { arr = meshes, UIMeshCount = 2, result_triangle = resultIndices, result_count = resultCount }.Execute();

            Assert.AreEqual(new Vector3(1, 0, 0), resultPos[0]);
            Assert.AreEqual(new Vector3(2, 2, 0), resultPos[4]);
            Assert.AreEqual(new Color32(10, 20, 30, 255), resultColors[0]);
            Assert.AreEqual((Color32)Color.white, resultColors[4]);
            Assert.AreEqual(meshes[1].uvs[0], resultUvs[4]);
            Assert.AreEqual(0, resultIndices[0]);
            Assert.AreEqual(4, resultIndices[6]);
            Assert.AreEqual(8, resultCount[0]);
            Assert.AreEqual(12, resultCount[1]);
        }
        finally
        {
            positions.Dispose();
            resultPos.Dispose();
            resultColors.Dispose();
            resultUvs.Dispose();
            resultIndices.Dispose();
            resultCount.Dispose();
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Jobs/UT_JOB_008.md
    [Test]
    [Category("UT_JOB_008")]
    public void BigXMergeJob_WritesAllOutputs()
    {
        var meshes = new[] { CreateMeshData(Vector3.zero, new Color32(10, 20, 30, 255)) };
        var positions = new NativeArray<Vector3>(1, Allocator.Temp);
        var resultPos = new NativeArray<Vector3>(4, Allocator.Temp);
        var resultColors = new NativeArray<Color32>(4, Allocator.Temp);
        var resultUvs = new NativeArray<Vector4>(4, Allocator.Temp);
        var resultIndices = new NativeArray<int>(6, Allocator.Temp);
        var resultCount = new NativeArray<int>(2, Allocator.Temp);
        try
        {
            positions[0] = new Vector3(3, 4, 0);

            new BigXMergeJob
            {
                arr = meshes,
                UIMeshCount = 1,
                pos = positions,
                result_pos = resultPos,
                result_colors = resultColors,
                result_uv = resultUvs,
                result_triangle = resultIndices,
                result_count = resultCount
            }.Execute();

            Assert.AreEqual(new Vector3(3, 4, 0), resultPos[0]);
            Assert.AreEqual(new Color32(10, 20, 30, 255), resultColors[0]);
            Assert.AreEqual(meshes[0].uvs[0], resultUvs[0]);
            CollectionAssert.AreEqual(meshes[0].triangles, resultIndices.ToArray());
            Assert.AreEqual(4, resultCount[0]);
            Assert.AreEqual(6, resultCount[1]);
        }
        finally
        {
            positions.Dispose();
            resultPos.Dispose();
            resultColors.Dispose();
            resultUvs.Dispose();
            resultIndices.Dispose();
            resultCount.Dispose();
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Jobs/UT_JOB_009.md
    [Test]
    [Category("UT_JOB_009")]
    public void BurstMergeJobUtility_CopiesMeshSlimOffsets()
    {
        var slim = new MeshSlim { VertexOffset = 1, VertexCount = 2, IndicesOffset = 3, IndicesCount = 4 };

        var soa = BurstMergeJobUtility.ToSoA(slim);

        Assert.AreEqual(slim.VertexOffset, soa.VertexOffset);
        Assert.AreEqual(slim.VertexCount, soa.VertexCount);
        Assert.AreEqual(slim.IndicesOffset, soa.IndicesOffset);
        Assert.AreEqual(slim.IndicesCount, soa.IndicesCount);
    }

    private static UIMeshData CreateMeshData(Vector3 offset, Color32 color)
    {
        return new UIMeshData
        {
            vertList = new[]
            {
                offset + new Vector3(0, 0, 0),
                offset + new Vector3(0, 1, 0),
                offset + new Vector3(1, 1, 0),
                offset + new Vector3(1, 0, 0),
            },
            uvs = new[]
            {
                new Vector4(0, 0, 1, 0),
                new Vector4(0, 1, 1, 0),
                new Vector4(1, 1, 1, 0),
                new Vector4(1, 0, 1, 0),
            },
            colors = new[] { color, color, color, color },
            triangles = new[] { 0, 1, 2, 2, 3, 0 },
            mesh = new MeshSlim { VertexCount = 4, IndicesCount = 6 },
        };
    }
}
