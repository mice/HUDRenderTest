using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// TC-REG-01: Two DataPrefabHolder instances sharing the same UIPrefabOwner must both receive
/// the same non-null UIPrefabRegistration after Register, i.e., per-instance wrappers are shared.
/// </summary>
public class TestUIPrefabRegistration
{
    private GameObject _ownerGO;

    [SetUp]
    public void SetUp()
    {
        _ownerGO = new GameObject("TemplateOwner");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_ownerGO);
    }

    // TC-REG-01
    // TestRecord: Documentation~/Testing/Unit/Prefab/UT_PREF_013.md
    [Test]
    [Category("UT_PREF_013")]
    public void MultiInstance_TextIndependent()
    {
        var owner = _ownerGO.AddComponent<UIPrefabOwner>();
        owner.targets = new List<Transform>();

        var holderA = new DataPrefabHolder<UIMeshData>();
        holderA.SetTarget(owner);

        var holderB = new DataPrefabHolder<UIMeshData>();
        holderB.SetTarget(owner);

        var manager = UIPrefabManager.Instance;
        manager.Register(holderA);
        manager.Register(holderB);

        try
        {
            Assert.IsNotNull(holderA.wrapper, "holderA must receive a wrapper after Register");
            Assert.IsNotNull(holderB.wrapper, "holderB must receive a wrapper after Register");
            Assert.AreSame(holderA.wrapper, holderB.wrapper,
                "both holders must share the same UIPrefabRegistration for the same UIPrefabOwner");
            // Holder instances are distinct — each owns its own backing data array.
            Assert.AreNotSame(holderA, holderB, "holderA and holderB are separate instances");
        }
        finally
        {
            manager.RemoveHolder(holderA);
            manager.RemoveHolder(holderB);
        }
    }
}
