using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TestTextureSlotTable
{
    private readonly List<Texture2D> createdTextures = new List<Texture2D>();

    [TearDown]
    public void TearDown()
    {
        foreach (var texture in createdTextures)
        {
            Object.DestroyImmediate(texture);
        }

        createdTextures.Clear();
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Register_First_ReturnsSlot1()
    {
        var table = new TextureSlotTable();
        var texA = CreateTexture("A");

        var slot = table.Register(101, texA);

        Assert.AreEqual(1, slot);
        Assert.AreEqual(1, table.Textures.Count);
    }

    [Test]
    public void Register_SameTexture_SharesSlot()
    {
        var table = new TextureSlotTable();
        var texA = CreateTexture("A");

        var slot1 = table.Register(101, texA);
        var slot2 = table.Register(102, texA);

        Assert.AreEqual(1, slot1);
        Assert.AreEqual(1, slot2);
        Assert.AreEqual(1, table.Textures.Count);
    }

    [Test]
    public void Register_SameOwnerNewTex_ReplacesOld()
    {
        var table = new TextureSlotTable();
        var texA = CreateTexture("A");
        var texB = CreateTexture("B");
        var texC = CreateTexture("C");
        var replaced = (From: -1, To: -1);

        table.SlotReplaced += (from, to) => replaced = (from, to);

        Assert.AreEqual(1, table.Register(101, texA));
        Assert.AreEqual(2, table.Register(102, texB));

        var slot = table.Register(101, texC);

        Assert.AreEqual(2, slot);
        Assert.AreEqual((2, 1), replaced);
        Assert.AreEqual(1, table.GetSlot(texB));
        Assert.AreEqual(2, table.GetSlot(texC));
        Assert.AreEqual(-1, table.GetSlot(texA));
    }

    [Test]
    public void Unregister_TriggersSwapWithLast()
    {
        var table = new TextureSlotTable();
        var texA = CreateTexture("A");
        var texB = CreateTexture("B");
        var texC = CreateTexture("C");
        var replaced = (From: -1, To: -1);

        table.SlotReplaced += (from, to) => replaced = (from, to);

        Assert.AreEqual(1, table.Register(101, texA));
        Assert.AreEqual(2, table.Register(102, texB));
        Assert.AreEqual(3, table.Register(103, texC));

        table.Unregister(102);

        Assert.AreEqual((3, 2), replaced);
        Assert.AreEqual(1, table.GetSlot(texA));
        Assert.AreEqual(2, table.GetSlot(texC));
        Assert.AreEqual(-1, table.GetSlot(texB));
        Assert.AreEqual(2, table.Textures.Count);
    }

    [Test]
    public void Unregister_LastSlot_OnlyRemoved()
    {
        var table = new TextureSlotTable();
        var texA = CreateTexture("A");
        var texB = CreateTexture("B");
        var replacedCalled = false;
        var removedIndex = -1;

        table.SlotReplaced += (_, _) => replacedCalled = true;
        table.SlotRemoved += index => removedIndex = index;

        Assert.AreEqual(1, table.Register(101, texA));
        Assert.AreEqual(2, table.Register(102, texB));

        table.Unregister(102);

        Assert.IsFalse(replacedCalled);
        Assert.AreEqual(2, removedIndex);
        Assert.AreEqual(1, table.GetSlot(texA));
        Assert.AreEqual(-1, table.GetSlot(texB));
        Assert.AreEqual(1, table.Textures.Count);
    }

    [Test]
    public void Register_OverCapacity_RegistersAndWarns()
    {
        var table = new TextureSlotTable(2);
        var texA = CreateTexture("A");
        var texB = CreateTexture("B");
        var texC = CreateTexture("C");

        Assert.AreEqual(1, table.Register(101, texA));
        Assert.AreEqual(2, table.Register(102, texB));

        LogAssert.Expect(LogType.Warning, new Regex(@"\[TextureSlotTable\] texture count exceeds per-batch limit"));
        var slot = table.Register(103, texC);

        Assert.AreEqual(3, slot, "over-capacity texture should be registered at next slot");
        Assert.AreEqual(3, table.Textures.Count);
        Assert.AreEqual(3, table.GetSlot(texC));
    }

    [Test]
    public void Register_OverCapacity_UnregisterReassignsSlot()
    {
        var table = new TextureSlotTable(2);
        var texA = CreateTexture("A");
        var texB = CreateTexture("B");
        var texC = CreateTexture("C");

        table.Register(101, texA);
        table.Register(102, texB);

        LogAssert.Expect(LogType.Warning, new Regex(@"\[TextureSlotTable\] texture count exceeds per-batch limit"));
        Assert.AreEqual(3, table.Register(103, texC));

        // Unregistering texB causes texC (slot 3) to swap into slot 2
        table.Unregister(102);

        Assert.AreEqual(1, table.GetSlot(texA));
        Assert.AreEqual(2, table.GetSlot(texC));
        Assert.AreEqual(-1, table.GetSlot(texB));
        Assert.AreEqual(2, table.Textures.Count);
    }

    // TC-TST-07: ExpandTo increases MaxImageSlots and allows registering beyond the default limit.
    [Test]
    public void ExpandTo_IncreasesCapacityAndAllowsRegistration()
    {
        var table = new TextureSlotTable(maxImageSlots: 2);
        var texA = CreateTexture("A");
        var texB = CreateTexture("B");
        var texC = CreateTexture("C");

        table.Register(101, texA);
        table.Register(102, texB);

        var expanded = table.ExpandTo(4);
        Assert.IsTrue(expanded, "ExpandTo should return true when capacity increases");
        Assert.AreEqual(4, table.MaxImageSlots);

        var slotC = table.Register(103, texC);
        Assert.AreEqual(3, slotC, "third texture should be registered at slot 3 after expansion");
        Assert.AreEqual(3, table.Textures.Count);
    }

    // TC-TST-08: ExpandTo is capped at MaxSupportedImageSlots (7); values ≤ current are no-ops.
    [Test]
    public void ExpandTo_CappedAt7_AndNoOpBelowCurrent()
    {
        var table = new TextureSlotTable(maxImageSlots: 3);

        var noop = table.ExpandTo(2);
        Assert.IsFalse(noop, "ExpandTo(2) on a table with max=3 should be no-op");
        Assert.AreEqual(3, table.MaxImageSlots, "MaxImageSlots must not decrease");

        var capped = table.ExpandTo(100);
        Assert.IsTrue(capped, "ExpandTo(100) should succeed (capped to 7)");
        Assert.AreEqual(7, table.MaxImageSlots, "MaxImageSlots capped at supported maximum");
    }

    [Test]
    public void SetMaxImageSlots_BeforeRegister_AvoidsOverCapacityWarning()
    {
        var table = new TextureSlotTable(maxImageSlots: 3);

        table.SetMaxImageSlots(7);

        for (int i = 0; i < 6; i++)
        {
            var texture = CreateTexture($"T{i}");
            var slot = table.Register(100 + i, texture);
            Assert.AreEqual(i + 1, slot);
        }

        Assert.AreEqual(7, table.MaxImageSlots);
        Assert.AreEqual(6, table.Textures.Count);
    }

    [Test]
    public void SetMaxImageSlots_CanReduceCapacityForFutureWarnings()
    {
        var table = new TextureSlotTable(maxImageSlots: 7);
        var texA = CreateTexture("A");
        var texB = CreateTexture("B");
        var texC = CreateTexture("C");
        var texD = CreateTexture("D");

        table.SetMaxImageSlots(3);

        Assert.AreEqual(1, table.Register(101, texA));
        Assert.AreEqual(2, table.Register(102, texB));
        Assert.AreEqual(3, table.Register(103, texC));

        LogAssert.Expect(LogType.Warning, new Regex(@"\[TextureSlotTable\] texture count exceeds per-batch limit"));
        Assert.AreEqual(4, table.Register(104, texD));
        Assert.AreEqual(3, table.MaxImageSlots);
    }

    private Texture2D CreateTexture(string name)
    {
        var texture = new Texture2D(2, 2) { name = name };
        createdTextures.Add(texture);
        return texture;
    }
}
