using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UIData;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class TestHolderBatchRenderer
{
    private sealed class StubMesh : IUIData
    {
        public int TextureIndex { get; set; }
        public void UpdateTextureIndex(int index) => TextureIndex = index;
        public void FillVertex(VertexHelper toFill, int flags) { }
        public void TransformVertex(Matrix4x4 mtx) { }
        public void FillToTriangleData(List<int> triangles, Vector3 localPosition) { }

        public void FillToDrawData(
            List<Vector3> vertices,
            List<Vector4> uvs,
            List<Color32> colors,
            List<int> triangles,
            Vector3 localPosition)
        {
            int offset = vertices.Count;
            vertices.Add(localPosition + new Vector3(0, 0));
            vertices.Add(localPosition + new Vector3(0, 1));
            vertices.Add(localPosition + new Vector3(1, 1));
            vertices.Add(localPosition + new Vector3(1, 0));
            uvs.Add(new Vector4(0, 0, TextureIndex, 0));
            uvs.Add(new Vector4(0, 1, TextureIndex, 0));
            uvs.Add(new Vector4(1, 1, TextureIndex, 0));
            uvs.Add(new Vector4(1, 0, TextureIndex, 0));
            var white = (Color32)Color.white;
            colors.Add(white);
            colors.Add(white);
            colors.Add(white);
            colors.Add(white);
            triangles.Add(offset + 0);
            triangles.Add(offset + 1);
            triangles.Add(offset + 2);
            triangles.Add(offset + 2);
            triangles.Add(offset + 3);
            triangles.Add(offset + 0);
        }

        public void FillWithMatrix(List<Vector3> vertices, List<Vector4> uvs, List<Color32> colors, List<int> triangles, Matrix4x4 mtx) { }
        public void WriteSimpleQuad(Vector4 drawing, Vector4 outerUv, Color32 color) { }
        public void Dispose() { }
    }

    private sealed class StubHolder : IUIPrefabHolder
    {
        private readonly IUIData[] meshes;

        public StubHolder(params int[] slotIndices)
        {
            meshes = new IUIData[slotIndices.Length];
            for (int i = 0; i < slotIndices.Length; i++)
                meshes[i] = new StubMesh { TextureIndex = slotIndices[i] };
        }

        public UIPrefabOwner Target => null;
        public Vector3 Position => Vector3.zero;
        public UIPrefabRegistration wrapper => null;
        public IList<IUIData> UIMeshDatas => meshes;
        public void SetTarget(UIPrefabOwner target) { }
        public void SetWrapper(UIPrefabRegistration wrapper) { }
        public void BuildMesh(IUIDrawTarget[] draws) { }

        public void Fill(List<Vector3> vertices, List<Vector4> uvs, List<Color32> colors, List<int> triangles, Vector3 localPosition)
        {
            foreach (var mesh in meshes)
                mesh.FillToDrawData(vertices, uvs, colors, triangles, localPosition);
        }

        public void Fill(List<int> triangles, Vector3 localPosition) { }

        public void SetTextureIndex(int index, int textureIndex)
        {
            ((StubMesh)meshes[index]).TextureIndex = textureIndex;
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_002.md
    [Test]
    [Category("UT_BATCH_002")]
    public void Rebuild_SplitsThroughMergeBatcher_AndRecordsPerf()
    {
        var renderer = new HolderBatchRenderer(maxSlots: 3, probe: new PerfProbe(windowSize: 4));
        var holders = new IUIPrefabHolder[]
        {
            new StubHolder(1, 2, 3),
            new StubHolder(4),
        };

        LogAssert.Expect(LogType.Warning, new Regex(@"\[MergeBatcher\] split into 2 batches"));
        renderer.Rebuild(holders, null, CreateMaterial, (_, _) => { });

        Assert.AreEqual(2, renderer.BatchCount);
        Assert.AreEqual(1, renderer.Probe.Count);
        Assert.AreEqual(2, renderer.Probe.MaxDrawCalls);

        renderer.Dispose();
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_001.md
    [Test]
    [Category("UT_BATCH_001")]
    public void Rebuild_RemapsGlobalSlotsToBatchLocalSlots()
    {
        var renderer = new HolderBatchRenderer(maxSlots: 3, probe: new PerfProbe(windowSize: 4));
        var holders = new IUIPrefabHolder[] { new StubHolder(4) };

        renderer.Rebuild(holders, null, CreateMaterial, (_, _) => { });

        var uvs = new List<Vector4>();
        renderer.GetBatchMesh(0).GetUVs(0, uvs);
        Assert.AreEqual(4, uvs.Count);
        foreach (var uv in uvs)
            Assert.AreEqual(1, (int)uv.z, "global slot 4 must be remapped to local slot 1 inside the batch");

        renderer.Dispose();
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_007.md
    [Test]
    [Category("UT_BATCH_007")]
    public void Plan_ReusesStableStructure_AndInvalidatesTextureChanges()
    {
        var first = new StubHolder(1, 2);
        var second = new StubHolder(3);
        var holders = new IUIPrefabHolder[] { first, second };
        var batcher = new MergeBatcher(3);

        var initial = batcher.Plan(holders);
        Assert.IsFalse(batcher.LastPlanCacheHit);
        var cached = batcher.Plan(holders);
        Assert.IsTrue(batcher.LastPlanCacheHit);
        Assert.AreSame(initial, cached);

        first.SetTextureIndex(1, 4);
        var changed = batcher.Plan(holders);
        Assert.IsFalse(batcher.LastPlanCacheHit);
        Assert.AreSame(initial, changed);
        Assert.AreEqual(4, changed[0].Holders[0].UIMeshDatas[1].TextureIndex);
        Assert.GreaterOrEqual(batcher.PlanCacheHits, 1);
        Assert.GreaterOrEqual(batcher.PlanCacheMisses, 2);
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_008.md
    [Test]
    [Category("UT_BATCH_008")]
    public void Plan_InvalidatesHolderOrder_AndPreservesLocalSlotMap()
    {
        var first = new StubHolder(4);
        var second = new StubHolder(2);
        var batcher = new MergeBatcher(3);
        var holders = new IUIPrefabHolder[] { first, second };

        var batches = batcher.Plan(holders);
        var map = batches[0].BuildLocalSlotMap();
        Assert.AreEqual(2, map[4]);

        holders[0] = second;
        holders[1] = first;
        batcher.Plan(holders);
        Assert.IsFalse(batcher.LastPlanCacheHit);
        Assert.AreSame(second, batcher.Plan(holders)[0].Holders[0]);
        Assert.IsTrue(batcher.LastPlanCacheHit);
    }

    private static Material CreateMaterial()
    {
        return new Material(Shader.Find("Sprites/Default"));
    }
}
