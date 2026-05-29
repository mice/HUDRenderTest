using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class TestLogHygiene
{
    [SetUp]
    public void SetUp()
    {
        UIMeshDataX.geometry.Reset();
    }

    // TestRecord: Documentation~/Testing/Unit/Diagnostics/UT_DIAG_001.md
    [Test]
    [Category("UT_DIAG_001")]
    public void NoUnexpectedErrors_OnHotPath()
    {
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

        var verts = new List<Vector3>();
        var uvs = new List<Vector4>();
        var colors = new List<Color32>();
        var triangles = new List<int>();

        meshData.FillToDrawData(verts, uvs, colors, triangles, Vector3.zero);
        meshDataX.FillToDrawData(verts, uvs, colors, triangles, Vector3.zero);

        LogAssert.NoUnexpectedReceived();
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
}
