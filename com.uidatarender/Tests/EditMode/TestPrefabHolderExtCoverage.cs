using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class TestPrefabHolderExtCoverage
{
    private sealed class StubMesh : IUIData
    {
        public int TextureIndex { get; set; }
        public int UpdateCount { get; private set; }

        public void UpdateTextureIndex(int index)
        {
            TextureIndex = index;
            UpdateCount++;
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
        private readonly IList<IUIData> meshes;

        public StubHolder(params IUIData[] meshes)
        {
            this.meshes = meshes;
        }

        public UIPrefabOwner Target { get; private set; }
        public Vector3 Position => Vector3.zero;
        public UIPrefabRegistration wrapper => null;
        public IList<IUIData> UIMeshDatas => meshes;
        public void SetTarget(UIPrefabOwner target) => Target = target;
        public void SetWrapper(UIPrefabRegistration wrapper) { }
        public void BuildMesh(IUIDrawTarget[] draws) { }
        public void Fill(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Vector3 localPosition) { }
        public void Fill(List<int> triangles_, Vector3 localPosition) { }
    }

    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_017.md
    [Test]
    [Category("UT_PREF_017")]
    public void NullHolder_ExtensionSettersAreNoOp()
    {
        IUIPrefabHolder holder = null;

        Assert.DoesNotThrow(() => holder.SetText(0, "x"));
        Assert.DoesNotThrow(() => holder.SetSprite(0, null));
        Assert.DoesNotThrow(() => holder.SetWidth(0, 10));
        Assert.DoesNotThrow(() => holder.SetTextureIndex(0, 2));
    }

    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_018.md
    [Test]
    [Category("UT_PREF_018")]
    public void SetTextureIndex_UpdatesMeshWhenIndexExists()
    {
        var mesh = new StubMesh();
        var holder = new StubHolder(mesh);

        holder.SetTextureIndex(0, 5);

        Assert.AreEqual(5, mesh.TextureIndex);
        Assert.AreEqual(1, mesh.UpdateCount);
    }

    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_019.md
    [Test]
    [Category("UT_PREF_019")]
    public void SetTextureIndex_IgnoresOutOfRangeIndex()
    {
        var mesh = new StubMesh();
        var holder = new StubHolder(mesh);

        holder.SetTextureIndex(3, 5);

        Assert.AreEqual(0, mesh.UpdateCount);
    }
}
