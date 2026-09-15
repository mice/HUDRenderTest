using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Stella3D;
using Stella3d.ExternJobGenerator;
using Unity.Collections;
using UnityEngine;

public class TestManagedTaskCoverage
{
    private sealed class CountTask : IJobTask
    {
        public int Count;
        public void Execute() => Count++;
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_024.md
    [Test]
    [Category("UT_UTIL_024")]
    public void ManagedCodeInJob_ScheduleAndLateUpdate_ExecutesAndCleansUp()
    {
        var go = new GameObject("managed-job", typeof(ManagedCodeInJob));
        try
        {
            var runner = go.GetComponent<ManagedCodeInJob>();
            var task = new CountTask();

            var handle = runner.ScheduleTask(task);
            handle.Complete();
            var lateUpdate = typeof(ManagedCodeInJob).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(lateUpdate);
            lateUpdate.Invoke(runner, null);

            Assert.AreEqual(1, task.Count);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_025.md
    [Test]
    [Category("UT_UTIL_025")]
    public unsafe void NativeArrayExtensions_ReturnWritableAndReadonlyPointers()
    {
        var array = new NativeArray<int>(2, Allocator.Temp);
        try
        {
            int* writable = array.Ptr();
            writable[0] = 7;
            int* readable = array.ReadPtr();

            Assert.AreEqual(7, readable[0]);
        }
        finally
        {
            array.Dispose();
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_026.md
    [Test]
    [Category("UT_UTIL_026")]
    public void SharedArray_ResizeClearEnumerateAndDispose()
    {
        var shared = new SharedArray<int>(new[] { 1, 2, 3 });
        try
        {
            Assert.AreEqual(3, shared.Length);
            shared.Resize(3);
            shared.Resize(5);
            Assert.AreEqual(5, shared.Length);

            int[] managed = shared;
            managed[3] = 9;
            NativeArray<int> native = shared;
            Assert.AreEqual(9, native[3]);

            var values = new List<int>();
            foreach (var item in shared)
            {
                values.Add(item);
            }
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 9, 0 }, values);

            shared.Clear();
            Assert.AreEqual(0, managed[3]);
        }
        finally
        {
            shared.Dispose();
            Assert.DoesNotThrow(() => shared.Dispose());
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_027.md
    [Test]
    [Category("UT_UTIL_027")]
    public void SharedArray_ThrowsWhenNativeAliasSizeDiffers()
    {
        Assert.Throws<System.InvalidOperationException>(() => new SharedArray<int, long>(1));
    }

    // TestRecord: Documentation~/Testing/Unit/Utility/UT_UTIL_028.md
    [Test]
    [Category("UT_UTIL_028")]
    public void UnsafeFastCopy_CopyMatrixAndVectorNativeArrays()
    {
        var matrices = new NativeArray<Matrix4x4>(2, Allocator.Temp);
        var vectors = new NativeArray<Vector4>(2, Allocator.Temp);
        try
        {
            matrices[0] = Matrix4x4.identity;
            matrices[1] = Matrix4x4.Translate(new Vector3(1, 2, 3));
            vectors[0] = new Vector4(1, 2, 3, 4);
            vectors[1] = new Vector4(5, 6, 7, 8);

            var matrixDest = new Matrix4x4[3];
            var vectorDest = new Vector4[3];
            UnsafeFastCopy.Copy(matrices, matrixDest, 1, 2, 1);
            UnsafeFastCopy.Copy(vectors, vectorDest, 0, 1, 2);

            Assert.AreEqual(matrices[1], matrixDest[2]);
            Assert.AreEqual(vectors[0], vectorDest[1]);
            Assert.AreEqual(vectors[1], vectorDest[2]);
        }
        finally
        {
            matrices.Dispose();
            vectors.Dispose();
        }
    }
}
