using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class TestVertexHelperUtils
{
    [Test]
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

    [Test]
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
