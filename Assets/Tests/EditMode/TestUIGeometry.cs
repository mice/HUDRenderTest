using NUnit.Framework;

public class TestUIGeometry
{
    [Test]
    public void TestUIGeometryctor()
    {
        var uiGeometry = new UIGeometry();
        Assert.AreEqual(1, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(1, uiGeometry.openVertexList.Count);
        Assert.AreEqual(0, uiGeometry.openVertexList.First.Value.start);
        Assert.AreEqual(uiGeometry.vertex.Length, uiGeometry.openVertexList.First.Value.count);
        Assert.AreEqual(0, uiGeometry.openIndicesList.First.Value.start);
        Assert.AreEqual(uiGeometry.indices.Length, uiGeometry.openIndicesList.First.Value.count);
    }

    [Test]
    public void TestUIGeometryAlloc46()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice = uiGeometry.Alloc(4, 6);
        Assert.AreEqual(4, meshSlice.VertexCount);
        Assert.AreEqual(6, meshSlice.IndicesCount);
    }

    [Test]
    public void TestUIGeometryAllocRelease()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice = uiGeometry.Alloc(4, 6);
        Assert.AreEqual(4, meshSlice.VertexCount);
        Assert.AreEqual(6, meshSlice.IndicesCount);

        uiGeometry.Release(meshSlice);

        AssertFull(uiGeometry);
    }

    [Test]
    public void TestUIGeometryAlloc2T46()
    {
        var uiGeometry = new UIGeometry();
        uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);

        Assert.AreEqual(4, meshSlice2.VertexOffset);
        Assert.AreEqual(4, meshSlice2.VertexCount);
        Assert.AreEqual(6, meshSlice2.IndicesCount);
        Assert.AreEqual(6, meshSlice2.IndicesOffset);

        Assert.AreEqual(1, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(1, uiGeometry.openVertexList.Count);
        Assert.AreEqual(8, uiGeometry.openVertexList.First.Value.start);
        Assert.AreEqual(uiGeometry.vertex.Length - 8, uiGeometry.openVertexList.First.Value.count);
        Assert.AreEqual(12, uiGeometry.openIndicesList.First.Value.start);
        Assert.AreEqual(uiGeometry.indices.Length - 12, uiGeometry.openIndicesList.First.Value.count);
    }

    [Test]
    public void TestUIGeometryAlloc2T46_release()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);

        uiGeometry.Release(meshSlice1);

        Assert.AreEqual(2, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(2, uiGeometry.openVertexList.Count);
        Assert.AreEqual(0, uiGeometry.openVertexList.First.Value.start);
        Assert.AreEqual(4, uiGeometry.openVertexList.First.Value.count);
        Assert.AreEqual(0, uiGeometry.openIndicesList.First.Value.start);
        Assert.AreEqual(6, uiGeometry.openIndicesList.First.Value.count);

        uiGeometry.Release(meshSlice2);

        AssertFull(uiGeometry);
    }

    [Test]
    public void TestUIGeometry_release_case2()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);
        uiGeometry.Alloc(4, 6);

        Assert.AreEqual(1, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(1, uiGeometry.openVertexList.Count);

        uiGeometry.Release(meshSlice2);
        uiGeometry.Release(meshSlice1);

        Assert.AreEqual(2, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(2, uiGeometry.openVertexList.Count);
        Assert.AreEqual(0, uiGeometry.openVertexList.First.Value.start);
        Assert.AreEqual(8, uiGeometry.openVertexList.First.Value.count);
        Assert.AreEqual(0, uiGeometry.openIndicesList.First.Value.start);
        Assert.AreEqual(12, uiGeometry.openIndicesList.First.Value.count);
    }

    [Test]
    public void TestUIGeometry_release_case1()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        uiGeometry.Alloc(4, 6);
        var meshSlice3 = uiGeometry.Alloc(4, 6);

        Assert.AreEqual(1, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(1, uiGeometry.openVertexList.Count);

        uiGeometry.Release(meshSlice3);
        uiGeometry.Release(meshSlice1);

        Assert.AreEqual(2, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(2, uiGeometry.openVertexList.Count);
        Assert.AreEqual(0, uiGeometry.openVertexList.First.Value.start);
        Assert.AreEqual(4, uiGeometry.openVertexList.First.Value.count);
        Assert.AreEqual(0, uiGeometry.openIndicesList.First.Value.start);
        Assert.AreEqual(6, uiGeometry.openIndicesList.First.Value.count);
    }

    [Test]
    public void TestUIGeometry_release_case3()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);
        var meshSlice3 = uiGeometry.Alloc(4, 6);

        Assert.AreEqual(1, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(1, uiGeometry.openVertexList.Count);

        uiGeometry.Release(meshSlice3);
        uiGeometry.Release(meshSlice1);
        uiGeometry.Release(meshSlice2);

        AssertFull(uiGeometry);
    }

    [Test]
    public void TestUIGeometry_release_case4()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);
        uiGeometry.Alloc(4, 6);
        var meshSlice4 = uiGeometry.Alloc(4, 6);

        Assert.AreEqual(1, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(1, uiGeometry.openVertexList.Count);

        uiGeometry.Release(meshSlice4);
        uiGeometry.Release(meshSlice1);
        uiGeometry.Release(meshSlice2);

        Assert.AreEqual(2, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(2, uiGeometry.openVertexList.Count);
        Assert.AreEqual(0, uiGeometry.openVertexList.First.Value.start);
        Assert.AreEqual(8, uiGeometry.openVertexList.First.Value.count);
        Assert.AreEqual(0, uiGeometry.openIndicesList.First.Value.start);
        Assert.AreEqual(12, uiGeometry.openIndicesList.First.Value.count);
    }

    [Test]
    public void TestUIGeometry_release_case5()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        uiGeometry.Alloc(4, 6);
        var meshSlice3 = uiGeometry.Alloc(4, 6);
        var meshSlice4 = uiGeometry.Alloc(4, 6);

        Assert.AreEqual(1, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(1, uiGeometry.openVertexList.Count);

        uiGeometry.Release(meshSlice4);
        uiGeometry.Release(meshSlice1);
        uiGeometry.Release(meshSlice3);

        Assert.AreEqual(2, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(2, uiGeometry.openVertexList.Count);
        Assert.AreEqual(0, uiGeometry.openVertexList.First.Value.start);
        Assert.AreEqual(4, uiGeometry.openVertexList.First.Value.count);
        Assert.AreEqual(0, uiGeometry.openIndicesList.First.Value.start);
        Assert.AreEqual(6, uiGeometry.openIndicesList.First.Value.count);
    }

    [Test]
    public void Alloc_Triggers_Grow()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice = uiGeometry.Alloc(UIGeometry.GROWTH + 1, UIGeometry.GROWTH * 2 + 1);

        Assert.AreEqual(UIGeometry.GROWTH + 1, meshSlice.VertexCount);
        Assert.AreEqual(UIGeometry.GROWTH * 2 + 1, meshSlice.IndicesCount);
        Assert.AreEqual(uiGeometry.vertex.Length, uiGeometry.drawVertex.Length);
        Assert.AreEqual(uiGeometry.vertex.Length, uiGeometry.uvs.Length);
        Assert.AreEqual(uiGeometry.vertex.Length, uiGeometry.colors.Length);
        Assert.GreaterOrEqual(uiGeometry.vertex.Length, meshSlice.VertexCount);
        Assert.GreaterOrEqual(uiGeometry.indices.Length, meshSlice.IndicesCount);

        uiGeometry.Release(meshSlice);

        AssertFull(uiGeometry);
    }

    [Test]
    public void ReAlloc_FreesOldSlice()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice = uiGeometry.Alloc(4, 6);
        uiGeometry.Alloc(4, 6);

        uiGeometry.ReAlloc(8, 12, ref meshSlice);
        var reused = uiGeometry.Alloc(4, 6);

        Assert.AreEqual(0, reused.VertexOffset);
        Assert.AreEqual(0, reused.IndicesOffset);
    }

    [Test]
    public void Release_Case0_Middle_Standalone()
    {
        var uiGeometry = new UIGeometry();
        uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);
        uiGeometry.Alloc(4, 6);
        var meshSlice4 = uiGeometry.Alloc(4, 6);
        uiGeometry.Alloc(4, 6);

        uiGeometry.Release(meshSlice2);
        uiGeometry.Release(meshSlice4);

        Assert.AreEqual(3, uiGeometry.openVertexList.Count);
        Assert.AreEqual(3, uiGeometry.openIndicesList.Count);

        var vertexNode = uiGeometry.openVertexList.First;
        Assert.AreEqual(4, vertexNode.Value.start);
        Assert.AreEqual(4, vertexNode.Value.count);
        vertexNode = vertexNode.Next;
        Assert.AreEqual(12, vertexNode.Value.start);
        Assert.AreEqual(4, vertexNode.Value.count);
        vertexNode = vertexNode.Next;
        Assert.AreEqual(20, vertexNode.Value.start);

        var indexNode = uiGeometry.openIndicesList.First;
        Assert.AreEqual(6, indexNode.Value.start);
        Assert.AreEqual(6, indexNode.Value.count);
        indexNode = indexNode.Next;
        Assert.AreEqual(18, indexNode.Value.start);
        Assert.AreEqual(6, indexNode.Value.count);
        indexNode = indexNode.Next;
        Assert.AreEqual(30, indexNode.Value.start);
    }

    private static void AssertFull(UIGeometry uiGeometry)
    {
        Assert.AreEqual(1, uiGeometry.openIndicesList.Count);
        Assert.AreEqual(1, uiGeometry.openVertexList.Count);
        Assert.AreEqual(0, uiGeometry.openVertexList.First.Value.start);
        Assert.AreEqual(uiGeometry.vertex.Length, uiGeometry.openVertexList.First.Value.count);
        Assert.AreEqual(0, uiGeometry.openIndicesList.First.Value.start);
        Assert.AreEqual(uiGeometry.indices.Length, uiGeometry.openIndicesList.First.Value.count);
    }
}
