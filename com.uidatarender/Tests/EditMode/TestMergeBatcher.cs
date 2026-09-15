using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// TC-MB-01: 容量内单批 — batches.Count == 1, no warning.
/// TC-MB-02: 超容量分批 + 告警 — batches.Count >= 2, LogWarning fires.
/// TC-MB-03: 贪心首次适应 — overlapping holders share a batch, no excess splits.
/// </summary>
public class TestMergeBatcher
{
    private sealed class StubMesh : IUIData
    {
        public int TextureIndex { get; set; }
        public void UpdateTextureIndex(int index) => TextureIndex = index;
        public void FillVertex(VertexHelper toFill, int flags) { }
        public void TransformVertex(Matrix4x4 mtx) { }
        public void FillToTriangleData(List<int> triangles_, Vector3 localPosition) { }
        public void FillToDrawData(List<Vector3> vertList, List<Vector4> uvs, List<Color32> colors, List<int> triangles, Vector3 localPosition) { }
        public void FillWithMatrix(List<Vector3> vertList, List<Vector4> uvs, List<Color32> colors, List<int> triangles, Matrix4x4 mtx) { }
        public void Dispose() { }
    }

    private sealed class StubHolder : IUIPrefabHolder
    {
        private readonly IUIData[] _meshes;

        /// <param name="slotIndices">TextureIndex values for this holder's mesh data items.</param>
        public StubHolder(params int[] slotIndices)
        {
            _meshes = new IUIData[slotIndices.Length];
            for (int i = 0; i < slotIndices.Length; i++)
                _meshes[i] = new StubMesh { TextureIndex = slotIndices[i] };
        }

        public IList<IUIData> UIMeshDatas => _meshes;
        public UIPrefabOwner Target => null;
        public Vector3 Position => Vector3.zero;
        public UIPrefabRegistration wrapper => null;
        public void SetTarget(UIPrefabOwner t) { }
        public void SetWrapper(UIPrefabRegistration w) { }
        public void BuildMesh(IUIDrawTarget[] d) { }
        public void Fill(List<Vector3> v, List<Vector4> u, List<Color32> c, List<int> t, Vector3 p) { }
        public void Fill(List<int> t, Vector3 p) { }
    }

    // TC-MB-01: Holders whose combined slot count fits in MaxImageSlots produce a single batch.
    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_004.md
    [Test]
    [Category("UT_BATCH_004")]
    public void Plan_FitsInOneBatch()
    {
        var batcher = new MergeBatcher(maxSlots: 3);
        var holderA = new StubHolder(1, 2);  // slots {1, 2}
        var holderB = new StubHolder(2, 3);  // slots {2, 3}

        var batches = batcher.Plan(new[] { holderA, holderB });

        Assert.AreEqual(1, batches.Count, "combined 3 slots should fit in one batch");
        Assert.AreEqual(2, batches[0].Holders.Count, "both holders in the batch");
        Assert.AreEqual(3, batches[0].UsedSlots.Count, "batch tracks 3 distinct slots");
    }

    // TC-MB-02: When combined slots exceed MaxImageSlots, batches split and LogWarning fires.
    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_005.md
    [Test]
    [Category("UT_BATCH_005")]
    public void Plan_SplitsWhenOver()
    {
        var batcher = new MergeBatcher(maxSlots: 3);
        var holderA = new StubHolder(1, 2, 3);  // fills the batch
        var holderB = new StubHolder(4);         // slot 4 cannot join holderA's batch

        LogAssert.Expect(LogType.Warning, new Regex(@"\[MergeBatcher\] split into 2 batches"));
        var batches = batcher.Plan(new[] { holderA, holderB });

        Assert.AreEqual(2, batches.Count);
        Assert.IsTrue(batches[0].Holders.Contains(holderA));
        Assert.IsTrue(batches[1].Holders.Contains(holderB));
    }

    // TC-MB-03: Greedy first-fit packs overlapping holders into the first available batch.
    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_003.md
    [Test]
    [Category("UT_BATCH_003")]
    public void Plan_FirstFit_MinimizesBatches()
    {
        var batcher = new MergeBatcher(maxSlots: 3);
        var holderA = new StubHolder(1, 2);  // {1,2}
        var holderB = new StubHolder(2, 3);  // {2,3}
        var holderC = new StubHolder(1, 3);  // {1,3} — union {1,2,3}.Count == 3

        var batches = batcher.Plan(new[] { holderA, holderB, holderC });

        Assert.AreEqual(1, batches.Count, "greedy first-fit fits all three in one batch");
        Assert.AreEqual(3, batches[0].Holders.Count);
    }
}
