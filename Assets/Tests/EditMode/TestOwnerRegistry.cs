using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// TC-OR-01: GetOrCreate returns the same UIPrefabRegistration for the same UIPrefabOwner.
/// TC-OR-02: TryGet returns false before creation and the correct reg after GetOrCreate.
/// </summary>
public class TestOwnerRegistry
{
    private GameObject _go;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("OwnerRegistryTest");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
    }

    // TC-OR-01: Two GetOrCreate calls with the same owner return the exact same registration.
    [Test]
    public void GetOrCreate_SameOwner_ReturnsSameRegistration()
    {
        var owner = _go.AddComponent<UIPrefabOwner>();
        owner.targets = new List<Transform>();
        var registry = new OwnerRegistry();

        var reg1 = registry.GetOrCreate(owner, null);
        var reg2 = registry.GetOrCreate(owner, null);

        Assert.IsNotNull(reg1, "registration must not be null");
        Assert.AreSame(reg1, reg2, "same owner must return the same UIPrefabRegistration instance");
    }

    // TC-OR-02: TryGet returns false for an unknown owner; after GetOrCreate it returns true and
    // the same instance.
    [Test]
    public void TryGet_ReturnsFalseBeforeCreate_TrueAfter()
    {
        var owner = _go.AddComponent<UIPrefabOwner>();
        owner.targets = new List<Transform>();
        var registry = new OwnerRegistry();

        bool foundBefore = registry.TryGet(owner, out var regBefore);
        Assert.IsFalse(foundBefore, "TryGet should return false for an owner that was never registered");
        Assert.IsNull(regBefore);

        var created = registry.GetOrCreate(owner, null);

        bool foundAfter = registry.TryGet(owner, out var regAfter);
        Assert.IsTrue(foundAfter, "TryGet should return true after GetOrCreate");
        Assert.AreSame(created, regAfter, "TryGet must return the same instance as GetOrCreate");
    }
}
