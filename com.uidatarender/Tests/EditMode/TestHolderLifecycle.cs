using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TC-HL-01: Add marks a holder as in-lifecycle; Contains returns true.
/// TC-HL-02: Remove clears both lifecycle and notifier tracking.
/// TC-HL-03: Remove on a never-added holder is a safe no-op.
/// TC-HL-04: Track adds to lifecycle + notifier atomically; subsequent slot events reach the mesh.
/// </summary>
public class TestHolderLifecycle
{
    private sealed class StubMesh : IUIData
    {
        public int TextureIndex { get; set; }
        public void UpdateTextureIndex(int index) { TextureIndex = index; }
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

    private HolderLifecycle _lifecycle;
    private HolderNotifier _notifier;

    [SetUp]
    public void SetUp()
    {
        var table = new TextureSlotTable(maxImageSlots: 3);
        _notifier = new HolderNotifier(table);
        _lifecycle = new HolderLifecycle(_notifier);
    }

    // TC-HL-01: Add places the holder in the lifecycle set.
    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_001.md
    [Test]
    [Category("UT_PREF_001")]
    public void Add_MarksHolderInLifecycle()
    {
        var holder = new StubHolder();
        Assert.IsFalse(_lifecycle.Contains(holder), "pre-condition: holder not yet in lifecycle");

        _lifecycle.Add(holder);

        Assert.IsTrue(_lifecycle.Contains(holder), "holder should be in lifecycle after Add");
    }

    // TC-HL-02: Remove removes from lifecycle and clears notifier tracking so slot events no
    // longer reach the holder's meshes.
    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_002.md
    [Test]
    [Category("UT_PREF_002")]
    public void Remove_AfterTrack_ClearsLifecycleAndNotifier()
    {
        var mesh = new StubMesh { TextureIndex = 1 };
        var holder = new StubHolder(mesh);

        _lifecycle.Track(holder);
        _notifier.ReplaceTextureID(from: 1, to: 2);
        Assert.AreEqual(2, mesh.TextureIndex, "pre-condition: mesh is tracked and receives events");

        _lifecycle.Remove(holder);
        Assert.IsFalse(_lifecycle.Contains(holder), "holder must not be in lifecycle after Remove");

        mesh.TextureIndex = 1;
        _notifier.ReplaceTextureID(from: 1, to: 99);
        Assert.AreEqual(1, mesh.TextureIndex, "mesh must not be reachable by notifier after Remove");
    }

    // TC-HL-03: Remove on a holder that was never added is a safe no-op (must not throw).
    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_003.md
    [Test]
    [Category("UT_PREF_003")]
    public void Remove_WhenNotAdded_IsNoOp()
    {
        var holder = new StubHolder();
        Assert.DoesNotThrow(() => _lifecycle.Remove(holder),
            "Remove on an unknown holder must not throw");
        Assert.IsFalse(_lifecycle.Contains(holder));
    }

    // TC-HL-04: Track adds holder to both the lifecycle set and the notifier atomically.
    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_004.md
    [Test]
    [Category("UT_PREF_004")]
    public void Track_RegistersMeshInNotifier_AndLifecycle()
    {
        var mesh = new StubMesh { TextureIndex = 2 };
        var holder = new StubHolder(mesh);

        _lifecycle.Track(holder);

        Assert.IsTrue(_lifecycle.Contains(holder), "holder must be in lifecycle after Track");

        _notifier.RemoveTextureID(2);
        Assert.AreEqual(-1, mesh.TextureIndex,
            "mesh should receive slot-removal event via notifier after Track");
    }
}
