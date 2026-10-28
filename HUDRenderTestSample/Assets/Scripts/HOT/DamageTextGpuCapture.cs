using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One bounded Player segment. All command buffers are immutable and allocated before Warming.</summary>
public sealed class DamageTextGpuCapture : IDisposable
{
    public const int DrainFrames = 16;
    public const string Scope = "HUD-camera-instrumented";
    public const string ScopeDescription = "BeforeForwardOpaque TOP_OF_PIPE through AfterEverything BOTTOM_OF_PIPE; this scene contains only HUD geometry. Includes forced render-pass store/resume and query overhead, may exclude initial clears, excludes presentation/other cameras. Not an uninstrumented whole-frame measurement.";
    [Serializable] public struct CameraFrame
    {
        public int unityFrame, preCullFrame, postRenderFrame, cameraPasses;
        public double bindingCpuMs;
    }
    [Serializable] public sealed class Trace
    {
        public string schema = "damage-native-gpu-capture-v1";
        public string pluginSHA256, calibrationSHA256, scope = ScopeDescription, failure;
        public int firstUnityFrame, capacity, capturedFrames, drainFrames, matchedSamples, commandBuffers;
        public double setupCpuMs;
        public bool complete;
        public DamageTextNativeGpuTiming.Info info;
        public CameraFrame[] cameraFrames;
        public DamageTextNativeGpuTiming.Sample[] timings;
    }
    private readonly Camera _camera;
    private readonly CommandBuffer[] _begins, _ends;
    private readonly CameraFrame[] _frames;
    private readonly string _pluginHash, _calibrationHash;
    private readonly double _setupMs;
    private int _used, _current = -1, _attached = -1;
    private bool _disposed;
    public int FirstUnityFrame { get; }

    public DamageTextGpuCapture(Camera camera, DamageTextBenchmarkPlan plan, string pluginHash, string calibrationHash)
    {
        if (camera == null) throw new ArgumentNullException(nameof(camera));
        if (plan.WarmupFrames < 60) throw new ArgumentException("Native capture requires at least 60 warmup frames.");
        int samples = plan.Mode == DamageTextBenchmarkMode.Realtime ? plan.MaximumRealtimeFrames : plan.SampleFrames;
        int capacity = checked(samples + plan.WarmupFrames + 2 + DrainFrames);
        if (capacity > 8192) throw new ArgumentException("Register a bounded native capture of at most 8,192 actual frames.");
        _camera = camera; _pluginHash = pluginHash; _calibrationHash = calibrationHash;
        FirstUnityFrame = Time.frameCount + 1;
        _frames = new CameraFrame[capacity]; _begins = new CommandBuffer[capacity]; _ends = new CommandBuffer[capacity];
        long start = Stopwatch.GetTimestamp();
        try
        {
            DamageTextNativeGpuTiming.Configure(capacity, out var callback, out int eventBase);
            for (int i = 0; i < capacity; i++)
            {
                _begins[i] = new CommandBuffer { name = "Damage native GPU begin" };
                _ends[i] = new CommandBuffer { name = "Damage native GPU end" };
                DamageTextNativeGpuTiming.Record(_begins[i], callback, eventBase, i, FirstUnityFrame + i);
                DamageTextNativeGpuTiming.Record(_ends[i], callback, eventBase + 1, i, FirstUnityFrame + i);
            }
            Camera.onPreCull += BeforeCamera; Camera.onPostRender += AfterCamera;
        }
        catch { Dispose(); throw; }
        _setupMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    }
    public void QueueFrame(int unityFrame)
    {
        if (_disposed || _used >= _frames.Length || unityFrame != FirstUnityFrame + _used)
            throw new InvalidOperationException("Native capture frame sequence/capacity changed.");
        _current = _used++; _frames[_current].unityFrame = unityFrame;
    }
    private void BeforeCamera(Camera camera)
    {
        if (camera != _camera || _current < 0 || _frames[_current].unityFrame != Time.frameCount) return;
        long start = Stopwatch.GetTimestamp();
        if (_attached >= 0)
        {
            _camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, _begins[_attached]);
            _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _ends[_attached]);
        }
        _camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, _begins[_current]);
        _camera.AddCommandBuffer(CameraEvent.AfterEverything, _ends[_current]); _attached = _current;
        ref var frame = ref _frames[_current];
        frame.cameraPasses++; frame.preCullFrame = Time.frameCount;
        frame.bindingCpuMs = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    }
    private void AfterCamera(Camera camera)
    {
        if (camera == _camera && _current >= 0) _frames[_current].postRenderFrame = Time.frameCount;
    }
    public void Complete(DamageTextBenchmarkReport report, int drainedFrames)
    {
        // Allocation/formatting and managed readback occur after Sampling and the drain.
        var values = DamageTextNativeGpuTiming.Read(_frames.Length, out var info);
        var trace = new Trace { pluginSHA256 = _pluginHash, calibrationSHA256 = _calibrationHash,
            firstUnityFrame = FirstUnityFrame, capacity = _frames.Length, capturedFrames = _used,
            drainFrames = drainedFrames, commandBuffers = _frames.Length * 2, setupCpuMs = _setupMs, info = info,
            cameraFrames = new CameraFrame[_used], timings = new DamageTextNativeGpuTiming.Sample[_used] };
        Array.Copy(_frames, trace.cameraFrames, _used); Array.Copy(values, trace.timings, _used);
        bool complete = info.status == 1 && drainedFrames == DrainFrames && !report.aborted;
        for (int index = 0; index < report.frames.Length; index++)
        {
            ref var frame = ref report.frames[index];
            int slot = frame.unityFrame - FirstUnityFrame;
            bool valid = slot >= 0 && slot < _used;
            if (valid)
            {
                var value = values[slot]; var camera = _frames[slot];
                valid = value.index == slot && value.unityFrame == frame.unityFrame && value.beginCalls == 1 && value.endCalls == 1 &&
                    value.available == 1 && value.status == 2 && value.milliseconds > 0 && !double.IsNaN(value.milliseconds) &&
                    !double.IsInfinity(value.milliseconds) && value.beginNativeFrame == value.endNativeFrame &&
                    camera.cameraPasses == 1 && camera.preCullFrame == frame.unityFrame && camera.postRenderFrame == frame.unityFrame;
            }
            frame.gpuAvailable = valid; frame.gpuMs = valid ? values[slot].milliseconds : -1;
            if (frame.stage == DamageTextBenchmarkStage.Sampling)
            {
                if (valid) trace.matchedSamples++; else complete = false;
            }
        }
        trace.complete = complete && trace.matchedSamples == report.samples;
        trace.failure = trace.complete ? "" : "Incomplete native query/camera/token/drain sequence; no GPU ranking.";
        report.nativeGpu = trace; report.gpuScope = Scope;
        report.gpuTool = "Vulkan timestamp queries through IUnityGraphicsVulkan";
        report.gpuStatus = trace.complete ? "validated-native-timestamps" : "incomplete-native-timestamps";
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        Camera.onPreCull -= BeforeCamera; Camera.onPostRender -= AfterCamera;
        if (_camera != null && _attached >= 0)
        {
            _camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, _begins[_attached]);
            _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _ends[_attached]);
        }
        foreach (var buffer in _begins) buffer?.Dispose();
        foreach (var buffer in _ends) buffer?.Dispose();
        _attached = _current = -1;
    }
}
