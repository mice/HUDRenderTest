using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public class TestMeshVertexBufferLayout
{
    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_015.md
    [Test]
    [Category("UT_MESH_015")]
    public void OneStreamLayout_OrdersColorBeforeTexCoord0()
    {
        var mesh = new Mesh();
        try
        {
            var layout = new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            };

            mesh.SetVertexBufferParams(1, layout);

            Assert.AreEqual(1, mesh.vertexBufferCount);
            Assert.AreEqual(32, mesh.GetVertexBufferStride(0));
            Assert.AreEqual(0, mesh.GetVertexAttributeOffset(VertexAttribute.Position));
            Assert.AreEqual(12, mesh.GetVertexAttributeOffset(VertexAttribute.Color));
            Assert.AreEqual(16, mesh.GetVertexAttributeOffset(VertexAttribute.TexCoord0));
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }
}
