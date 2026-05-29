using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class TestUIMeshDataX
{
    [SetUp]
    public void SetUp()
    {
        UIMeshDataX.geometry.Reset();
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_010.md
    [Test]
    [Category("UT_MESH_010")]
    public void FillToTriangleData_UsesIndicesOffset()
    {
        var first = new UIMeshDataX();
        using (var firstHelper = CreateTriangle())
        {
            first.FillVertex(firstHelper, 0);
        }

        var second = new UIMeshDataX();
        using (var secondHelper = CreateQuad())
        {
            second.FillVertex(secondHelper, 0);
        }

        var triangles = new List<int>();
        var localPosition = new Vector3(5, 6, 0);

        second.FillToTriangleData(triangles, localPosition);

        CollectionAssert.AreEqual(new[] { 3, 4, 5, 5, 6, 3 }, triangles);
        Assert.AreEqual(new Vector3(15, 6, 0), UIMeshDataX.geometry.drawVertex[second.mesh.VertexOffset + 0]);
        Assert.AreEqual(new Vector3(15, 16, 0), UIMeshDataX.geometry.drawVertex[second.mesh.VertexOffset + 1]);
        Assert.AreEqual(new Vector3(25, 16, 0), UIMeshDataX.geometry.drawVertex[second.mesh.VertexOffset + 2]);
        Assert.AreEqual(new Vector3(25, 6, 0), UIMeshDataX.geometry.drawVertex[second.mesh.VertexOffset + 3]);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_009.md
    [Test]
    [Category("UT_MESH_009")]
    public void FillToDrawData_VertsEqual()
    {
        var output = CreateEquivalentOutputs();

        CollectionAssert.AreEqual(output.MeshDataVerts, output.MeshDataXVerts);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_008.md
    [Test]
    [Category("UT_MESH_008")]
    public void FillToDrawData_UVsColorsEqual()
    {
        var output = CreateEquivalentOutputs();

        CollectionAssert.AreEqual(output.MeshDataUvs, output.MeshDataXUvs);
        CollectionAssert.AreEqual(output.MeshDataColors, output.MeshDataXColors);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_007.md
    [Test]
    [Category("UT_MESH_007")]
    public void FillToDrawData_IndicesEqual()
    {
        var output = CreateEquivalentOutputs();

        CollectionAssert.AreEqual(output.MeshDataTriangles, output.MeshDataXTriangles);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_011.md
    [Test]
    [Category("UT_MESH_011")]
    public void FillWithMatrix_IdentityEqualsFill()
    {
        var meshDataX = CreateMeshDataX();
        var fillOutput = new MeshLists();
        var matrixOutput = new MeshLists();
        var localPosition = new Vector3(3, 4, 0);

        Seed(fillOutput.Verts, fillOutput.Uvs, fillOutput.Colors, fillOutput.Triangles);
        Seed(matrixOutput.Verts, matrixOutput.Uvs, matrixOutput.Colors, matrixOutput.Triangles);

        meshDataX.FillToDrawData(fillOutput.Verts, fillOutput.Uvs, fillOutput.Colors, fillOutput.Triangles, localPosition);
        meshDataX.FillWithMatrix(matrixOutput.Verts, matrixOutput.Uvs, matrixOutput.Colors, matrixOutput.Triangles, Matrix4x4.Translate(localPosition));

        CollectionAssert.AreEqual(fillOutput.Verts, matrixOutput.Verts);
        CollectionAssert.AreEqual(fillOutput.Uvs, matrixOutput.Uvs);
        CollectionAssert.AreEqual(fillOutput.Colors, matrixOutput.Colors);
        CollectionAssert.AreEqual(fillOutput.Triangles, matrixOutput.Triangles);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_012.md
    [Test]
    [Category("UT_MESH_012")]
    public void FillWithMatrix_TRSEqual()
    {
        var output = CreateMatrixOutputs(Matrix4x4.TRS(new Vector3(1, 2, 0), Quaternion.Euler(0, 0, 90), new Vector3(2, 3, 1)));

        AssertVectorListsEqual(output.MeshDataVerts, output.MeshDataXVerts);
        CollectionAssert.AreEqual(output.MeshDataUvs, output.MeshDataXUvs);
        CollectionAssert.AreEqual(output.MeshDataColors, output.MeshDataXColors);
        CollectionAssert.AreEqual(output.MeshDataTriangles, output.MeshDataXTriangles);
    }

    private static VertexHelper CreateTriangle()
    {
        var helper = new VertexHelper();
        helper.AddVert(Vector3.zero, Color.white, Vector2.zero);
        helper.AddVert(Vector3.up, Color.white, Vector2.up);
        helper.AddVert(Vector3.right, Color.white, Vector2.right);
        helper.AddTriangle(0, 1, 2);
        return helper;
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

    private static MeshOutput CreateEquivalentOutputs()
    {
        var localPosition = new Vector3(3, 4, 0);

        var meshData = new UIMeshData();
        using (var helper = CreateQuad())
        {
            meshData.FillVertex(helper, 2);
        }

        var meshDataX = new UIMeshDataX();
        using (var helper = CreateQuad())
        {
            meshDataX.FillVertex(helper, 2);
        }

        var output = new MeshOutput();
        Seed(output.MeshDataVerts, output.MeshDataUvs, output.MeshDataColors, output.MeshDataTriangles);
        Seed(output.MeshDataXVerts, output.MeshDataXUvs, output.MeshDataXColors, output.MeshDataXTriangles);

        meshData.FillToDrawData(output.MeshDataVerts, output.MeshDataUvs, output.MeshDataColors, output.MeshDataTriangles, localPosition);
        meshDataX.FillToDrawData(output.MeshDataXVerts, output.MeshDataXUvs, output.MeshDataXColors, output.MeshDataXTriangles, localPosition);

        return output;
    }

    private static MeshOutput CreateMatrixOutputs(Matrix4x4 matrix)
    {
        var meshData = CreateMeshData();
        var meshDataX = CreateMeshDataX();
        var output = new MeshOutput();
        Seed(output.MeshDataVerts, output.MeshDataUvs, output.MeshDataColors, output.MeshDataTriangles);
        Seed(output.MeshDataXVerts, output.MeshDataXUvs, output.MeshDataXColors, output.MeshDataXTriangles);

        meshData.FillWithMatrix(output.MeshDataVerts, output.MeshDataUvs, output.MeshDataColors, output.MeshDataTriangles, matrix);
        meshDataX.FillWithMatrix(output.MeshDataXVerts, output.MeshDataXUvs, output.MeshDataXColors, output.MeshDataXTriangles, matrix);

        return output;
    }

    private static UIMeshData CreateMeshData()
    {
        var meshData = new UIMeshData();
        using (var helper = CreateQuad())
        {
            meshData.FillVertex(helper, 2);
        }

        return meshData;
    }

    private static UIMeshDataX CreateMeshDataX()
    {
        var meshDataX = new UIMeshDataX();
        using (var helper = CreateQuad())
        {
            meshDataX.FillVertex(helper, 2);
        }

        return meshDataX;
    }

    private static void Seed(List<Vector3> verts, List<Vector4> uvs, List<Color32> colors, List<int> triangles)
    {
        verts.Add(new Vector3(-1, -1, 0));
        uvs.Add(Vector4.zero);
        colors.Add(Color.black);
        triangles.Add(0);
    }

    private static void AssertVectorListsEqual(List<Vector3> expected, List<Vector3> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.That(actual[i].x, Is.EqualTo(expected[i].x).Within(0.0001f));
            Assert.That(actual[i].y, Is.EqualTo(expected[i].y).Within(0.0001f));
            Assert.That(actual[i].z, Is.EqualTo(expected[i].z).Within(0.0001f));
        }
    }

    private sealed class MeshOutput
    {
        public readonly List<Vector3> MeshDataVerts = new List<Vector3>();
        public readonly List<Vector4> MeshDataUvs = new List<Vector4>();
        public readonly List<Color32> MeshDataColors = new List<Color32>();
        public readonly List<int> MeshDataTriangles = new List<int>();

        public readonly List<Vector3> MeshDataXVerts = new List<Vector3>();
        public readonly List<Vector4> MeshDataXUvs = new List<Vector4>();
        public readonly List<Color32> MeshDataXColors = new List<Color32>();
        public readonly List<int> MeshDataXTriangles = new List<int>();
    }

    private sealed class MeshLists
    {
        public readonly List<Vector3> Verts = new List<Vector3>();
        public readonly List<Vector4> Uvs = new List<Vector4>();
        public readonly List<Color32> Colors = new List<Color32>();
        public readonly List<int> Triangles = new List<int>();
    }
}
