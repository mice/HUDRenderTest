using System;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

public sealed partial class HudRcPerformanceRunner
{
    [Serializable]
    public struct AllocationReading
    {
        public bool available, saturated, bytesAvailable;
        public long bytes, events;
        public long rawValue;
        public string unit;
        public static AllocationReading Unavailable => new AllocationReading { bytes = -1, events = -1 };
    }

    /// <summary>Bounded diagnostic scope, never uses the unsafe Android IL2CPP thread-byte API.</summary>
    public sealed class AllocationEventCounter : IDisposable
    {
        private ProfilerRecorder recorder;
        private readonly int capacity;
        private readonly bool byteUnit;
        private readonly string unit;
        public AllocationReading Calibration { get; }
        public AllocationEventCounter(int capacity)
        {
            if (capacity < 1 || capacity > 65536) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", capacity,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            unit = "unavailable";
            if (!recorder.Valid) { Calibration = AllocationReading.Unavailable; return; }
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var handle in handles)
            {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                if (description.Name != "GC.Alloc") continue;
                byteUnit = description.UnitType == ProfilerMarkerDataUnit.Bytes;
                unit = description.UnitType.ToString(); break;
            }
            recorder.Stop(); recorder.Reset(); recorder.Start();
            GC.KeepAlive(new byte[4096]);
            recorder.Stop();
            var control = Read();
            control.available &= control.events > 0;
            control.bytesAvailable = control.available && byteUnit && control.rawValue >= 4096;
            control.bytes = control.bytesAvailable ? control.rawValue : -1;
            if (!control.available) { control.bytes = -1; control.events = -1; }
            Calibration = control;
        }
        public void Begin()
        {
            if (!Calibration.available) return;
            recorder.Reset(); recorder.Start();
        }
        public AllocationReading End()
        {
            if (!Calibration.available) return AllocationReading.Unavailable;
            recorder.Stop();
            return Read();
        }
        private AllocationReading Read()
        {
            var value = AllocationReading.Unavailable;
            if (!recorder.Valid) return value;
            value.saturated = recorder.Count >= capacity;
            value.unit = unit;
            if (value.saturated) return value;
            value.available = true; value.rawValue = value.events = 0;
            for (int i = 0; i < recorder.Count; i++)
            {
                var sample = recorder.GetSample(i);
                value.rawValue += sample.Value; value.events += sample.Count;
            }
            value.bytesAvailable = byteUnit && (Calibration.available ? Calibration.bytesAvailable : true);
            value.bytes = value.bytesAvailable ? value.rawValue : -1;
            return value;
        }
        public void Dispose() { recorder.Dispose(); }
    }

    private AllocationEventCounter allocationCounter;
    private AllocationReading allocationCalibration;

    private void FinishAllocationScope(ref FrameSample sample, AllocationReading value)
    {
        sample.allocationEventsAvailable = value.available;
        sample.allocationEventsSaturated = value.saturated;
        sample.allocationEventBytes = value.bytes;
        sample.allocationEventBytesAvailable = value.bytesAvailable;
        sample.allocationEventCount = value.events;
    }
}
