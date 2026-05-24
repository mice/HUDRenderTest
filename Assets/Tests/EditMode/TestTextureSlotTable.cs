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
    public void Register_OverCapacity_ReturnsMinusOneAndWarn()
    {
        var table = new TextureSlotTable(2);
        var texA = CreateTexture("A");
        var texB = CreateTexture("B");
        var texC = CreateTexture("C");

        Assert.AreEqual(1, table.Register(101, texA));
        Assert.AreEqual(2, table.Register(102, texB));

        LogAssert.Expect(LogType.Warning, new Regex(@"\[TextureSlotTable\] over capacity"));
        var slot = table.Register(103, texC);

        Assert.AreEqual(-1, slot);
        Assert.AreEqual(2, table.Textures.Count);
        Assert.AreEqual(-1, table.GetSlot(texC));
    }

    [Test]
    public void Register_OverCapacity_RetryAfterSlotReleased_Succeeds()
    {
        var table = new TextureSlotTable(2);
        var texA = CreateTexture("A");
        var texB = CreateTexture("B");
        var texC = CreateTexture("C");

        Assert.AreEqual(1, table.Register(101, texA));
        Assert.AreEqual(2, table.Register(102, texB));

        LogAssert.Expect(LogType.Warning, new Regex(@"\[TextureSlotTable\] over capacity"));
        Assert.AreEqual(-1, table.Register(103, texC));

        table.Unregister(102);

        Assert.AreEqual(2, table.Register(103, texC));
        Assert.AreEqual(1, table.GetSlot(texA));
        Assert.AreEqual(2, table.GetSlot(texC));
        Assert.AreEqual(-1, table.GetSlot(texB));
        Assert.AreEqual(2, table.Textures.Count);
    }

    private Texture2D CreateTexture(string name)
    {
        var texture = new Texture2D(2, 2) { name = name };
        createdTextures.Add(texture);
        return texture;
    }
}
