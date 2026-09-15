using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// TC-FA-01..08: Unit tests for FreeListAllocator algorithms.
/// Verifies the alloc/release/merge contract in isolation from UIGeometry.
/// </summary>
public class TestFreeListAllocator
{
    // TC-FA-01
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_004.md
    [Test]
    [Category("UT_UTIL_004")]
    public void Ctor_SingleFullSlice()
    {
        const int cap = 100;
        var alloc = new FreeListAllocator(cap);

        Assert.AreEqual(1, alloc.FreeList.Count);
        var slice = alloc.FreeList.First.Value;
        Assert.AreEqual(0, slice.start);
        Assert.AreEqual(cap, slice.count);
    }

    // TC-FA-02
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_001.md
    [Test]
    [Category("UT_UTIL_001")]
    public void Alloc_ExactMatch_RemovesSlice()
    {
        var alloc = new FreeListAllocator(10);

        bool ok = alloc.Alloc(10, out int offset);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, offset);
        Assert.AreEqual(0, alloc.FreeList.Count, "exact-match alloc must remove the free node");
    }

    // TC-FA-03
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_003.md
    [Test]
    [Category("UT_UTIL_003")]
    public void Alloc_Split_LargerSlice()
    {
        var alloc = new FreeListAllocator(20);

        bool ok = alloc.Alloc(7, out int offset);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, offset);
        Assert.AreEqual(1, alloc.FreeList.Count);
        Assert.AreEqual(7, alloc.FreeList.First.Value.start);
        Assert.AreEqual(13, alloc.FreeList.First.Value.count);
    }

    // TC-FA-04: no GrowCallback in this implementation; Alloc returns false when exhausted
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_002.md
    [Test]
    [Category("UT_UTIL_002")]
    public void Alloc_NoFit_ReturnsFalse()
    {
        var alloc = new FreeListAllocator(4);
        alloc.Alloc(4, out _); // consume all

        bool ok = alloc.Alloc(1, out _);

        Assert.IsFalse(ok);
        Assert.AreEqual(0, alloc.FreeList.Count, "free list must be empty after consuming all memory");
    }

    // TC-FA-05: Release case1..5 – merging behavior
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_010.md
    [Test]
    [Category("UT_UTIL_010")]
    public void Release_LeftAdjacent_MergesForward()
    {
        // Free list: [(8,4)]. Release (4,4) → adjacent from left → merge to [(4,8)]
        var alloc = new FreeListAllocator(0);
        alloc.Release(8, 4);
        alloc.Release(4, 4);

        Assert.AreEqual(1, alloc.FreeList.Count);
        Assert.AreEqual(4, alloc.FreeList.First.Value.start);
        Assert.AreEqual(8, alloc.FreeList.First.Value.count);
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_012.md
    [Test]
    [Category("UT_UTIL_012")]
    public void Release_RightAdjacent_MergesBackward()
    {
        // Free list: [(0,4)]. Release (4,4) → adjacent from right → merge to [(0,8)]
        var alloc = new FreeListAllocator(0);
        alloc.Release(0, 4);
        alloc.Release(4, 4);

        Assert.AreEqual(1, alloc.FreeList.Count);
        Assert.AreEqual(0, alloc.FreeList.First.Value.start);
        Assert.AreEqual(8, alloc.FreeList.First.Value.count);
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_006.md
    [Test]
    [Category("UT_UTIL_006")]
    public void Release_BothAdjacent_MergesThreeWay()
    {
        // Free list: [(0,4),(8,4)]. Release (4,4) → bridges both → merge to [(0,12)]
        var alloc = new FreeListAllocator(0);
        alloc.Release(0, 4);
        alloc.Release(8, 4);
        alloc.Release(4, 4);

        Assert.AreEqual(1, alloc.FreeList.Count);
        Assert.AreEqual(0, alloc.FreeList.First.Value.start);
        Assert.AreEqual(12, alloc.FreeList.First.Value.count);
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_009.md
    [Test]
    [Category("UT_UTIL_009")]
    public void Release_InsertBeforeHead_NotAdjacent()
    {
        // Free list: [(8,4)]. Release (0,4) → before head, gap → [(0,4),(8,4)]
        var alloc = new FreeListAllocator(0);
        alloc.Release(8, 4);
        alloc.Release(0, 4);

        Assert.AreEqual(2, alloc.FreeList.Count);
        Assert.AreEqual(0, alloc.FreeList.First.Value.start);
        Assert.AreEqual(4, alloc.FreeList.First.Value.count);
        Assert.AreEqual(8, alloc.FreeList.Last.Value.start);
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_008.md
    [Test]
    [Category("UT_UTIL_008")]
    public void Release_InsertAfterTail_NotAdjacent()
    {
        // Free list: [(0,4)]. Release (8,4) → after tail, gap → [(0,4),(8,4)]
        var alloc = new FreeListAllocator(0);
        alloc.Release(0, 4);
        alloc.Release(8, 4);

        Assert.AreEqual(2, alloc.FreeList.Count);
        Assert.AreEqual(0, alloc.FreeList.First.Value.start);
        Assert.AreEqual(8, alloc.FreeList.Last.Value.start);
        Assert.AreEqual(4, alloc.FreeList.Last.Value.count);
    }

    // TC-FA-06
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_007.md
    [Test]
    [Category("UT_UTIL_007")]
    public void Release_Case0_Middle_Standalone()
    {
        // Free list: [(0,4),(8,4)]. Release (5,2) – middle, not adjacent to either.
        var alloc = new FreeListAllocator(0);
        alloc.Release(0, 4);
        alloc.Release(8, 4);
        alloc.Release(5, 2);

        Assert.AreEqual(3, alloc.FreeList.Count);
        var node = alloc.FreeList.First;
        Assert.AreEqual(0, node.Value.start); Assert.AreEqual(4, node.Value.count);
        node = node.Next;
        Assert.AreEqual(5, node.Value.start); Assert.AreEqual(2, node.Value.count);
        node = node.Next;
        Assert.AreEqual(8, node.Value.start); Assert.AreEqual(4, node.Value.count);
    }

    // TC-FA-07: Fuzz – free + allocated = capacity at every step
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_005.md
    [Test]
    [Category("UT_UTIL_005")]
    public void Fuzz_RandomAllocRelease()
    {
        const int cap = 100;
        const int iterations = 1000;
        var alloc = new FreeListAllocator(cap);
        var rng = new Random(42);
        var allocated = new List<(int start, int count)>();
        int totalAllocated = 0;

        for (int i = 0; i < iterations; i++)
        {
            bool doAlloc = allocated.Count == 0 || rng.Next(2) == 0;

            if (doAlloc)
            {
                int size = rng.Next(1, 10);
                if (alloc.Alloc(size, out int offset))
                {
                    allocated.Add((offset, size));
                    totalAllocated += size;
                }
            }
            else
            {
                int idx = rng.Next(allocated.Count);
                var slot = allocated[idx];
                alloc.Release(slot.start, slot.count);
                totalAllocated -= slot.count;
                allocated.RemoveAt(idx);
            }

            int freeSum = 0;
            foreach (var s in alloc.FreeList)
                freeSum += s.count;

            Assert.AreEqual(cap, freeSum + totalAllocated,
                $"iter {i}: free({freeSum}) + allocated({totalAllocated}) must equal cap({cap})");
        }
    }

    // TC-FA-08
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_011.md
    [Test]
    [Category("UT_UTIL_011")]
    public void Release_OrderIndependence()
    {
        // Releasing two non-adjacent slices in either order produces the same free list.
        var allocA = new FreeListAllocator(0);
        allocA.Release(0, 4);
        allocA.Release(8, 4);

        var allocB = new FreeListAllocator(0);
        allocB.Release(8, 4);
        allocB.Release(0, 4);

        Assert.AreEqual(allocA.FreeList.Count, allocB.FreeList.Count);
        var a = allocA.FreeList.First;
        var b = allocB.FreeList.First;
        while (a != null)
        {
            Assert.AreEqual(a.Value.start, b.Value.start);
            Assert.AreEqual(a.Value.count, b.Value.count);
            a = a.Next;
            b = b.Next;
        }
    }
}
