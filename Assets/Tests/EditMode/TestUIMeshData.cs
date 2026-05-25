using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class TestUIMeshData
{
    // TC-UM-01
    [Test]
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
    [Test]
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
    [Test]
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
    [Test]
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

    private static void AssertVectorEqual(Vector3 expected, Vector3 actual)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
    }
}
