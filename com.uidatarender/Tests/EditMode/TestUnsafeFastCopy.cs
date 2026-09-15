using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// TC-UFC-01: UnsafeFastCopy.CopyVec4 output matches Array.Copy.
/// TC-UFC-02: UnsafeFastCopy.CopyColor32 output matches Array.Copy.
/// </summary>
public class TestUnsafeFastCopy
{
    // TC-UFC-01
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_017.md
    [Test]
    [Category("UT_UTIL_017")]
    public unsafe void CopyVec4_EquivalentToArrayCopy()
    {
        var src = new Vector4[]
        {
            new Vector4(1, 2, 3, 4),
            new Vector4(5, 6, 7, 8),
            new Vector4(9, 10, 11, 12),
        };
        var expected = new Vector4[src.Length];
        Array.Copy(src, 0, expected, 0, src.Length);

        var dest = new Vector4[src.Length];
        var handle = GCHandle.Alloc(dest, GCHandleType.Pinned);
        try
        {
            Vector4* ptr = (Vector4*)handle.AddrOfPinnedObject().ToPointer();
            UnsafeFastCopy.CopyVec4(src, ptr, srcIndex: 0, destIndex: 0, length: src.Length);
        }
        finally
        {
            handle.Free();
        }

        for (int i = 0; i < src.Length; i++)
            Assert.AreEqual(expected[i], dest[i], $"Element {i} mismatch");
    }

    // TC-UFC-01 extended: destIndex offset shifts write position
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_016.md
    [Test]
    [Category("UT_UTIL_016")]
    public unsafe void CopyVec4_DestIndex_ShiftsWritePosition()
    {
        const int destOffset = 2;
        var src = new Vector4[] { new Vector4(7, 8, 9, 10), new Vector4(11, 12, 13, 14) };
        var dest = new Vector4[src.Length + destOffset];

        var handle = GCHandle.Alloc(dest, GCHandleType.Pinned);
        try
        {
            Vector4* ptr = (Vector4*)handle.AddrOfPinnedObject().ToPointer();
            UnsafeFastCopy.CopyVec4(src, ptr, srcIndex: 0, destIndex: destOffset, length: src.Length);
        }
        finally
        {
            handle.Free();
        }

        Assert.AreEqual(src[0], dest[destOffset],     "dest[2] should equal src[0]");
        Assert.AreEqual(src[1], dest[destOffset + 1], "dest[3] should equal src[1]");
        Assert.AreEqual(default(Vector4), dest[0],    "dest[0] should be untouched");
    }

    // TC-UFC-02
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_015.md
    [Test]
    [Category("UT_UTIL_015")]
    public unsafe void CopyColor32_EquivalentToArrayCopy()
    {
        var src = new Color32[]
        {
            new Color32(1,  2,  3,  255),
            new Color32(5,  6,  7,  255),
            new Color32(9, 10, 11, 255),
        };
        var expected = new Color32[src.Length];
        Array.Copy(src, 0, expected, 0, src.Length);

        var dest = new Color32[src.Length];
        var handle = GCHandle.Alloc(dest, GCHandleType.Pinned);
        try
        {
            Color32* ptr = (Color32*)handle.AddrOfPinnedObject().ToPointer();
            UnsafeFastCopy.CopyColor32(src, ptr, srcIndex: 0, destIndex: 0, length: src.Length);
        }
        finally
        {
            handle.Free();
        }

        for (int i = 0; i < src.Length; i++)
        {
            Assert.AreEqual(expected[i].r, dest[i].r, $"R mismatch at {i}");
            Assert.AreEqual(expected[i].g, dest[i].g, $"G mismatch at {i}");
            Assert.AreEqual(expected[i].b, dest[i].b, $"B mismatch at {i}");
            Assert.AreEqual(expected[i].a, dest[i].a, $"A mismatch at {i}");
        }
    }
}
