using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class UnsafeFastCopy  {

    public static void Copy(NativeArray<Matrix4x4> nativeArray, Matrix4x4[] arrays, int srcIndex, int destIndex, int length)
    {
        unsafe
        {
            Matrix4x4* src = (Matrix4x4*)nativeArray.GetUnsafeReadOnlyPtr();
            src = src + srcIndex;
            fixed (Matrix4x4* dest = &arrays[destIndex])
            {
                UnsafeUtility.MemCpy(dest, src, sizeof(Matrix4x4) * length);
            }
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Copy(NativeArray<Vector4> nativeArray, Vector4[] arrays, int srcIndex, int destIndex, int length)
    {
        unsafe
        {
            Vector4* src = (Vector4*)nativeArray.GetUnsafeReadOnlyPtr();
            src = src + srcIndex;
            fixed (Vector4* dest = &arrays[destIndex])
            {
                UnsafeUtility.MemCpy(dest, src, sizeof(Vector4) * length);
            }
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void CopyVec4(Vector4[] srcList, Vector4* dest_ptr, int srcIndex, int destIndex, int length)
    {
        dest_ptr += destIndex;
        fixed (Vector4* src = &srcList[0])
        {
            UnsafeUtility.MemCpy(dest_ptr, src, sizeof(Vector4) * length);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    public static unsafe void CopyColor32(Color32[] srcList, Color32* dest_ptr, int srcIndex, int destIndex, int length)
    {
        dest_ptr += destIndex;
        fixed (Color32* src = &srcList[0])
        {
            UnsafeUtility.MemCpy(dest_ptr, src, sizeof(Color32) * length);
        }
    }
}
