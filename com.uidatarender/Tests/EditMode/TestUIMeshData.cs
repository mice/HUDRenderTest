using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class TestUIMeshData
{
    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_025.md
    [Test]
    [Category("UT_MESH_025")]
    public void WriteSimpleQuad_WritesCanonicalManagedGeometry()
    {
        var data = new UIMeshData { TextureIndex = 3 };
        data.WriteSimpleQuad(new Vector4(-2, -1, 2, 1), new Vector4(.1f, .2f, .8f, .9f), new Color32(10, 20, 30, 40));

        Assert.AreEqual(4, data.mesh.VertexCount);
        Assert.AreEqual(6, data.mesh.IndicesCount);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 3, 0 }, data.triangles);
        Assert.AreEqual(new Vector3(-2, -1, 0), data.vertList[0]);
        Assert.AreEqual(new Vector3(2, 1, 0), data.vertList[2]);
        Assert.AreEqual(new Vector4(.8f, .9f, 3, 0), data.uvs[2]);
        Assert.AreEqual(new Color32(10, 20, 30, 40), data.colors[3]);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_024.md
    [Test]
    [Category("UT_MESH_024")]
    public void TransformVertex_MultiplyPoint3x4MatchesLegacy_AndReportsCost()
    {
        const int vertexCount = 768;
        const int iterations = 1000;
        var matrix = Matrix4x4.TRS(new Vector3(3, -2, 1), Quaternion.Euler(12, 27, -8), new Vector3(1.2f, 0.8f, 1.1f));
        var vertices = new Vector3[vertexCount];
        var legacy = new Vector3[vertexCount];
        var optimized = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++)
            vertices[i] = new Vector3(i % 32, i / 32, (i % 7) * 0.1f);

        for (int i = 0; i < vertexCount; i++)
        {
            legacy[i] = matrix.MultiplyPoint(vertices[i]);
            optimized[i] = matrix.MultiplyPoint3x4(vertices[i]);
            Assert.That(optimized[i].x, Is.EqualTo(legacy[i].x).Within(1e-5f));
            Assert.That(optimized[i].y, Is.EqualTo(legacy[i].y).Within(1e-5f));
            Assert.That(optimized[i].z, Is.EqualTo(legacy[i].z).Within(1e-5f));
        }

        var stopwatch = new Stopwatch();
        stopwatch.Start();
        for (int iteration = 0; iteration < iterations; iteration++)
            for (int i = 0; i < vertexCount; i++)
                legacy[i] = matrix.MultiplyPoint(vertices[i]);
        stopwatch.Stop();
        var legacyTicks = stopwatch.ElapsedTicks;

        stopwatch.Restart();
        for (int iteration = 0; iteration < iterations; iteration++)
            for (int i = 0; i < vertexCount; i++)
                optimized[i] = matrix.MultiplyPoint3x4(vertices[i]);
        stopwatch.Stop();
        var optimizedTicks = stopwatch.ElapsedTicks;

        TestContext.Out.WriteLine(
            $"TransformVertex comparison: vertices={vertexCount}, iterations={iterations}, " +
            $"frequency={Stopwatch.Frequency}, legacyTicks={legacyTicks}, optimizedTicks={optimizedTicks}, " +
            $"legacyMs={legacyTicks * 1000d / Stopwatch.Frequency:F3}, " +
            $"optimizedMs={optimizedTicks * 1000d / Stopwatch.Frequency:F3}, " +
            $"speedup={(double)legacyTicks / System.Math.Max(1, optimizedTicks):F2}x");
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_021.md
    [Test]
    [Category("UT_MESH_021")]
    public void FillVertex_CapturesCompleteMeshContract()
    {
        var meshData = new UIMeshData { TextureIndex = 4 };
        using (var helper = CreateDetailedQuad())
        {
            meshData.FillVertex(helper, 7);
        }

        Assert.AreEqual(4, meshData.mesh.VertexCount);
        Assert.AreEqual(6, meshData.mesh.IndicesCount);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 3, 0 }, meshData.triangles);
        CollectionAssert.AreEqual(new[]
        {
            new Color32(10, 20, 30, 40),
            new Color32(50, 60, 70, 80),
            new Color32(90, 100, 110, 120),
            new Color32(130, 140, 150, 160),
        }, meshData.colors);
        Assert.AreEqual(new Vector4(0.1f, 0.2f, 4, 7), meshData.uvs[0]);
        Assert.AreEqual(new Vector4(0.7f, 0.8f, 4, 7), meshData.uvs[3]);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_022.md
    [Test]
    [Category("UT_MESH_022")]
    public void TransformVertex_ChangesPositionsWithoutChangingTopology()
    {
        var meshData = new UIMeshData();
        using (var helper = CreateQuad())
        {
            meshData.FillVertex(helper, 0);
        }

        var originalIndices = (int[])meshData.triangles.Clone();
        meshData.TransformVertex(Matrix4x4.Translate(new Vector3(3, 4, 0)));

        Assert.AreEqual(new Vector3(13, 4, 0), meshData.vertList[0]);
        Assert.AreEqual(new Vector3(23, 14, 0), meshData.vertList[2]);
        CollectionAssert.AreEqual(originalIndices, meshData.triangles);
    }

    // TC-UM-01
    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_003.md
    [Test]
    [Category("UT_MESH_003")]
    public void FillToDrawData_AppliesLocalPosition()
    {
        var meshData = new UIMeshData();
        using (var helper = CreateQuad())
        {
            meshData.FillVertex(helper, 0);
        }

        var verts = new List<Vector3>();
        var uvs = new List<Vector4>();
        var colors = new List<Color32>();
        var triangles = new List<int>();
        var localPosition = new Vector3(10, 20, 0);

        meshData.FillToDrawData(verts, uvs, colors, triangles, localPosition);

        Assert.AreEqual(4, verts.Count);
        for (int i = 0; i < verts.Count; i++)
        {
            Assert.That(verts[i].x, Is.EqualTo(meshData.vertList[i].x + localPosition.x).Within(1e-4f));
            Assert.That(verts[i].y, Is.EqualTo(meshData.vertList[i].y + localPosition.y).Within(1e-4f));
        }
    }

    // TC-UM-02
    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_004.md
    [Test]
    [Category("UT_MESH_004")]
    public void FillToDrawData_RebasesIndices()
    {
        var meshData = new UIMeshData();
        using (var helper = CreateQuad())
        {
            meshData.FillVertex(helper, 0);
        }

        var verts = new List<Vector3>();
        var uvs = new List<Vector4>();
        var colors = new List<Color32>();
        var triangles = new List<int>();

        // Seed 3 pre-existing verts so offset = 3
        verts.Add(Vector3.zero); verts.Add(Vector3.one); verts.Add(Vector3.up);
        triangles.Add(0); triangles.Add(1); triangles.Add(2);
        int preCount = verts.Count;  // 3

        meshData.FillToDrawData(verts, uvs, colors, triangles, Vector3.zero);

        for (int k = 0; k < meshData.mesh.IndicesCount; k++)
        {
            Assert.AreEqual(preCount + meshData.triangles[k], triangles[3 + k],
                $"triangle[{k}] must be rebased by {preCount}");
        }
    }

    // TC-UM-04
    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_006.md
    [Test]
    [Category("UT_MESH_006")]
    public void UpdateTextureIndex_WritesUvZForAll()
    {
        var meshData = new UIMeshData();
        using (var helper = CreateQuad())
        {
            meshData.FillVertex(helper, 0);
        }

        meshData.UpdateTextureIndex(3);

        for (int i = 0; i < meshData.mesh.VertexCount; i++)
        {
            Assert.AreEqual(3, meshData.uvs[i].z, $"uv[{i}].z must be updated to new texture index");
        }
    }

    // TC-UM-03
    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_005.md
    [Test]
    [Category("UT_MESH_005")]
    public void FillWithMatrix_ScaleRotation()
    {
        var meshData = new UIMeshData();
        using (var helper = CreateQuad())
        {
            meshData.FillVertex(helper, 2);
        }

        var verts = new List<Vector3>();
        var uvs = new List<Vector4>();
        var colors = new List<Color32>();
        var triangles = new List<int>();
        var matrix = Matrix4x4.TRS(new Vector3(1, 2, 0), Quaternion.Euler(0, 0, 90), new Vector3(2, 3, 1));

        meshData.FillWithMatrix(verts, uvs, colors, triangles, matrix);

        AssertVectorEqual(matrix.MultiplyPoint(new Vector3(10, 0, 0)), verts[0]);
        AssertVectorEqual(matrix.MultiplyPoint(new Vector3(10, 10, 0)), verts[1]);
        AssertVectorEqual(matrix.MultiplyPoint(new Vector3(20, 10, 0)), verts[2]);
        AssertVectorEqual(matrix.MultiplyPoint(new Vector3(20, 0, 0)), verts[3]);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 3, 0 }, triangles);
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

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
    }
}
