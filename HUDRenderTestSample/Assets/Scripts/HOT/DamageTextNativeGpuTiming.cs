using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Android Vulkan diagnostic. Explicit command tokens; no receive-frame offset or blocking readback.</summary>
public static class DamageTextNativeGpuTiming
{
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct Info
    {
        public double timestampPeriodNs;
        public uint timestampValidBits, queueFamily, vendorId, deviceId, apiVersion, driverVersion, capacity;
        public int status;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSample
    {
        public ulong beginTimestamp, endTimestamp, beginNativeFrame, endNativeFrame;
        public ulong readSafeFrame, readNativeFrame;
        public uint unityFrame, index, beginCalls, endCalls, available;
        public int status;
        public double milliseconds;
    }
    [Serializable] public sealed class Sample
    {
        public string beginTimestamp, endTimestamp, beginNativeFrame, endNativeFrame;
        public string readSafeFrame, readNativeFrame;
        public uint unityFrame, index, beginCalls, endCalls, available;
        public int status;
        public double milliseconds;
    }
    [DllImport("DamageTextGpuTiming")] private static extern int DamageGpuConfigure(uint capacity);
    [DllImport("DamageTextGpuTiming")] private static extern IntPtr DamageGpuGetEvent();
    [DllImport("DamageTextGpuTiming")] private static extern int DamageGpuGetEventBase();
    [DllImport("DamageTextGpuTiming")] private static extern void DamageGpuGetInfo(out Info output);
    [DllImport("DamageTextGpuTiming")] private static extern int DamageGpuGetSamples([Out] NativeSample[] output, uint capacity);

    public static void Configure(int capacity, out IntPtr callback, out int eventBase)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan || IntPtr.Size != 8) throw new NotSupportedException("Requires Android ARM64/Vulkan.");
        if (Marshal.SizeOf<Info>() != 40 || Marshal.SizeOf<NativeSample>() != 80) throw new InvalidOperationException("Native timing ABI mismatch.");
        int status = DamageGpuConfigure((uint)capacity);
        if (status != 1) throw new InvalidOperationException("Vulkan timestamp initialization failed: " + status);
        callback = DamageGpuGetEvent(); eventBase = DamageGpuGetEventBase();
        if (callback == IntPtr.Zero) throw new InvalidOperationException("Missing native callback.");
#else
        throw new NotSupportedException("Native GPU calibration runs only in the Android Player.");
#endif
    }
    public static void Record(CommandBuffer buffer, IntPtr callback, int eventId, int index, int unityFrame)
    {
        ulong token = ((ulong)(uint)unityFrame << 32) | (uint)(index + 1);
        buffer.IssuePluginEventAndData(callback, eventId, new IntPtr(unchecked((long)token)));
    }
    public static Sample[] Read(int capacity, out Info info)
    {
        DamageGpuGetInfo(out info);
        var values = new NativeSample[capacity];
        if (DamageGpuGetSamples(values, (uint)capacity) != capacity) throw new InvalidOperationException("Incomplete native sample storage.");
        var result = new Sample[capacity];
        for (int i = 0; i < capacity; i++)
        {
            var value = values[i];
            result[i] = new Sample { beginTimestamp = value.beginTimestamp.ToString("X16"), endTimestamp = value.endTimestamp.ToString("X16"),
                beginNativeFrame = value.beginNativeFrame.ToString(), endNativeFrame = value.endNativeFrame.ToString(),
                readSafeFrame = value.readSafeFrame.ToString(), readNativeFrame = value.readNativeFrame.ToString(),
                unityFrame = value.unityFrame, index = value.index, beginCalls = value.beginCalls, endCalls = value.endCalls,
                available = value.available, status = value.status, milliseconds = value.milliseconds };
        }
        return result;
    }
}
