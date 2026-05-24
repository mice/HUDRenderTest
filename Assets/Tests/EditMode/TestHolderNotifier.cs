using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TC-HN-01: SlotReplaced notifies only affected IUIData.
/// TC-HN-02: SlotRemoved sets TextureIndex to -1 on affected IUIData only.
/// TC-HN-03: Untrack → TextureIndex change → Track; old-slot events silenced, new-slot events routed.
/// TC-HN-04: AddHolder + RemoveHolder fully clears reverse-index; post-removal events silent.
/// </summary>
public class TestHolderNotifier
{
    private sealed class StubMesh : IUIData
    {
        public int TextureIndex { get; set; }
        public int UpdateCallCount { get; private set; }

        public void UpdateTextureIndex(int index)
        {
            TextureIndex = index;
            UpdateCallCount++;
        }

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
        public StubHolder(params IUIData[] meshes) => _meshes = meshes;
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

    private TextureSlotTable _table;
    private HolderNotifier _notifier;

    [SetUp]
    public void SetUp()
    {
        _table = new TextureSlotTable(maxImageSlots: 3);
        _notifier = new HolderNotifier(_table);
    }

    // TC-HN-01: SlotReplaced only calls UpdateTextureIndex on meshes mapped to the 'from' slot.
    [Test]
    public void SlotReplaced_TargetedNotify()
    {
        var meshFrom = new StubMesh { TextureIndex = 1 };
        var meshOther = new StubMesh { TextureIndex = 2 };
        _notifier.Track(meshFrom);
        _notifier.Track(meshOther);

        _notifier.ReplaceTextureID(from: 1, to: 3);

        Assert.AreEqual(1, meshFrom.UpdateCallCount, "affected mesh should be notified once");
        Assert.AreEqual(3, meshFrom.TextureIndex, "affected mesh TextureIndex should update to 3");
        Assert.AreEqual(0, meshOther.UpdateCallCount, "non-matching mesh must not be notified");
        Assert.AreEqual(2, meshOther.TextureIndex, "non-matching mesh TextureIndex must be unchanged");
    }

    // TC-HN-02: SlotRemoved sets TextureIndex = -1 only on meshes mapped to the given slot.
    [Test]
    public void SlotRemoved_SetsMinusOne()
    {
        var meshTarget = new StubMesh { TextureIndex = 1 };
        var meshOther = new StubMesh { TextureIndex = 2 };
        _notifier.Track(meshTarget);
        _notifier.Track(meshOther);

        _notifier.RemoveTextureID(1);

        Assert.AreEqual(-1, meshTarget.TextureIndex, "removed slot mesh should have TextureIndex = -1");
        Assert.AreEqual(2, meshOther.TextureIndex, "non-matching mesh TextureIndex must be unchanged");
        Assert.AreEqual(0, meshOther.UpdateCallCount, "non-matching mesh must not be notified");
    }

    // Verifies the event subscription chain: TextureSlotTable.SlotReplaced → HolderNotifier.
    [Test]
    public void SlotReplaced_ViaTSlotEvent_UpdatesMesh()
    {
        var texA = new Texture2D(2, 2) { name = "A" };
        var texB = new Texture2D(2, 2) { name = "B" };
        var texC = new Texture2D(2, 2) { name = "C" };

        try
        {
            _table.Register(1, texA);
            _table.Register(2, texB);
            _table.Register(3, texC);

            var meshC = new StubMesh { TextureIndex = 3 };
            var meshA = new StubMesh { TextureIndex = 1 };
            _notifier.Track(meshC);
            _notifier.Track(meshA);

            // Unregistering owner 1 (texA at slot 1) triggers swap-with-last:
            // texC moves from slot 3 → slot 1; fires SlotReplaced(from=3, to=1).
            _table.Unregister(1);

            Assert.AreEqual(1, meshC.TextureIndex, "texC moved to slot 1 – meshC should update");
            Assert.AreEqual(0, meshA.UpdateCallCount, "meshA (slot 1, texA) must not be notified via SlotReplaced");
        }
        finally
        {
            Object.DestroyImmediate(texA);
            Object.DestroyImmediate(texB);
            Object.DestroyImmediate(texC);
        }
    }

    // Verifies the event subscription chain: TextureSlotTable.SlotRemoved → HolderNotifier.
    [Test]
    public void SlotRemoved_ViaTSlotEvent_SetsMinusOne()
    {
        var texA = new Texture2D(2, 2) { name = "A" };
        var texB = new Texture2D(2, 2) { name = "B" };

        try
        {
            _table.Register(1, texA);
            _table.Register(2, texB);

            var meshB = new StubMesh { TextureIndex = 2 };
            var meshA = new StubMesh { TextureIndex = 1 };
            _notifier.Track(meshB);
            _notifier.Track(meshA);

            // Unregistering last slot fires SlotRemoved(2).
            _table.Unregister(2);

            Assert.AreEqual(-1, meshB.TextureIndex, "last-slot mesh should be set to -1");
            Assert.AreEqual(0, meshA.UpdateCallCount, "non-matching mesh must not be notified");
        }
        finally
        {
            Object.DestroyImmediate(texA);
            Object.DestroyImmediate(texB);
        }
    }

    // TC-HN-03: After Untrack → TextureIndex change → Track (simulating SetSprite/UntrackMesh+TrackMesh),
    // events at the OLD slot no longer reach the mesh; events at the NEW slot do.
    [Test]
    public void Retrack_AfterTextureIndexChange_SlotEventHitsNewSlotOnly()
    {
        var mesh = new StubMesh { TextureIndex = 1 };
        _notifier.Track(mesh);

        // Simulate UIPrefabManager.UntrackMesh → DoGenerate → TrackMesh
        _notifier.Untrack(mesh);   // remove from old slot while TextureIndex is still 1
        mesh.TextureIndex = 2;     // DoGenerate writes new slot
        _notifier.Track(mesh);     // register at new slot

        // Old-slot event must NOT affect mesh
        _notifier.RemoveTextureID(index: 1);
        Assert.AreEqual(2, mesh.TextureIndex,    "mesh is no longer at slot 1; must not be changed by slot-1 removal");
        Assert.AreEqual(0, mesh.UpdateCallCount, "no UpdateTextureIndex call from old slot");

        // New-slot event MUST affect mesh
        _notifier.RemoveTextureID(index: 2);
        Assert.AreEqual(-1, mesh.TextureIndex,   "mesh at new slot 2 must be set to -1 on removal");
        Assert.AreEqual(1, mesh.UpdateCallCount, "exactly one UpdateTextureIndex call from new slot");
    }

    // TC-HN-04: AddHolder registers all holder meshes; RemoveHolder clears all tracking;
    // subsequent slot events do not reach removed meshes.
    [Test]
    public void RemoveHolder_AfterAddHolder_ClearsAllMeshTracking()
    {
        var mesh1 = new StubMesh { TextureIndex = 1 };
        var mesh2 = new StubMesh { TextureIndex = 2 };
        var holder = new StubHolder(mesh1, mesh2);

        _notifier.AddHolder(holder);

        // Verify tracking is active before removal
        _notifier.ReplaceTextureID(from: 1, to: 3);
        Assert.AreEqual(3, mesh1.TextureIndex, "mesh1 must update when tracked");
        Assert.AreEqual(1, mesh1.UpdateCallCount);

        _notifier.RemoveHolder(holder);

        int count1 = mesh1.UpdateCallCount;
        int count2 = mesh2.UpdateCallCount;

        // After removal, no slot events should reach either mesh
        _notifier.ReplaceTextureID(from: 3, to: 5); // mesh1 was at slot 3
        _notifier.RemoveTextureID(index: 2);         // mesh2 was at slot 2
        Assert.AreEqual(count1, mesh1.UpdateCallCount, "mesh1 must not be notified after RemoveHolder");
        Assert.AreEqual(count2, mesh2.UpdateCallCount, "mesh2 must not be notified after RemoveHolder");
    }
}

