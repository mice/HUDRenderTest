using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TestUIGeometry
{
    [Test]
    public void TestUIGeometryctor()
    {
        var uiGeometry = new UIGeometry();
        Assert.AreEqual(uiGeometry.openIndicesList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.count, uiGeometry.vertex.Length);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.count, uiGeometry.indices.Length);
    }

    // A Test behaves as an ordinary method
    [Test]
    public void TestUIGeometryAlloc46()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice = uiGeometry.Alloc(4, 6);
        Assert.AreEqual(meshSlice.VertexCount, 4);
        Assert.AreEqual(meshSlice.IndicesCount, 6);
    }

    [Test]
    public void TestUIGeometryAllocRelease()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice = uiGeometry.Alloc(4, 6);
        Assert.AreEqual(meshSlice.VertexCount, 4);
        Assert.AreEqual(meshSlice.IndicesCount, 6);
        uiGeometry.Release(meshSlice);
        Assert.AreEqual(uiGeometry.openIndicesList.Count ,1);
        Assert.AreEqual(uiGeometry.openVertexList.Count ,1);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.count, uiGeometry.vertex.Length);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.count, uiGeometry.indices.Length);

    }

    [Test]
    public void TestUIGeometryAlloc2T46()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);
        Assert.AreEqual(meshSlice2.VertexOffset, 4);
        Assert.AreEqual(meshSlice2.VertexCount, 4);
        Assert.AreEqual(meshSlice2.IndicesCount, 6);
        Assert.AreEqual(meshSlice2.IndicesOffset, 6);

        Assert.AreEqual(uiGeometry.openIndicesList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.start, 8);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.count, uiGeometry.vertex.Length-8);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.start, 12);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.count, uiGeometry.indices.Length-12);
    }

    [Test]
    public void TestUIGeometryAlloc2T46_release()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);

        var meshSlice2 = uiGeometry.Alloc(4, 6);

        uiGeometry.Release(meshSlice1);

        Assert.AreEqual(uiGeometry.openIndicesList.Count,2);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 2);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.count, 4);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.count, 6);

        uiGeometry.Release(meshSlice2);
        Assert.AreEqual(uiGeometry.openIndicesList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.count, uiGeometry.vertex.Length);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.count, uiGeometry.indices.Length);
    }

    [Test]
    //case2 合并头部
    public void TestUIGeometry_release_case2()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);
        var meshSlice3 = uiGeometry.Alloc(4, 6);

        Assert.AreEqual(uiGeometry.openIndicesList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 1);

        uiGeometry.Release(meshSlice2);
        uiGeometry.Release(meshSlice1);
        Assert.AreEqual(uiGeometry.openIndicesList.Count, 2);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 2);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.count, 8);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.count, 12);
    }

    [Test]
    //case1 插入头部
    public void TestUIGeometry_release_case1()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);
        var meshSlice3 = uiGeometry.Alloc(4, 6);


        Assert.AreEqual(uiGeometry.openIndicesList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 1);

        uiGeometry.Release(meshSlice3);
        uiGeometry.Release(meshSlice1);

        Assert.AreEqual(uiGeometry.openIndicesList.Count, 2);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 2);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.count, 4);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.count, 6);
    }

    [Test]
    //case3 ,两头链接
    public void TestUIGeometry_release_case3()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);
        var meshSlice3 = uiGeometry.Alloc(4, 6);


        Assert.AreEqual(uiGeometry.openIndicesList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 1);

        uiGeometry.Release(meshSlice3);
        uiGeometry.Release(meshSlice1);
        uiGeometry.Release(meshSlice2);

        AssetFull(uiGeometry);
    }

    [Test]
    //case3 ,连接前,不链接后面
    public void TestUIGeometry_release_case4()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);
        var meshSlice3 = uiGeometry.Alloc(4, 6);
        var meshSlice4 = uiGeometry.Alloc(4, 6);


        Assert.AreEqual(uiGeometry.openIndicesList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 1);


        uiGeometry.Release(meshSlice4);
        uiGeometry.Release(meshSlice1);
        uiGeometry.Release(meshSlice2);

        Assert.AreEqual(uiGeometry.openIndicesList.Count, 2);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 2);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.count, 8);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.count, 12);
    }

    [Test]
    //case3 ,连接后,不链接前面
    public void TestUIGeometry_release_case5()
    {
        var uiGeometry = new UIGeometry();
        var meshSlice1 = uiGeometry.Alloc(4, 6);
        var meshSlice2 = uiGeometry.Alloc(4, 6);
        var meshSlice3 = uiGeometry.Alloc(4, 6);
        var meshSlice4 = uiGeometry.Alloc(4, 6);


        Assert.AreEqual(uiGeometry.openIndicesList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 1);


        uiGeometry.Release(meshSlice4);
        uiGeometry.Release(meshSlice1);
        uiGeometry.Release(meshSlice3);

        Assert.AreEqual(uiGeometry.openIndicesList.Count, 2);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 2);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.count, 4);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.count, 6);
    }

    private void AssetFull(UIGeometry uiGeometry)
    {
        Assert.AreEqual(uiGeometry.openIndicesList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.Count, 1);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openVertexList.First.Value.count, uiGeometry.vertex.Length);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.start, 0);
        Assert.AreEqual(uiGeometry.openIndicesList.First.Value.count, uiGeometry.indices.Length);

    }
}
