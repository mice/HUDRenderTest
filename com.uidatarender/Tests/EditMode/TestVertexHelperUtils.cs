using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class TestVertexHelperUtils
{
    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_013.md
    [Test]
    [Category("UT_MESH_013")]
    public void FillData2_WritesTextureIndexAndFlags()
    {
        var verts = new Vector3[0];
        var colors = new Color32[0];
        var uvs = new Vector4[0];
        var triangles = new int[0];

        using (var helper = CreateQuad())
        {
            var result = helper.FillData2(ref verts, ref colors, ref uvs, ref triangles, 2, 9);

            Assert.AreEqual(4, result.Item1);
            Assert.AreEqual(6, result.Item2);
        }

        for (int i = 0; i < uvs.Length; i++)
        {
            Assert.AreEqual(2, uvs[i].z);
            Assert.AreEqual(9, uvs[i].w);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_014.md
    [Test]
    [Category("UT_MESH_014")]
    public void FillData3_IndicesIncludeVertexOffset()
    {
        var verts = new Vector3[0];
        var colors = new Color32[0];
        var uvs = new Vector4[0];
        var triangles = new int[0];
        const int vertexOffset = 5;
        const int indicesOffset = 7;

        using (var helper = CreateQuad())
        {
            helper.FillData3(ref verts, ref colors, ref uvs, ref triangles, vertexOffset, indicesOffset, 3, 11);
        }

        CollectionAssert.AreEqual(new[] { 5, 6, 7, 7, 8, 5 }, new[]
        {
            triangles[indicesOffset + 0],
            triangles[indicesOffset + 1],
            triangles[indicesOffset + 2],
            triangles[indicesOffset + 3],
            triangles[indicesOffset + 4],
            triangles[indicesOffset + 5],
        });
        Assert.AreEqual(3, uvs[vertexOffset].z);
        Assert.AreEqual(11, uvs[vertexOffset].w);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_016.md
    [Test]
    [Category("UT_MESH_016")]
    public void FillData2_PreservesIndexedTopologyAndVertexChannels()
    {
        var verts = Array.Empty<Vector3>();
        var colors = Array.Empty<Color32>();
        var uvs = Array.Empty<Vector4>();
        var triangles = Array.Empty<int>();

        using (var helper = CreateDetailedQuad())
        {
            var result = helper.FillData2(ref verts, ref colors, ref uvs, ref triangles, 7, 11);

            Assert.AreEqual(4, result.Item1);
            Assert.AreEqual(6, result.Item2);
        }

        CollectionAssert.AreEqual(new[]
        {
            new Vector3(1, 2, 0),
            new Vector3(3, 5, 0),
            new Vector3(8, 13, 0),
            new Vector3(21, 34, 0),
        }, verts);
        CollectionAssert.AreEqual(new[]
        {
            new Color32(10, 20, 30, 40),
            new Color32(50, 60, 70, 80),
            new Color32(90, 100, 110, 120),
            new Color32(130, 140, 150, 160),
        }, colors);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 3, 0 }, triangles);

        Assert.AreEqual(new Vector4(0.1f, 0.2f, 7, 11), uvs[0]);
        Assert.AreEqual(new Vector4(0.3f, 0.4f, 7, 11), uvs[1]);
        Assert.AreEqual(new Vector4(0.5f, 0.6f, 7, 11), uvs[2]);
        Assert.AreEqual(new Vector4(0.7f, 0.8f, 7, 11), uvs[3]);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_017.md
    [Test]
    [Category("UT_MESH_017")]
    public void FillData2And3_AfterWarmup_DoNotAllocateManagedMemory()
    {
        const int warmupIterations = 32;
        const int measuredIterations = 10000;
        var exactVerts = new Vector3[4];
        var exactColors = new Color32[4];
        var exactUvs = new Vector4[4];
        var exactTriangles = new int[6];
        var appendVerts = new Vector3[6];
        var appendColors = new Color32[6];
        var appendUvs = new Vector4[6];
        var appendTriangles = new int[9];
        var stopwatch = new Stopwatch();

        using (var helper = CreateQuad())
        {
            for (int i = 0; i < warmupIterations; i++)
            {
                helper.FillData2(ref exactVerts, ref exactColors, ref exactUvs, ref exactTriangles, 2, 9);
                helper.FillData3(ref appendVerts, ref appendColors, ref appendUvs, ref appendTriangles, 2, 3, 3, 11);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long fill2Before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < measuredIterations; i++)
            {
                helper.FillData2(ref exactVerts, ref exactColors, ref exactUvs, ref exactTriangles, 2, 9);
            }
            stopwatch.Stop();
            long fill2Allocated = GC.GetAllocatedBytesForCurrentThread() - fill2Before;
            long fill2Ticks = stopwatch.ElapsedTicks;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long fill3Before = GC.GetAllocatedBytesForCurrentThread();
            stopwatch.Restart();
            for (int i = 0; i < measuredIterations; i++)
            {
                helper.FillData3(ref appendVerts, ref appendColors, ref appendUvs, ref appendTriangles, 2, 3, 3, 11);
            }
            stopwatch.Stop();
            long fill3Allocated = GC.GetAllocatedBytesForCurrentThread() - fill3Before;
            long fill3Ticks = stopwatch.ElapsedTicks;

            TestContext.Out.WriteLine(
                $"VertexHelper extraction: iterations={measuredIterations}, " +
                $"FillData2Ticks={fill2Ticks}, FillData2Allocated={fill2Allocated}, " +
                $"FillData3Ticks={fill3Ticks}, FillData3Allocated={fill3Allocated}");

            Assert.AreEqual(0, fill2Allocated, "FillData2 allocated managed memory after warm-up.");
            Assert.AreEqual(0, fill3Allocated, "FillData3 allocated managed memory after warm-up.");
        }
    }

    private static VertexHelper CreateQuad()
    {
        var helper = new VertexHelper();
        helper.AddVert(new Vector3(10, 0, 0), Color.white, Vector2.zero);
        helper.AddVert(new Vector3(10, 10, 0), Color.white, Vector2.up);
        helper.AddVert(new Vector3(20, 10, 0), Color.white, Vector2.one);
        helper.AddVert(new Vector3(20, 0, 0), Color.white, Vector2.right);
        helper.AddTriangle(0, 1, 2);
        helper.AddTriangle(2, 3, 0);
        return helper;
    }

    private static VertexHelper CreateDetailedQuad()
    {
        var helper = new VertexHelper();
        AddVertex(helper, new Vector3(1, 2, 0), new Color32(10, 20, 30, 40), new Vector4(0.1f, 0.2f, 0, 0));
        AddVertex(helper, new Vector3(3, 5, 0), new Color32(50, 60, 70, 80), new Vector4(0.3f, 0.4f, 0, 0));
        AddVertex(helper, new Vector3(8, 13, 0), new Color32(90, 100, 110, 120), new Vector4(0.5f, 0.6f, 0, 0));
        AddVertex(helper, new Vector3(21, 34, 0), new Color32(130, 140, 150, 160), new Vector4(0.7f, 0.8f, 0, 0));
        helper.AddTriangle(0, 1, 2);
        helper.AddTriangle(2, 3, 0);
        return helper;
    }

    private static void AddVertex(VertexHelper helper, Vector3 position, Color32 color, Vector4 uv)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertex.uv0 = uv;
        helper.AddVert(vertex);
    }
}
