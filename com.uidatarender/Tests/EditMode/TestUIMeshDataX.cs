using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class TestUIMeshDataX
{
    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_029.md
    [Test]
    [Category("UT_MESH_029")]
    public void SlimFill_PreservesColorSentinelAndOffsetGeometryForBothMethods()
    {
        using (var prefix = new UIMeshDataX())
        using (var data = new UIMeshDataX { TextureIndex = 7 })
        {
            prefix.WriteSimpleQuad(new Vector4(-1, -1, 1, 1), Vector4.zero, Color.white);
            data.WriteSimpleQuad(new Vector4(-2, -3, 4, 5), new Vector4(.1f, .2f, .8f, .9f), Color.white);
            Assert.Greater(data.mesh.VertexOffset, 0);
            Assert.Greater(data.mesh.IndicesOffset, 0);
            var cases = new[] { default(Color32), new Color32(1, 0, 0, 0), new Color32(0, 1, 0, 0),
                new Color32(0, 0, 1, 0), new Color32(0, 0, 0, 1), new Color32(0, 0, 0, 255),
                new Color32(11, 22, 33, 0), new Color32(11, 22, 33, 44), new Color32(255, 255, 255, 255) };
            var translation = new Vector3(3, -4, 2);
            var matrix = Matrix4x4.TRS(new Vector3(2, 3, 4), Quaternion.Euler(10, 20, 30), new Vector3(2, 3, 1));
            foreach (var color in cases)
            {
                for (int i = 0; i < 4; i++)
                {
                    int index = data.mesh.VertexOffset + i;
                    UIMeshDataX.geometry.colors[index] = color;
                    var uv = UIMeshDataX.geometry.uvs[index];
                    uv.w = 9;
                    UIMeshDataX.geometry.uvs[index] = uv;
                }
                foreach (bool withMatrix in new[] { false, true })
                {
                    var output = new MeshLists();
                    Seed(output.Verts, output.Uvs, output.Colors, output.Triangles);
                    if (withMatrix) data.FillWithMatrix(output.Verts, output.Uvs, output.Colors, output.Triangles, matrix);
                    else data.FillToDrawData(output.Verts, output.Uvs, output.Colors, output.Triangles, translation);
                    Assert.AreEqual(5, output.Verts.Count);
                    Assert.AreEqual(5, output.Uvs.Count);
                    Assert.AreEqual(5, output.Colors.Count);
                    Assert.AreEqual(Color.black, (Color)output.Colors[0]);
                    for (int i = 0; i < 4; i++)
                    {
                        int index = data.mesh.VertexOffset + i;
                        var expectedPosition = withMatrix ? matrix.MultiplyPoint(UIMeshDataX.geometry.vertex[index])
                            : UIMeshDataX.geometry.vertex[index] + translation;
                        Assert.LessOrEqual(Vector3.Distance(expectedPosition, output.Verts[i + 1]), 1e-5f);
                        Assert.AreEqual(UIMeshDataX.geometry.uvs[index], output.Uvs[i + 1]);
                        // Legacy semantics: transparent black is a sentinel; transparent colored values are not.
                        var expectedColor = color.Equals(default(Color32)) ? (Color32)Color.white : color;
                        Assert.AreEqual(expectedColor, output.Colors[i + 1]);
                        Assert.AreEqual(color, UIMeshDataX.geometry.colors[index], "Filling must not mutate source colors.");
                    }
                    CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 3, 4, 1 }, output.Triangles);
                }
            }
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_030.md
    [Test]
    [Category("UT_MESH_030")]
    public void SlimFill_WarmedPreallocatedOutputs_AllocateNoManagedMemory()
    {
        using (var data = new UIMeshDataX())
        {
            data.WriteSimpleQuad(new Vector4(-2, -1, 2, 1), Vector4.zero, new Color32(1, 2, 3, 4));
            UIMeshDataX.geometry.colors[data.mesh.VertexOffset] = default(Color32);
            var output = new MeshLists();
            output.Verts.Capacity = output.Uvs.Capacity = output.Colors.Capacity = 4;
            output.Triangles.Capacity = 6;
            var counts = new int[2];
            for (int mode = 0; mode < 2; mode++)
            {
                for (int i = 0; i < 64; i++) FillAndClear(data, output, mode);
                using (var recorder = Unity.Profiling.ProfilerRecorder.StartNew(
                    Unity.Profiling.ProfilerCategory.Internal, "GC.Alloc", 4096,
                    Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
                {
                    var control = new byte[4096];
                    System.GC.KeepAlive(control);
                    recorder.Stop();
                    Assert.IsTrue(recorder.Valid);
                    Assert.Greater(recorder.Count, 0, "Positive allocation control must be visible.");
                    recorder.Reset();
                    recorder.Start();
                    for (int i = 0; i < 256; i++) FillAndClear(data, output, mode);
                    recorder.Stop();
                    counts[mode] = recorder.Count;
                }
            }
            Assert.That(counts, Is.All.EqualTo(0),
                $"Allocation samples: FillToDrawData={counts[0]}, FillWithMatrix={counts[1]}.");
        }
    }

    private static void FillAndClear(UIMeshDataX data, MeshLists output, int mode)
    {
        output.Verts.Clear();
        output.Uvs.Clear();
        output.Colors.Clear();
        output.Triangles.Clear();
        if (mode == 0) data.FillToDrawData(output.Verts, output.Uvs, output.Colors, output.Triangles, Vector3.one);
        else data.FillWithMatrix(output.Verts, output.Uvs, output.Colors, output.Triangles, Matrix4x4.identity);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_026.md
    [Test]
    [Category("UT_MESH_026")]
    public void WriteSimpleQuad_WritesCanonicalSlimGeometry()
    {
        var data = new UIMeshDataX { TextureIndex = 4 };
        data.WriteSimpleQuad(new Vector4(-2, -1, 2, 1), new Vector4(.1f, .2f, .8f, .9f), new Color32(10, 20, 30, 40));

        Assert.AreEqual(4, data.mesh.VertexCount);
        Assert.AreEqual(6, data.mesh.IndicesCount);
        Assert.AreEqual(new Vector3(-2, -1, 0), UIMeshDataX.geometry.vertex[data.mesh.VertexOffset]);
        Assert.AreEqual(new Vector3(2, 1, 0), UIMeshDataX.geometry.vertex[data.mesh.VertexOffset + 2]);
        Assert.AreEqual(new Vector4(.8f, .9f, 4, 0), UIMeshDataX.geometry.uvs[data.mesh.VertexOffset + 2]);
        Assert.AreEqual(data.mesh.VertexOffset, UIMeshDataX.geometry.indices[data.mesh.IndicesOffset]);
        data.Dispose();
    }
    [SetUp]
    public void SetUp()
    {
        UIMeshDataX.geometry.Reset();
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_023.md
    [Test]
    [Category("UT_MESH_023")]
    public void FillVertex_WritesSharedGeometryWithTextureAndFlags()
    {
        var meshData = new UIMeshDataX { TextureIndex = 6 };
        using (var helper = CreateQuad())
        {
            meshData.FillVertex(helper, 9);
        }

        Assert.AreEqual(4, meshData.mesh.VertexCount);
        Assert.AreEqual(6, meshData.mesh.IndicesCount);
        for (int i = meshData.mesh.VertexOffset; i < meshData.mesh.VertexOffset + 4; i++)
        {
            Assert.AreEqual(6, UIMeshDataX.geometry.uvs[i].z);
            Assert.AreEqual(9, UIMeshDataX.geometry.uvs[i].w);
        }

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 3, 0 },
            new[]
            {
                UIMeshDataX.geometry.indices[meshData.mesh.IndicesOffset + 0],
                UIMeshDataX.geometry.indices[meshData.mesh.IndicesOffset + 1],
                UIMeshDataX.geometry.indices[meshData.mesh.IndicesOffset + 2],
                UIMeshDataX.geometry.indices[meshData.mesh.IndicesOffset + 3],
                UIMeshDataX.geometry.indices[meshData.mesh.IndicesOffset + 4],
                UIMeshDataX.geometry.indices[meshData.mesh.IndicesOffset + 5],
            });
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
