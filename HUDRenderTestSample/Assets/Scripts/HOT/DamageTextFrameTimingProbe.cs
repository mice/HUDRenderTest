using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Hardware calibration only. Retains late observations; never supplies formal benchmark samples.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class DamageTextFrameTimingProbe : MonoBehaviour
{
    private const int FrameCount = 640, WarmupFrames = 64, DrainStart = 624;
    public DamageTextBenchmarkRunner runner;
    public bool nativeVulkanTimestamps;
    public string nativePluginSHA256;

    [Serializable] private sealed class Frame
    {
        public int unityFrame, relativeFrame, cpuSleepMs, requestedDrawRepeats, renderedDrawRepeats;
        public int preCullUnityFrame, postRenderUnityFrame, cameraPasses, updateResultCount, endResultCount;
        public string stopwatchTicks, preCullTicks, postRenderTicks;
        public bool warmup, drain;
        [NonSerialized] public long updateClock, preCullClock, postRenderClock;
    }
    private struct Observation
    {
        public int unityFrame, phase;
        public long clock;
        public FrameTiming value;
    }
    [Serializable] private sealed class Timing
    {
        public int observedAtUnityFrame, observationPhase;
        public string observedAtTicks, frameStartTimestamp, firstSubmitTimestamp, presentTimestamp, completeTimestamp;
        public double cpuFrameMs, cpuMainThreadMs, cpuRenderThreadMs, cpuPresentWaitMs, gpuFrameMsRaw;
        public float widthScale, heightScale;
        public uint syncInterval;
        public bool gpuTimeValid;
    }
    [Serializable] private sealed class Report
    {
        public string schema = "damage-frame-timing-calibration-v2";
        public string sourceFingerprint, commit, buildGuid, api, device, model, applicationIdentifier, backend;
        public string cpuTimerFrequency, gpuTimerFrequency, stopwatchFrequency;
        public string clipHash, atlasHash, styleHash, cameraHash, configurationHash, captureRequestSHA256, scheme, vertexLayout;
        public string scope = "Diagnostic Unity whole-frame timing, not HUD-only. Immutable extra-draw buffers; camera callbacks and all repeated timestamp observations retained. No formal ranking.";
        public string observationPhases = "0=Update after CaptureFrameTimings/CPU perturbation, 1=WaitForEndOfFrame";
        public string schedule = "CPU sleeps at relative 80/168/328/520; GPU single pulses at 96/192/288/384/480/576 (16/32/64 repeated); GPU 64-repeat plateaus at 120..122/240..242/360..362";
        public bool featureEnabled, formal;
        public int documentedDelayFrames = 4, frameCount = FrameCount, warmupFrames = WarmupFrames, drainStart = DrainStart;
        public int width, height;
        public Frame[] frames;
        public Timing[] timings;
        public string nativePluginSHA256;
        public string nativeScope;
        public DamageTextNativeGpuTiming.Info nativeInfo;
        public DamageTextNativeGpuTiming.Sample[] nativeTimings;
    }

    private readonly CommandBuffer[] _loads = new CommandBuffer[3];
    private readonly FrameTiming[] _latest = new FrameTiming[16];
    private readonly List<Frame> _frames = new List<Frame>(FrameCount);
    private readonly List<Observation> _observations = new List<Observation>(FrameCount * 2 * 16);
    private CommandBuffer _attached;
    private Camera _camera;
    private Frame _current;
    private readonly List<CommandBuffer> _timestampBuffers = new List<CommandBuffer>(FrameCount * 2);
    private CommandBuffer _beginTimestamp, _endTimestamp, _attachedBegin, _attachedEnd;
    private IntPtr _nativeCallback;
    private int _nativeEventBase;

    private void Awake() { if (runner != null) runner.enabled = false; }
    private IEnumerator Start()
    {
        runner.ApplyCaptureRequest(File.ReadAllText(DamageTextBenchmarkRunner.CaptureRequestPath));
        if (runner.plan.Phase != DamageTextBenchmarkPhase.DrawOnly) throw new InvalidOperationException("Calibration requires a frozen draw-only request.");
        runner.autoExport = false; runner.Begin();
        if (runner.Session == null) throw new InvalidOperationException("Calibration prepare failed: " + runner.Failure);
        var session = runner.Session.Report;
        session.formal = false;
        _camera = runner.outputCamera;
        if (nativeVulkanTimestamps) DamageTextNativeGpuTiming.Configure(FrameCount, out _nativeCallback, out _nativeEventBase);
        // Record once. A preceding render frame may still reference the command buffer.
        for (int index = 0; index < _loads.Length; index++)
        {
            int repeats = 16 << index;
            var buffer = _loads[index] = new CommandBuffer { name = "Damage GPU calibration immutable x" + repeats };
            for (int draw = 0; draw < repeats; draw++) runner.Session.Renderer.RecordDraws(buffer, Matrix4x4.identity);
        }
        Camera.onPreCull += BeforeCamera;
        Camera.onPostRender += AfterCamera;
        var report = new Report
        {
            sourceFingerprint = runner.sourceFingerprint, commit = runner.sourceCommit, buildGuid = Application.buildGUID,
            api = SystemInfo.graphicsDeviceType.ToString(), device = SystemInfo.graphicsDeviceName,
            model = SystemInfo.deviceModel, applicationIdentifier = Application.identifier, width = Screen.width, height = Screen.height,
#if ENABLE_IL2CPP
            backend = "IL2CPP",
#else
            backend = "Mono",
#endif
            featureEnabled = FrameTimingManager.IsFeatureEnabled(),
            cpuTimerFrequency = FrameTimingManager.GetCpuTimerFrequency().ToString(),
            gpuTimerFrequency = FrameTimingManager.GetGpuTimerFrequency().ToString(), stopwatchFrequency = Stopwatch.Frequency.ToString(),
            clipHash = session.clipHash, atlasHash = session.atlasHash, styleHash = session.styleHash,
            cameraHash = session.cameraHash, configurationHash = session.configurationHash,
            captureRequestSHA256 = session.captureRequestSHA256, scheme = session.scheme, vertexLayout = session.vertexLayout
        };
        if (nativeVulkanTimestamps)
        {
            report.schema = "damage-frame-timing-calibration-v3";
            report.nativePluginSHA256 = nativePluginSHA256;
            report.nativeScope = "Instrumented camera render region: BeforeForwardOpaque TOP_OF_PIPE through AfterEverything BOTTOM_OF_PIPE. The calibration scene contains only HUD/extra draws; render-pass store/resume overhead is included, initial clears may precede the region. Excludes presentation and other cameras; not an uninstrumented whole-frame metric. Both events ensure outside render pass; no explicit queue flush or wait during sampling. Diagnostic only.";
        }
        var endOfFrame = new WaitForEndOfFrame();
        try
        {
            for (int index = 0; index < FrameCount; index++)
            {
                _current = new Frame
                {
                    unityFrame = Time.frameCount, relativeFrame = index, updateClock = Stopwatch.GetTimestamp(),
                    cpuSleepMs = SleepMilliseconds(index), requestedDrawRepeats = DrawRepeats(index),
                    warmup = index < WarmupFrames, drain = index >= DrainStart
                };
                _frames.Add(_current);
                if (nativeVulkanTimestamps)
                {
                    _beginTimestamp = new CommandBuffer { name = "Damage native GPU begin " + index };
                    _endTimestamp = new CommandBuffer { name = "Damage native GPU end " + index };
                    DamageTextNativeGpuTiming.Record(_beginTimestamp, _nativeCallback, _nativeEventBase, index, Time.frameCount);
                    DamageTextNativeGpuTiming.Record(_endTimestamp, _nativeCallback, _nativeEventBase + 1, index, Time.frameCount);
                    // Keep immutable buffers alive until all diagnostic frames have drained.
                    _timestampBuffers.Add(_beginTimestamp); _timestampBuffers.Add(_endTimestamp);
                }
                FrameTimingManager.CaptureFrameTimings();
                if (_current.cpuSleepMs != 0) System.Threading.Thread.Sleep(_current.cpuSleepMs);
                _current.updateResultCount = Observe(0);
                yield return endOfFrame;
                _current.endResultCount = Observe(1);
                yield return null;
            }
            _current = null;
            if (nativeVulkanTimestamps) report.nativeTimings = DamageTextNativeGpuTiming.Read(FrameCount, out report.nativeInfo);
            report.frames = _frames.ToArray();
            foreach (var frame in report.frames)
            {
                frame.stopwatchTicks = frame.updateClock.ToString();
                frame.preCullTicks = frame.preCullClock.ToString();
                frame.postRenderTicks = frame.postRenderClock.ToString();
            }
            report.timings = new Timing[_observations.Count];
            for (int index = 0; index < _observations.Count; index++)
            {
                var observation = _observations[index]; var value = observation.value;
                report.timings[index] = new Timing
                {
                    observedAtUnityFrame = observation.unityFrame, observationPhase = observation.phase,
                    observedAtTicks = observation.clock.ToString(), frameStartTimestamp = value.frameStartTimestamp.ToString("X16"),
                    firstSubmitTimestamp = value.firstSubmitTimestamp.ToString("X16"), presentTimestamp = value.cpuTimePresentCalled.ToString("X16"),
                    completeTimestamp = value.cpuTimeFrameComplete.ToString("X16"), cpuFrameMs = value.cpuFrameTime,
                    cpuMainThreadMs = value.cpuMainThreadFrameTime, cpuRenderThreadMs = value.cpuRenderThreadFrameTime,
                    cpuPresentWaitMs = value.cpuMainThreadPresentWaitTime, gpuFrameMsRaw = value.gpuFrameTime,
                    widthScale = value.widthScale, heightScale = value.heightScale, syncInterval = value.syncInterval,
                    gpuTimeValid = value.gpuFrameTime > 0 && !double.IsNaN(value.gpuFrameTime) && !double.IsInfinity(value.gpuFrameTime)
                };
            }
            string directory = Path.Combine(Application.persistentDataPath, "DamageTextBenchmark", "Diagnostics");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "timing-probe-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            UnityEngine.Debug.Log("DAMAGE_TIMING_PROBE_COMPLETE=" + path);
        }
        finally { Cleanup(); }
    }
    private int Observe(int phase)
    {
        uint count = FrameTimingManager.GetLatestTimings((uint)_latest.Length, _latest);
        long clock = Stopwatch.GetTimestamp();
        for (int index = 0; index < count; index++)
            _observations.Add(new Observation { unityFrame = Time.frameCount, phase = phase, clock = clock, value = _latest[index] });
        return (int)count;
    }
    private void BeforeCamera(Camera camera)
    {
        if (camera != _camera || _current == null) return;
        if (_attachedBegin != null) _camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, _attachedBegin);
        if (_attachedEnd != null) _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _attachedEnd);
        int repeats = _current.requestedDrawRepeats;
        CommandBuffer desired = repeats == 16 ? _loads[0] : repeats == 32 ? _loads[1] : repeats == 64 ? _loads[2] : null;
        if (_attached != desired)
        {
            if (_attached != null) _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _attached);
            _attached = desired;
            if (_attached != null) _camera.AddCommandBuffer(CameraEvent.AfterEverything, _attached);
        }
        if (nativeVulkanTimestamps)
        {
            _attachedBegin = _beginTimestamp; _attachedEnd = _endTimestamp;
            _camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, _attachedBegin);
            // Appended after the current extra-draw buffer, so the end includes every scheduled draw.
            _camera.AddCommandBuffer(CameraEvent.AfterEverything, _attachedEnd);
        }
        _current.cameraPasses++;
        _current.preCullUnityFrame = Time.frameCount; _current.preCullClock = Stopwatch.GetTimestamp();
        _current.renderedDrawRepeats = repeats;
    }
    private void AfterCamera(Camera camera)
    {
        if (camera != _camera || _current == null) return;
        _current.postRenderUnityFrame = Time.frameCount; _current.postRenderClock = Stopwatch.GetTimestamp();
    }
    private static int SleepMilliseconds(int frame) => frame == 80 ? 80 : frame == 168 ? 130 : frame == 328 ? 100 : frame == 520 ? 90 : 0;
    private static int DrawRepeats(int frame)
    {
        if ((frame >= 120 && frame <= 122) || (frame >= 240 && frame <= 242) || (frame >= 360 && frame <= 362)) return 64;
        if (frame == 96 || frame == 384) return 16;
        if (frame == 192 || frame == 480) return 32;
        return frame == 288 || frame == 576 ? 64 : 0;
    }
    private void OnDestroy() { Cleanup(); }
    private void Cleanup()
    {
        Camera.onPreCull -= BeforeCamera; Camera.onPostRender -= AfterCamera;
        if (_attached != null && _camera != null) _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _attached);
        _attached = null;
        if (_camera != null && _attachedBegin != null) _camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, _attachedBegin);
        if (_camera != null && _attachedEnd != null) _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _attachedEnd);
        _attachedBegin = _attachedEnd = _beginTimestamp = _endTimestamp = null;
        foreach (var buffer in _timestampBuffers) buffer.Dispose();
        _timestampBuffers.Clear();
        for (int index = 0; index < _loads.Length; index++) { _loads[index]?.Dispose(); _loads[index] = null; }
        if (runner != null) runner.Stop();
    }
}
