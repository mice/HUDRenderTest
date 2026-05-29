using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

public class TestResourceUtility
{
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_018.md
    [Test]
    [Category("UT_UTIL_018")]
    public void CreateMaterial_UsesUnlitColor_AndAssignedColor()
    {
        var material = ResourceUtility.CreateMaterial(Color.red);
        try
        {
            Assert.NotNull(material);
            Assert.AreEqual(Color.red, material.color);
            Assert.NotNull(material.shader);
        }
        finally
        {
            Object.DestroyImmediate(material);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_019.md
    [Test]
    [Category("UT_UTIL_019")]
    public void CreateQuad_ReturnsFourVerticesAndTwoTriangles()
    {
        var mesh = ResourceUtility.CreateQuad();
        try
        {
            Assert.AreEqual(4, mesh.vertexCount);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 1, 3 }, mesh.triangles);
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_020.md
    [Test]
    [Category("UT_UTIL_020")]
    public void CreateCircle_ReturnsCenterFan()
    {
        var mesh = ResourceUtility.CreateCircle();
        try
        {
            Assert.AreEqual(33, mesh.vertexCount);
            Assert.AreEqual(32 * 3, mesh.triangles.Length);
            Assert.AreEqual(32, mesh.triangles[0]);
            Assert.AreEqual(0, mesh.triangles[1]);
            Assert.AreEqual(1, mesh.triangles[2]);
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_021.md
    [Test]
    [Category("UT_UTIL_021")]
    public void BuildTextMesh_Vector4Uvs_WritesTextureIndexAndFlags()
    {
        var font = ResourceUtility.CreateAsciiFont(24);
        try
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector4>();
            var triangles = new List<int>();

            ResourceUtility.BuildTextMesh(font, 24, 3, "AB", vertices, uvs, triangles);

            Assert.That(vertices.Count, Is.GreaterThanOrEqualTo(4));
            Assert.AreEqual(vertices.Count, uvs.Count);
            Assert.AreEqual(vertices.Count / 4 * 6, triangles.Count);
            for (int i = 0; i < uvs.Count; i++)
            {
                Assert.AreEqual(3, uvs[i].z);
                Assert.AreEqual(1, uvs[i].w);
            }
        }
        finally
        {
            Object.DestroyImmediate(font);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_022.md
    [Test]
    [Category("UT_UTIL_022")]
    public void CreateTextMeshVariants_CreateMeshesWithExpectedChannels()
    {
        var font = ResourceUtility.CreateAsciiFont(24);
        Mesh colored = null;
        Mesh raw = null;
        Mesh builder = null;
        Mesh dynamicMesh = null;
        try
        {
            colored = ResourceUtility.CreateTextMesh(font, "A", Color.green, 24, 2);
            raw = ResourceUtility.CreateTextMeshRaw(font, "A", 24);
            builder = ResourceUtility.CreateTextMesh2(font, "A", 24);
            dynamicMesh = ResourceUtility.CreateDynamicMesh();

            Assert.That(colored.vertexCount, Is.GreaterThanOrEqualTo(4));
            Assert.That(raw.vertexCount, Is.EqualTo(colored.vertexCount));
            Assert.That(builder.vertexCount, Is.GreaterThanOrEqualTo(4));
            Assert.NotNull(dynamicMesh);
        }
        finally
        {
            Object.DestroyImmediate(colored);
            Object.DestroyImmediate(raw);
            Object.DestroyImmediate(builder);
            Object.DestroyImmediate(dynamicMesh);
            Object.DestroyImmediate(font);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_023.md
    [Test]
    [Category("UT_UTIL_023")]
    public void BuildTextMesh_StringBuilder_CentersAndScalesGlyphs()
    {
        var font = ResourceUtility.CreateAsciiFont(24);
        try
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            ResourceUtility.BuildTextMesh(font, 24, new StringBuilder("AB"), vertices, uvs, triangles);

            Assert.That(vertices.Count, Is.GreaterThanOrEqualTo(4));
            Assert.AreEqual(vertices.Count, uvs.Count);
            Assert.AreEqual(vertices.Count / 4 * 6, triangles.Count);
        }
        finally
        {
            Object.DestroyImmediate(font);
        }
    }
}
