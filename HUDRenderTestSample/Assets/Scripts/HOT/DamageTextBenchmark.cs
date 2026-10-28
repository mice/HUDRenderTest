using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum DamageTextBenchmarkMode { Visual, FixedSequence, Realtime }
public enum DamageTextBenchmarkPhase { DrawOnly, FullLifecycle }
public enum DamageTextBenchmarkStage { Preparing, Warming, Barrier, Sampling, Completed, Aborted }

[Serializable]
public sealed class DamageTextBenchmarkPlan
{
    public DamageTextBenchmarkMode Mode = DamageTextBenchmarkMode.FixedSequence;
    public DamageTextBenchmarkPhase Phase = DamageTextBenchmarkPhase.FullLifecycle;
    public DamageTextGeometry Geometry = DamageTextGeometry.PersistentMesh;
    public DamageTextPositionFormat PositionFormat = DamageTextPositionFormat.Float32;
    public int WarmupFrames = 60, SampleFrames = 3600, MaximumRealtimeFrames = 36000;
    public float Checkpoint = .12f, PreparationStep = 1f / 60f;
    public bool BurstWorkload;
    public DamageTextBenchmarkPlan Copy() => (DamageTextBenchmarkPlan)MemberwiseClone();
    public void Validate(float duration)
    {
        if (!Enum.IsDefined(typeof(DamageTextBenchmarkMode), Mode) || !Enum.IsDefined(typeof(DamageTextBenchmarkPhase), Phase) ||
            !Enum.IsDefined(typeof(DamageTextGeometry), Geometry) || !Enum.IsDefined(typeof(DamageTextPositionFormat), PositionFormat) ||
            (Geometry == DamageTextGeometry.InstancedQuad && PositionFormat != DamageTextPositionFormat.Float32) ||
            WarmupFrames < 0 || WarmupFrames > 10000 || SampleFrames < 2 || SampleFrames > 100000 ||
            MaximumRealtimeFrames < SampleFrames || MaximumRealtimeFrames > 100000 ||
            !DamageTextRequest.Finite(Checkpoint) || Checkpoint < 0 || Checkpoint > duration ||
            !DamageTextRequest.Finite(PreparationStep) || PreparationStep <= 0 || PreparationStep > 1)
            throw new ArgumentException("Invalid bounded benchmark plan.");
    }
}

[Serializable]
public struct DamageTextBenchmarkFrame
{
    public int unityFrame, index;
    public DamageTextBenchmarkStage stage;
    public float time;
    public double outerUpdateMs, telemetryMs, frameIntervalMs;
    public double mainThreadMs, renderThreadMs, gpuMs;
    public bool mainThreadAvailable, renderThreadAvailable, gcAvailable, gpuAvailable;
    public long gcBytes;
    public int consumed, expectedLiveEvents, expectedLiveCharacters, completedWindows;
    public long expectedLiveChecksum, actualLiveChecksum;
    public bool stateMatches, refreshFrame;
    public DamageTextStatistics statistics;
}

[Serializable]
public struct DamageTextBenchmarkEvent
{
    public DamageTextRequest request;
    public DamageTextSpawnResult result;
    public float consumedAt, firstPublishedAt, endTime, observedEndedAt;
    public bool missed;
}

[Serializable]
public sealed class DamageTextBenchmarkReport
{
    public string schema = "damage-text-benchmark-v1";
    public string utc, commit = "unknown", sourceStatus = "unknown", sourceFingerprint = "unknown", buildGuid;
    public string packageVersion = "unknown", unityVersion, device, operatingSystem, graphicsApi, graphicsDevice, backend, executionMode;
    public string sourceFingerprintSchema = "damage-comparison-source-v1", colorSpace, processorType, buildType;
    public string shaderName;
    public string vertexLayout;
    public int shaderPassCount;
    public int targetFrameRate, vSyncCount, qualityLevel, processorCount, systemMemoryMB;
    public string clipHash, atlasHash, styleHash, configurationHash, cameraHash, planHash, workloadHash, timesHash, expectedRequestHash, consumedRequestHash;
    public string scheme, requestedFormat, effectiveFormat, formatReason;
    public string visualStatus = "unverified", visualEvidenceSHA256 = "unknown";
    public string captureRunId, captureRequestSHA256;
    public string gpuStatus = "unavailable", gpuScope = "unavailable", gpuTool = "unavailable";
    public string gcStatus = "unavailable", stageCpuStatus = "unavailable: detailed diagnostics disabled";
    public string uploadScope = "API input estimates; not driver traffic", gpuMemoryStatus = "unavailable";
    public string managedStateCacheStatus = "unavailable: event/queue/HashSet object bytes not measured; capacities in rendererConfiguration";
    public string preparationGcStatus = "unavailable: Preparing precedes frame recorders";
    public string cleanupGcStatus = "unavailable: final export/disposal follow Sampling";
    public string reason;
    public string clockPolicy;
    public bool aborted, formal, completeEventInterval, controlsVisible;
    public float intervalStart, intervalEnd;
    public int width, height, expectedEvents, consumedEvents, samples, completedWindows;
    public double preparationMs, resetBarrierMs;
    public long theoreticalVertexCapacityBytes, theoreticalIndexCapacityBytes, managedGeometryArrayPayloadBytes;
    public long ownedUnityObjectBytes;
    public string ownedUnityObjectBytesScope = "Unity-reported Mesh/Material object estimate; excludes atlas/managed caches; not measured GPU memory";
    public DamageTextBenchmarkPlan plan;
    public DamageTextConfiguration rendererConfiguration;
    public DamageTextPrecisionResult precision;
    public double[] registeredTimes;
    public string timeEncoding = "ieee754-f64-hex-v1";
    public string[] registeredTimeBits;
    public DamageTextBenchmarkEvent[] events;
    public DamageTextBenchmarkFrame[] frames;
    public DamageTextGpuCapture.Trace nativeGpu;
}

/// <summary>One renderer and clock per segment. No Seek, file IO, UI, or growing sample buffers in Sampling.</summary>
public sealed class DamageTextBenchmark : IDisposable
{
    private DamageTextClip _clip;
    private DamageTextConfiguration _configuration;
    private DamageTextGlyphAtlas _atlas;
    private DamageTextStyles _styles;
    private DamageTextStyle[] _frozenStyles;
    private Shader _shader;
    private Camera _camera;
    private DamageTextBenchmarkPlan _plan;
    private DamageTextPositionFormat _selectedFormat;
    private DamageTextBenchmarkReport _report;
    private DamageTextBenchmarkFrame[] _frames;
    private DamageTextBenchmarkEvent[] _events;
    private double[] _registeredTimes;
    private int[] _endOrder, _characters;
    private float[] _windowEnds;
    private int _windowEndCount;
    private int _cursor, _endCursor, _frame, _sample, _warmup, _barrier, _count, _expectedLiveEvents, _expectedLiveCharacters;
    private long _expectedLiveChecksum;
    private double _time;
    private bool _disposed;
    public DamageTextRenderer Renderer { get; private set; }
    public DamageTextBenchmarkStage Stage { get; private set; }
    public int SampleCount => _sample;
    public float PlaybackTime => (float)_time;
    public DamageTextBenchmarkReport Report
    {
        get
        {
            if (_report != null && !IsRunning && _report.frames == null) FinalizeReport();
            return _report;
        }
    }
    public bool IsReportFinalized => _report?.frames != null;
    public bool IsRunning => _report != null && Stage != DamageTextBenchmarkStage.Completed && Stage != DamageTextBenchmarkStage.Aborted;
    public int LastFrameIndex => _count - 1;

    public void Prepare(DamageTextClip clip, DamageTextConfiguration configuration, DamageTextGlyphAtlas atlas,
        DamageTextStyles styles, Shader shader, Camera camera, DamageTextBenchmarkPlan plan)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DamageTextBenchmark));
        Abort("prepare"); Renderer?.Dispose(); _report = null;
        if (clip == null || configuration == null || atlas == null || styles == null || shader == null || camera == null || plan == null)
            throw new ArgumentNullException();
        plan = plan.Copy(); plan.Validate(clip.Duration); configuration = configuration.Copy(); configuration.Validate();
        string expectedShader = plan.Geometry == DamageTextGeometry.InstancedQuad ? "UIData/DamageTextInstanced" : "UIData/DamageText";
        if (shader.name != expectedShader || !shader.isSupported) throw new NotSupportedException("Benchmark requires the supported frozen baseline Shader: " + expectedShader);
        if (clip.Count > configuration.MaximumSessionEvents || clip.Duration > configuration.MaximumTime)
            throw new ArgumentException("Clip exceeds session bounds.");
        _clip = clip; _configuration = configuration; _atlas = atlas; _styles = styles; _shader = shader; _camera = camera; _plan = plan;
        _frozenStyles = styles.Freeze(out _);
        Stage = DamageTextBenchmarkStage.Preparing;
        long start = Stopwatch.GetTimestamp();
        _cursor = _endCursor = _frame = _sample = _warmup = _count = _expectedLiveEvents = _expectedLiveCharacters = 0;
        _expectedLiveChecksum = 0; _barrier = 0; _time = 0;
        _oracleSpawnCursor = _windowCursor = _windowEndCount = 0;
        _frames = new DamageTextBenchmarkFrame[plan.MaximumRealtimeFrames + plan.WarmupFrames + 4];
        _events = new DamageTextBenchmarkEvent[clip.Count]; _endOrder = new int[clip.Count]; _characters = new int[clip.Count];
        _windowEnds = new float[clip.Count];
        var atlasSnapshot = atlas.Freeze();
        int burst = 0; float lastSpawn = -1;
        for (int i = 0; i < clip.Count; i++)
        {
            var request = clip.GetEvent(i);
            if (!atlasSnapshot.Contains(request.Amount) || request.SpawnTime + _frozenStyles[(int)request.Kind].Lifetime > clip.Duration)
                throw new ArgumentException("Clip event exceeds fixed glyph/lifetime bounds.");
            if (request.SpawnTime != lastSpawn) _windowEndCount++;
            burst = request.SpawnTime == lastSpawn ? burst + 1 : 1; lastSpawn = request.SpawnTime;
            if (burst > configuration.MaximumPendingEvents || burst > configuration.BucketCapacity * configuration.MaximumBuckets)
                throw new ArgumentException("Clip burst exceeds intake capacity.");
            _events[i] = new DamageTextBenchmarkEvent
            {
                request = request, firstPublishedAt = -1, consumedAt = -1, observedEndedAt = -1,
                endTime = request.SpawnTime + _frozenStyles[(int)request.Kind].Lifetime
            };
            _windowEnds[_windowEndCount - 1] = Mathf.Max(_windowEnds[_windowEndCount - 1], _events[i].endTime);
            uint amount = request.Amount; int digits = 0; do { digits++; amount /= 10; } while (amount != 0);
            _characters[i] = digits; _endOrder[i] = i;
        }
        Array.Sort(_endOrder, (a, b) => { int comparison = _events[a].endTime.CompareTo(_events[b].endTime); return comparison != 0 ? comparison : a.CompareTo(b); });
        Array.Sort(_windowEnds, 0, _windowEndCount);
        var times = new double[plan.Mode == DamageTextBenchmarkMode.Realtime ? 0 : plan.SampleFrames];
        for (int i = 0; i < times.Length; i++) times[i] = plan.Phase == DamageTextBenchmarkPhase.DrawOnly ? plan.Checkpoint : (double)clip.Duration * i / (times.Length - 1);
        _registeredTimes = times;
        var timeBits = new string[times.Length];
        for (int i = 0; i < times.Length; i++) timeBits[i] = BitConverter.DoubleToInt64Bits(times[i]).ToString("X16", CultureInfo.InvariantCulture);
        int expectedEvents = clip.Count;
        if (plan.Phase == DamageTextBenchmarkPhase.DrawOnly)
        { expectedEvents = 0; while (expectedEvents < clip.Count && clip.GetEvent(expectedEvents).SpawnTime <= plan.Checkpoint) expectedEvents++; }
        var workloadPlan = plan.Copy(); workloadPlan.Geometry = DamageTextGeometry.PersistentMesh; workloadPlan.PositionFormat = DamageTextPositionFormat.Float32;
        clip.EvaluateCamera(0, camera);
        _report = new DamageTextBenchmarkReport
        {
            utc = DateTime.UtcNow.ToString("O"), plan = plan.Copy(), rendererConfiguration = configuration.Copy(), registeredTimes = (double[])times.Clone(), expectedEvents = expectedEvents,
            registeredTimeBits = timeBits,
            intervalStart = 0, intervalEnd = plan.Phase == DamageTextBenchmarkPhase.DrawOnly ? plan.Checkpoint : clip.Duration,
            clockPolicy = plan.Mode == DamageTextBenchmarkMode.Realtime ? "realtime-unscaled-delta-v1" : "pre-registered-times-v1",
            formal = plan.Mode != DamageTextBenchmarkMode.Visual,
            clipHash = clip.Hash, buildGuid = Application.buildGUID, unityVersion = Application.unityVersion,
            device = SystemInfo.deviceModel, operatingSystem = SystemInfo.operatingSystem, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            graphicsDevice = SystemInfo.graphicsDeviceName, width = camera.pixelWidth, height = camera.pixelHeight,
            colorSpace = QualitySettings.activeColorSpace.ToString(), processorType = SystemInfo.processorType,
            shaderName = shader.name,
            processorCount = SystemInfo.processorCount, systemMemoryMB = SystemInfo.systemMemorySize,
            targetFrameRate = Application.targetFrameRate, vSyncCount = QualitySettings.vSyncCount, qualityLevel = QualitySettings.GetQualityLevel(),
            buildType = UnityEngine.Debug.isDebugBuild ? "Development" : "Release",
            cameraHash = CameraHash(camera), planHash = DamageTextHash.Compute(w => w.Write(JsonUtility.ToJson(plan))),
            workloadHash = DamageTextHash.Compute(w => w.Write(JsonUtility.ToJson(workloadPlan))),
            timesHash = DamageTextHash.Compute(w => { w.Write("damage-times-v1"); foreach (double value in times) w.Write(value); }),
            expectedRequestHash = RequestHash(_events, expectedEvents),
#if ENABLE_IL2CPP
            backend = "IL2CPP",
#else
            backend = "Mono",
#endif
            executionMode = Application.isEditor ? "Editor" : "Player"
        };
        _selectedFormat = plan.PositionFormat;
        if (plan.PositionFormat == DamageTextPositionFormat.Float16)
        {
            _report.precision = DamageTextPrecisionAudit.Evaluate(clip, atlasSnapshot, _frozenStyles);
            if (!_report.precision.candidatePassed) _selectedFormat = DamageTextPositionFormat.Float32;
        }
        ResetRenderer();
        if (plan.Phase == DamageTextBenchmarkPhase.DrawOnly) ReachCheckpoint();
        _report.atlasHash = Renderer.AtlasHash; _report.styleHash = Renderer.StyleHash; _report.configurationHash = Renderer.ConfigurationHash;
        _report.scheme = Renderer.Scheme; _report.requestedFormat = plan.PositionFormat.ToString();
        _report.effectiveFormat = Renderer.EffectivePositionFormat.ToString();
        _report.vertexLayout = Renderer.VertexLayout; _report.shaderPassCount = Renderer.ShaderPassCount;
        _report.formatReason = _selectedFormat != plan.PositionFormat ? "precision-exceeds-0.25px-candidate-threshold" : Renderer.FormatReason;
        CaptureCapacity();
        _report.preparationMs = Milliseconds(start);
        Stage = plan.WarmupFrames == 0 ? DamageTextBenchmarkStage.Sampling : DamageTextBenchmarkStage.Warming;
    }

    private void ResetRenderer()
    {
        Renderer?.Dispose(); Renderer = new DamageTextRenderer(geometry: _plan.Geometry, positionFormat: _selectedFormat);
        Renderer.Prepare(_configuration, _atlas, _styles, _shader); Renderer.BindCamera(_camera);
    }
    private void ReachCheckpoint()
    {
        int first = _cursor; DispatchAndTick(0); ObservePublications(first);
        while (_time < _plan.Checkpoint)
        { first = _cursor; DispatchAndTick(Math.Min(_plan.Checkpoint, _time + _plan.PreparationStep)); ObservePublications(first); }
    }
    public void Advance(double elapsedSeconds, int unityFrame)
    {
        if (!IsRunning) return;
        if (!Renderer.CanDraw) { Finish(true, "draw-resource-unavailable-or-changed"); return; }
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (Stage == DamageTextBenchmarkStage.Barrier)
        {
            AddFrame(new DamageTextBenchmarkFrame { stage = Stage, unityFrame = unityFrame, time = PlaybackTime,
                mainThreadMs = -1, renderThreadMs = -1, gpuMs = -1, gcBytes = -1 });
            if (--_barrier == 0) Stage = DamageTextBenchmarkStage.Sampling;
            return;
        }
        var frame = new DamageTextBenchmarkFrame { stage = Stage, unityFrame = unityFrame, index = _sample, frameIntervalMs = elapsedSeconds * 1000,
            mainThreadMs = -1, renderThreadMs = -1, gpuMs = -1, gcBytes = -1 };
        try
        {
            long start = Stopwatch.GetTimestamp();
            int firstConsumed = _cursor;
            var before = Renderer.Statistics;
            if (_plan.Phase == DamageTextBenchmarkPhase.FullLifecycle)
            {
                double time = Stage == DamageTextBenchmarkStage.Warming ? Math.Min(_clip.Duration, _warmup * _plan.PreparationStep) :
                    _plan.Mode == DamageTextBenchmarkMode.Realtime ? Math.Min(_clip.Duration, _time + (_sample == 0 ? 0 : elapsedSeconds)) :
                    _registeredTimes[_sample];
                DispatchAndTick(time);
            }
            frame.outerUpdateMs = Milliseconds(start);
            start = Stopwatch.GetTimestamp();
            ObservePublications(firstConsumed);
            ObserveOracle();
            frame.time = PlaybackTime; frame.statistics = Renderer.Statistics; frame.consumed = _cursor;
            frame.expectedLiveEvents = _expectedLiveEvents; frame.expectedLiveCharacters = _expectedLiveCharacters;
            frame.expectedLiveChecksum = _expectedLiveChecksum; frame.actualLiveChecksum = frame.statistics.LiveEventChecksum;
            frame.stateMatches = frame.statistics.LiveEvents == _expectedLiveEvents && frame.statistics.LiveCharacters == _expectedLiveCharacters &&
                frame.actualLiveChecksum == _expectedLiveChecksum;
            frame.refreshFrame = frame.statistics.Rebuilds != before.Rebuilds;
            frame.completedWindows = CompletedWindows(); frame.telemetryMs = Milliseconds(start);
            AddFrame(frame);
            if (Stage == DamageTextBenchmarkStage.Warming)
            {
                if (++_warmup >= _plan.WarmupFrames) EndWarmup();
                return;
            }
            _sample++;
            bool finished = _plan.Phase == DamageTextBenchmarkPhase.FullLifecycle && _plan.Mode == DamageTextBenchmarkMode.Realtime ?
                PlaybackTime >= _clip.Duration : _sample >= _plan.SampleFrames;
            if (finished) Finish(false, "completed");
            else if (_sample >= _plan.MaximumRealtimeFrames) Finish(true, "realtime-buffer-exhausted");
        }
        catch (Exception error) { Finish(true, "failure: " + error.Message); }
    }
    private void EndWarmup()
    {
        if (_plan.Phase == DamageTextBenchmarkPhase.FullLifecycle)
        {
            long start = Stopwatch.GetTimestamp();
            ResetRenderer();
            if (Renderer.EffectivePositionFormat.ToString() != _report.effectiveFormat || Renderer.VertexLayout != _report.vertexLayout ||
                Renderer.AtlasHash != _report.atlasHash || Renderer.StyleHash != _report.styleHash || Renderer.ConfigurationHash != _report.configurationHash)
            { _report.resetBarrierMs = Milliseconds(start); Finish(true, "resources-or-format-changed-during-warmup-reset"); return; }
            _cursor = _endCursor = _frame = _expectedLiveEvents = _expectedLiveCharacters = 0; _expectedLiveChecksum = 0; _time = 0;
            _oracleSpawnCursor = _windowCursor = 0;
            for (int i = 0; i < _events.Length; i++)
            {
                _events[i].consumedAt = _events[i].firstPublishedAt = _events[i].observedEndedAt = -1;
                _events[i].missed = false;
            }
            _report.resetBarrierMs = Milliseconds(start); _barrier = 2; Stage = DamageTextBenchmarkStage.Barrier;
        }
        else Stage = DamageTextBenchmarkStage.Sampling;
    }
    private void DispatchAndTick(double time)
    {
        float renderedTime = (float)time;
        while (_cursor < _clip.Count && _events[_cursor].request.SpawnTime <= renderedTime)
        {
            _events[_cursor].consumedAt = renderedTime;
            _events[_cursor].result = Renderer.Spawn(_events[_cursor].request); _cursor++;
        }
        Renderer.Tick(renderedTime, _frame++); _time = time; _clip.EvaluateCamera(renderedTime, _camera);
        if (Renderer.Status != DamageTextRendererStatus.Ready) throw Renderer.LastError ?? new InvalidOperationException("Renderer failed.");
    }
    private void ObservePublications(int first)
    {
        for (int i = first; i < _cursor; i++)
        {
            if (Renderer.TryGetEvent(_events[i].request.EventId, out var state) && state.Published)
                _events[i].firstPublishedAt = state.FirstPublishedTime;
            _events[i].missed = _events[i].result == DamageTextSpawnResult.Accepted && _events[i].endTime <= PlaybackTime && _events[i].firstPublishedAt < 0;
        }
    }
    private int _oracleSpawnCursor;
    private void ObserveOracle()
    {
        // O(births + ends), not O(all historical events) on every frame.
        while (_oracleSpawnCursor < _cursor)
        {
            int i = _oracleSpawnCursor++; _expectedLiveEvents++; _expectedLiveCharacters += _characters[i];
            _expectedLiveChecksum ^= DamageTextEventChecksum.Compute(_events[i].request);
        }
        while (_endCursor < _endOrder.Length && _events[_endOrder[_endCursor]].endTime <= PlaybackTime)
        {
            int i = _endOrder[_endCursor++]; _events[i].observedEndedAt = PlaybackTime;
            _expectedLiveEvents--; _expectedLiveCharacters -= _characters[i]; _expectedLiveChecksum ^= DamageTextEventChecksum.Compute(_events[i].request);
        }
    }
    private int CompletedWindows()
    {
        while (_windowCursor < _windowEndCount && _windowEnds[_windowCursor] <= PlaybackTime) _windowCursor++;
        return _windowCursor;
    }
    private int _windowCursor;
    private void AddFrame(DamageTextBenchmarkFrame frame)
    {
        if (_count >= _frames.Length) throw new InvalidOperationException("Recorder capacity exceeded.");
        _frames[_count++] = frame;
    }
    public void AttachPreviousFrameCounters(int index, double mainMs, double renderMs, long gcBytes,
        bool mainAvailable, bool renderAvailable, bool gcAvailable)
    {
        if (_frames == null || index < 0 || index >= _count) return;
        ref var frame = ref _frames[index];
        frame.mainThreadAvailable = mainAvailable; frame.mainThreadMs = mainAvailable ? mainMs : -1;
        frame.renderThreadAvailable = renderAvailable; frame.renderThreadMs = renderAvailable ? renderMs : -1;
        frame.gcAvailable = gcAvailable; frame.gcBytes = gcAvailable ? gcBytes : -1;
        if (_report.frames != null && index < _report.frames.Length) _report.frames[index] = frame;
    }
    public void Abort(string reason) { if (IsRunning) Finish(true, reason); }
    private void Finish(bool aborted, string reason)
    {
        _report.aborted = aborted; _report.reason = reason; _report.samples = _sample; _report.consumedEvents = _cursor;
        _report.completeEventInterval = _cursor == _report.expectedEvents;
        _report.completedWindows = _windowCursor;
        Stage = aborted ? DamageTextBenchmarkStage.Aborted : DamageTextBenchmarkStage.Completed;
    }
    private void FinalizeReport()
    {
        // The runner reads Report on the following Update, after attaching the last sampled frame's counters.
        // SHA256, trimming and serialization must not allocate inside the final sampled Unity frame.
        _report.consumedRequestHash = RequestHash(_events, _cursor);
        _report.frames = new DamageTextBenchmarkFrame[_count]; Array.Copy(_frames, _report.frames, _count);
        _report.events = new DamageTextBenchmarkEvent[_cursor]; Array.Copy(_events, _report.events, _cursor);
    }
    private void CaptureCapacity()
    {
        _report.ownedUnityObjectBytes = Renderer.GetOwnedObjectBytes();
        long characters = (long)_configuration.BucketCapacity * 8;
        if (_plan.Geometry == DamageTextGeometry.InstancedQuad)
        {
            _report.theoreticalVertexCapacityBytes = 4 * Renderer.Statistics.VertexStride;
            _report.theoreticalIndexCapacityBytes = 6 * 2;
            long groups = (characters + 127) / 128;
            _report.managedGeometryArrayPayloadBytes = groups * _configuration.MaximumBuckets * 2 * 128 * 96 + 128 * 64;
        }
        else
        {
            _report.theoreticalVertexCapacityBytes = characters * 4 * Renderer.Statistics.VertexStride * 2 * _configuration.MaximumBuckets;
            _report.theoreticalIndexCapacityBytes = characters * 6 * 2 * 2 * _configuration.MaximumBuckets;
            _report.managedGeometryArrayPayloadBytes = characters * 4 * (68 + (Renderer.EffectivePositionFormat == DamageTextPositionFormat.Float16 ? 64 : 0));
        }
    }
    public static string CameraHash(Camera camera) => DamageTextHash.Compute(w =>
    {
        w.Write("damage-camera-v1"); w.Write(camera.orthographic); w.Write(camera.orthographicSize); w.Write(camera.fieldOfView);
        w.Write(camera.nearClipPlane); w.Write(camera.farClipPlane); w.Write(camera.pixelWidth); w.Write(camera.pixelHeight);
        var p = camera.transform.position; var r = camera.transform.rotation;
        w.Write(p.x); w.Write(p.y); w.Write(p.z); w.Write(r.x); w.Write(r.y); w.Write(r.z); w.Write(r.w);
        w.Write(camera.rect.x); w.Write(camera.rect.y); w.Write(camera.rect.width); w.Write(camera.rect.height);
    });
    public static string RequestHash(DamageTextBenchmarkEvent[] events, int count) => DamageTextHash.Compute(w =>
    { w.Write("damage-benchmark-events-v1"); w.Write(count); for (int i = 0; i < count; i++) DamageTextHash.Write(w, events[i].request); });
    private static double Milliseconds(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    public void Dispose()
    {
        if (_disposed) return;
        try { Abort("dispose"); } finally { Renderer?.Dispose(); _disposed = true; }
    }
}

/// <summary>Serialization is deliberately performed after the segment, never inside a sampled frame.</summary>
public static class DamageTextBenchmarkFiles
{
    [Serializable] private sealed class Manifest { public string schema = "damage-benchmark-artifacts-v1"; public string reportSHA256, csvSHA256; }
    public static string Save(DamageTextBenchmarkReport report, string directory)
    {
        if (report == null || report.frames == null) throw new InvalidOperationException("Finish/abort the segment before exporting.");
        Directory.CreateDirectory(directory);
        string stem = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N");
        string reportPath = Path.Combine(directory, stem + ".report.json"), csvPath = Path.Combine(directory, stem + ".frames.csv");
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        var csv = new StringBuilder("unityFrame,index,stage,time,outerUpdateMs,telemetryMs,frameIntervalMs,mainThreadMs,renderThreadMs,gpuMs,gcBytes,consumed,live,submitted,expiredResident,buckets,groups,stride,draws,passes,refresh,stateMatches,completedWindows\n");
        foreach (var f in report.frames)
        {
            csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3:R},{4:R},{5:R},{6:R},{7:R},{8:R},{9:R},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22}\n",
                f.unityFrame, f.index, (int)f.stage, f.time, f.outerUpdateMs, f.telemetryMs, f.frameIntervalMs, f.mainThreadMs, f.renderThreadMs,
                f.gpuMs, f.gcBytes, f.consumed, f.statistics.LiveCharacters, f.statistics.SubmittedCharacters, f.statistics.ExpiredResidentCharacters,
                f.statistics.Buckets, f.statistics.InstanceGroups, f.statistics.VertexStride, f.statistics.Draws, f.statistics.Passes,
                f.refreshFrame ? 1 : 0, f.stateMatches ? 1 : 0, f.completedWindows);
        }
        File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, stem + ".manifest.json"), JsonUtility.ToJson(new Manifest
        { reportSHA256 = HashFile(reportPath), csvSHA256 = HashFile(csvPath) }, true), new UTF8Encoding(false));
        return reportPath;
    }
    public static string HashFile(string path)
    {
        using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
    }
}
