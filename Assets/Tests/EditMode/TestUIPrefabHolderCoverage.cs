using NUnit.Framework;
using UnityEngine;

public class TestUIPrefabHolderCoverage
{
    private sealed class TestHolder : UIPrefabHolder
    {
        public void AssignTarget(UIPrefabOwner owner) => _target = owner;
    }

    private sealed class StubDrawTarget : IUIDrawTarget
    {
        public int GenerateCount { get; private set; }
        public Transform LastRoot { get; private set; }

        public void DoGenerate(IUIData meshData, Transform root = null)
        {
            GenerateCount++;
            LastRoot = root;
            meshData.TextureIndex = 2;
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_014.md
    [Test]
    [Category("UT_PREF_014")]
    public void UseSlim_CreatesExpectedHolderType_AndCanSwitch()
    {
        var ownerGo = new GameObject("owner", typeof(UIPrefabOwner));
        var holderGo = new GameObject("holder", typeof(TestHolder));
        try
        {
            var owner = ownerGo.GetComponent<UIPrefabOwner>();
            var holder = holderGo.GetComponent<TestHolder>();
            holder.AssignTarget(owner);

            holder.UseSlim(true);
            Assert.IsInstanceOf<DataPrefabHolder<UIMeshDataX>>(holder.DataHolder);

            holder.UseSlim(false);
            Assert.IsInstanceOf<DataPrefabHolder<UIMeshData>>(holder.DataHolder);
        }
        finally
        {
            Object.DestroyImmediate(holderGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_015.md
    [Test]
    [Category("UT_PREF_015")]
    public void BuildMesh_InitializesDataHolderAndRefreshesGeneratedMeshes()
    {
        var ownerGo = new GameObject("owner", typeof(UIPrefabOwner));
        var holderGo = new GameObject("holder", typeof(TestHolder));
        try
        {
            var owner = ownerGo.GetComponent<UIPrefabOwner>();
            var holder = holderGo.GetComponent<TestHolder>();
            holder.AssignTarget(owner);
            holder.UseSlim(false);

            var draw = new StubDrawTarget();
            holder.BuildMesh(new IUIDrawTarget[] { draw });

            Assert.NotNull(holder.DataHolder);
            Assert.AreEqual(1, holder.DataHolder.UIMeshDatas.Count);
            Assert.AreEqual(1, draw.GenerateCount);
            Assert.AreSame(owner.transform, draw.LastRoot);
            Assert.AreEqual(2, holder.DataHolder.UIMeshDatas[0].TextureIndex);
        }
        finally
        {
            Object.DestroyImmediate(holderGo);
            Object.DestroyImmediate(ownerGo);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_016.md
    [Test]
    [Category("UT_PREF_016")]
    public void NullSafeSetters_DoNotThrowBeforeBuildMesh()
    {
        var holderGo = new GameObject("holder", typeof(TestHolder));
        try
        {
            var holder = holderGo.GetComponent<TestHolder>();

            Assert.DoesNotThrow(() => holder.SetSprite(0, null));
            Assert.DoesNotThrow(() => holder.SetWidth(0, 10));
            Assert.DoesNotThrow(() => holder.SetTextureIndex(0, 1));
        }
        finally
        {
            Object.DestroyImmediate(holderGo);
        }
    }
}
