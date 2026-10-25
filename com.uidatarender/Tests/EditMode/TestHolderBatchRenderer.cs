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
        private IUIData[] meshes;

        public StubHolder(params int[] slotIndices)
        {
            meshes = new IUIData[slotIndices.Length];
            for (int i = 0; i < slotIndices.Length; i++)
                meshes[i] = new StubMesh { TextureIndex = slotIndices[i] };
        }

        public StubHolder(IUIData mesh) { meshes = new[] { mesh }; }

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

        public void AddMesh(int textureIndex)
        {
            System.Array.Resize(ref meshes, meshes.Length + 1);
            meshes[meshes.Length - 1] = new StubMesh { TextureIndex = textureIndex };
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

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_009.md
    [Test]
    [Category("UT_BATCH_009")]
    public void Rebuild_GeometryOnlyChangesReusePlan_AndMatchColdOutput()
    {
        foreach (bool slim in new[] { false, true })
        {
            using (IUIData data = slim ? (IUIData)new UIMeshDataX() : new UIMeshData())
            using (var warm = new HolderBatchRenderer(3))
            using (var cold = new HolderBatchRenderer(3))
            {
                data.TextureIndex = 4;
                data.WriteSimpleQuad(new Vector4(-1, -1, 1, 1), new Vector4(0, 0, 1, 1), Color.white);
                var holder = new StubHolder(data);
                var holders = new IUIPrefabHolder[] { holder, new StubHolder(2) };
                var positions = new Dictionary<IUIPrefabHolder, Vector3> { [holder] = Vector3.one };
                var planner = new MergeBatcher(3);
                var batch = planner.Plan(holders)[0];
                var map = batch.BuildLocalSlotMap();
                warm.Rebuild(holders, positions, CreateMaterial, (_, _) => { });
                var oldVertices = warm.GetBatchMesh(0).vertices;
                var color = new Color32(10, 20, 30, 40);
                data.WriteSimpleQuad(new Vector4(-2, -3, 4, 5), new Vector4(.2f, .3f, .7f, .8f), color);
                data.TransformVertex(Matrix4x4.TRS(new Vector3(2, 3, 0),
                    Quaternion.Euler(0, 0, 25), new Vector3(2, 1, 1)));
                positions[holder] = new Vector3(9, -4, 2);
                Assert.AreSame(batch, planner.Plan(holders)[0]);
                Assert.IsTrue(planner.LastPlanCacheHit);
                Assert.AreSame(map, batch.BuildLocalSlotMap());
                warm.Rebuild(holders, positions, CreateMaterial, (_, _) => { });
                cold.Rebuild(holders, positions, CreateMaterial, (_, _) => { });
                Assert.IsTrue(warm.LastPlanCacheHit);
                Assert.IsFalse(cold.LastPlanCacheHit);
                var actual = warm.GetBatchMesh(0);
                var expected = cold.GetBatchMesh(0);
                CollectionAssert.AreEqual(expected.vertices, actual.vertices);
                CollectionAssert.AreEqual(expected.colors32, actual.colors32);
                CollectionAssert.AreEqual(expected.triangles, actual.triangles);
                var actualUvs = new List<Vector4>();
                var expectedUvs = new List<Vector4>();
                actual.GetUVs(0, actualUvs);
                expected.GetUVs(0, expectedUvs);
                CollectionAssert.AreEqual(expectedUvs, actualUvs);
                Assert.AreEqual(expected.bounds, actual.bounds);
                Assert.AreNotEqual(oldVertices[0], actual.vertices[0], "Warm output must contain the new geometry.");
                Assert.AreEqual(color, actual.colors32[0]);
                Assert.AreEqual(new Vector4(.2f, .3f, 2, 0), actualUvs[0]);
                Assert.AreEqual(1, actualUvs[4].z, "Global slot 2 precedes slot 4.");
            }
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_010.md
    [Test]
    [Category("UT_BATCH_010")]
    public void Plan_WarmAndColdPartitionsMatch_AndCollectionChangesInvalidate()
    {
        var first = new StubHolder(4, 2);
        var second = new StubHolder(7);
        var third = new StubHolder(2);
        var holders = new List<IUIPrefabHolder> { first, second, third };
        var warm = new MergeBatcher(2);
        LogAssert.Expect(LogType.Warning, new Regex(@"\[MergeBatcher\] split into 2 batches"));
        warm.Plan(holders);
        var cached = warm.Plan(holders);
        Assert.IsTrue(warm.LastPlanCacheHit);
        LogAssert.Expect(LogType.Warning, new Regex(@"\[MergeBatcher\] split into 2 batches"));
        var fresh = new MergeBatcher(2).Plan(holders);
        Assert.AreEqual(fresh.Count, cached.Count);
        for (int i = 0; i < fresh.Count; i++)
        {
            CollectionAssert.AreEqual(fresh[i].Holders, cached[i].Holders);
            CollectionAssert.AreEquivalent(fresh[i].UsedSlots, cached[i].UsedSlots);
            var expectedMap = fresh[i].BuildLocalSlotMap();
            var actualMap = cached[i].BuildLocalSlotMap();
            Assert.AreEqual(expectedMap.Count, actualMap.Count);
            foreach (var entry in expectedMap) Assert.AreEqual(entry.Value, actualMap[entry.Key]);
        }
        Assert.AreSame(first, cached[0].Holders[0]);
        Assert.AreSame(third, cached[0].Holders[1]);
        Assert.AreSame(second, cached[1].Holders[0]);
        Assert.AreEqual(1, cached[0].BuildLocalSlotMap()[2]);
        Assert.AreEqual(2, cached[0].BuildLocalSlotMap()[4]);

        // Isolate mesh count and holder identity changes while preserving the slot set.
        first.AddMesh(2);
        LogAssert.Expect(LogType.Warning, new Regex(@"\[MergeBatcher\] split into 2 batches"));
        warm.Plan(holders);
        Assert.IsFalse(warm.LastPlanCacheHit);
        holders[0] = new StubHolder(4, 2, 2);
        LogAssert.Expect(LogType.Warning, new Regex(@"\[MergeBatcher\] split into 2 batches"));
        warm.Plan(holders);
        Assert.IsFalse(warm.LastPlanCacheHit);
        holders.RemoveAt(1);
        warm.Plan(holders);
        Assert.IsFalse(warm.LastPlanCacheHit);
        holders.Add(new StubHolder(4));
        warm.Plan(holders);
        Assert.IsFalse(warm.LastPlanCacheHit);
        holders.Clear();
        Assert.AreEqual(0, warm.Plan(holders).Count);
    }

    private static Material CreateMaterial()
    {
        return new Material(Shader.Find("Sprites/Default"));
    }
}
