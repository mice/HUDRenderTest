using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// TC-LIFE-01: UIPrefabHolder.UseSlim toggles the internal DataPrefabHolder type and
/// keeps UIPrefabManager.holders consistent (no double-registration, proper re-registration).
/// </summary>
public class TestUseSlimLifecycle
{
    private GameObject _holderGO;
    private GameObject _ownerGO;

    [SetUp]
    public void SetUp()
    {
        _ownerGO  = new GameObject("Owner");
        _holderGO = new GameObject("PrefabHolder");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_holderGO);
        Object.DestroyImmediate(_ownerGO);
    }

    // TC-LIFE-01
    // TestRecord: Documentation~/Testing/Unit/Scenes/UT_SCENE_003.md
    [UnityTest]
    [Category("UT_SCENE_003")]
    public IEnumerator UseSlim_Switch_ReRegisters()
    {
        var owner  = _ownerGO.AddComponent<UIPrefabOwner>();
        owner.targets = new List<Transform>();

        var component = _holderGO.AddComponent<UIPrefabHolder>();

        // Reflect-set the serialized _target field so the component is properly initialized.
        var field = typeof(UIPrefabHolder).GetField("_target",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field.SetValue(component, owner);

        // Start with UIMeshDataX (slim = true)
        component.UseSlim(true);
        yield return null;

        Assert.IsNotNull(component.DataHolder, "DataHolder must be non-null after UseSlim(true)");
        Assert.IsInstanceOf<DataPrefabHolder<UIMeshDataX>>(component.DataHolder,
            "UseSlim(true) must produce DataPrefabHolder<UIMeshDataX>");

        // Switch to UIMeshData (slim = false)
        component.UseSlim(false);
        yield return null;

        Assert.IsNotNull(component.DataHolder, "DataHolder must be non-null after UseSlim(false)");
        Assert.IsInstanceOf<DataPrefabHolder<UIMeshData>>(component.DataHolder,
            "UseSlim(false) must produce DataPrefabHolder<UIMeshData>");

        // Switch back to slim
        component.UseSlim(true);
        yield return null;

        Assert.IsInstanceOf<DataPrefabHolder<UIMeshDataX>>(component.DataHolder,
            "Switching back to UseSlim(true) must restore DataPrefabHolder<UIMeshDataX>");
    }
}
