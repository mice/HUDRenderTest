using NUnit.Framework;
using Unity.Collections;
using Stella3D;

/// <summary>
/// TC-SA-01: Writing through the managed view is immediately visible in the NativeArray view.
/// </summary>
public class TestSharedArray
{
    // TC-SA-01
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_013.md
    [Test]
    [Category("UT_UTIL_013")]
    public void ManagedWrite_NativeRead()
    {
        using var sa = new SharedArray<float>(4);

        float[] managed = sa;
        managed[0] = 1.5f;
        managed[1] = 2.5f;
        managed[2] = 3.5f;
        managed[3] = 4.5f;

        NativeArray<float> native = sa;
        Assert.AreEqual(1.5f, native[0], "native[0] must reflect managed write");
        Assert.AreEqual(2.5f, native[1], "native[1] must reflect managed write");
        Assert.AreEqual(3.5f, native[2], "native[2] must reflect managed write");
        Assert.AreEqual(4.5f, native[3], "native[3] must reflect managed write");
    }

    // Complement: writing through NativeArray is visible in managed view
    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_014.md
    [Test]
    [Category("UT_UTIL_014")]
    public void NativeWrite_ManagedRead()
    {
        using var sa = new SharedArray<int>(3);

        NativeArray<int> native = sa;
        native[0] = 10;
        native[1] = 20;
        native[2] = 30;

        int[] managed = sa;
        Assert.AreEqual(10, managed[0]);
        Assert.AreEqual(20, managed[1]);
        Assert.AreEqual(30, managed[2]);
    }
}
