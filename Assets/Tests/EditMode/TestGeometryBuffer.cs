using NUnit.Framework;
using UnityEngine;

public class TestGeometryBuffer
{
    [Test]
    public void EnsureVertex_KeepsArraysParallel()
    {
        var buffer = new GeometryBuffer(4, 8, 4);

        buffer.EnsureVertexCapacity(7);

        Assert.AreEqual(8, buffer.vertex.Length);
        Assert.AreEqual(buffer.vertex.Length, buffer.drawVertex.Length);
        Assert.AreEqual(buffer.vertex.Length, buffer.uvs.Length);
        Assert.AreEqual(buffer.vertex.Length, buffer.colors.Length);
    }

    [Test]
    public void EnsureIndex_Grows()
    {
        var buffer = new GeometryBuffer(4, 8, 4);

        buffer.EnsureIndexCapacity(9);

        Assert.GreaterOrEqual(buffer.indices.Length, 9);
    }

    [Test]
    public void Grow_PreservesContent()
    {
        var buffer = new GeometryBuffer(4, 8, 4);
        buffer.vertex[0] = new Vector3(1, 2, 3);
        buffer.drawVertex[0] = new Vector3(4, 5, 6);
        buffer.uvs[0] = new Vector4(7, 8, 9, 10);
        buffer.colors[0] = new Color32(11, 12, 13, 14);
        buffer.indices[0] = 15;

        buffer.EnsureVertexCapacity(5);
        buffer.EnsureIndexCapacity(9);

        Assert.AreEqual(new Vector3(1, 2, 3), buffer.vertex[0]);
        Assert.AreEqual(new Vector3(4, 5, 6), buffer.drawVertex[0]);
        Assert.AreEqual(new Vector4(7, 8, 9, 10), buffer.uvs[0]);
        Assert.AreEqual(new Color32(11, 12, 13, 14), buffer.colors[0]);
        Assert.AreEqual(15, buffer.indices[0]);
    }

    [Test]
    public void Grow_UsesGrowthStep()
    {
        var buffer = new GeometryBuffer(4, 8, 4);

        buffer.EnsureVertexCapacity(13);
        buffer.EnsureIndexCapacity(17);

        Assert.AreEqual(16, buffer.vertex.Length);
        Assert.AreEqual(20, buffer.indices.Length);
    }
}
