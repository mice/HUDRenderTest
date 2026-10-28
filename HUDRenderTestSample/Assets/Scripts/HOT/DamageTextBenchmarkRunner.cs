using System;
using System.IO;
using Unity.Profiling;
using UnityEngine;

[Serializable]
public sealed class DamageTextCaptureRequest
{
    public string schema, runId, clipName, sourceFingerprint, visualEvidenceSHA256;
    public DamageTextBenchmarkPlan plan;
    public bool captureNativeGpu;
    public string gpuCalibrationSHA256;
}

/// <summary>Sample-only controls/counters. Session work is timed without UI formatting or file writes.</summary>
public sealed class DamageTextBenchmarkRunner : MonoBehaviour
{
    public Camera outputCamera;
    public DamageTextGlyphAtlas atlas;
    public DamageTextStyles styles;
    public DamageTextClipAsset clip;
    public DamageTextClipAsset[] captureClips = Array.Empty<DamageTextClipAsset>();
    public Shader meshShader, instanceShader;
    public DamageTextConfiguration configuration = new DamageTextConfiguration { MaximumSessionEvents = 65536 };
    public DamageTextBenchmarkPlan plan = new DamageTextBenchmarkPlan();
    public string sourceCommit = "unknown", sourceStatus = "unknown", sourceFingerprint = "unknown", packageVersion = "unknown";
    public string visualEvidenceSHA256 = "unknown";
    public bool autoRun, autoExport = true, showControls = true, sideBySidePreview = true;
    public bool readCaptureRequestOnStart;
    public bool nativeGpuTimingSupported;
    public string nativeGpuPluginSHA256;
    public static string CaptureRequestPath => Path.Combine(Application.persistentDataPath, "DamageTextBenchmark", "capture-request.json");
    public DamageTextBenchmark Session { get; private set; }
    public string LastReportPath { get; private set; }
    public string Failure { get; private set; }
    private ProfilerRecorder _mainThread, _renderThread, _gc;
    private int _pending = -1;
    private bool _savedSettings, _exported;
    private int _oldVsync, _oldFrameRate;
    private float _oldTimeScale;
    private string _display = "Ready";
    private float _nextDisplay;
    private bool _configurationChanged;
    private DamageTextComparisonPreview _preview;
    private int _preparedWidth, _preparedHeight, _preparedQuality;
    private string _captureRunId, _captureRequestHash, _captureSource, _capturePlan;
    private DamageTextClipAsset _captureClip;
    private bool _captureNativeGpu;
    private string _captureGpuCalibration;
    private DamageTextGpuCapture _gpuCapture;
    private int _gpuDrain;

    private void Start()
    {
        try { if (readCaptureRequestOnStart) ApplyCaptureRequest(File.ReadAllText(CaptureRequestPath)); }
        catch (Exception error) { Failure = "Capture request rejected: " + error.Message; return; }
        if (autoRun) Begin();
    }
    /// <summary>Load once before Preparing; one installed build can run every preregistered ABBA segment.</summary>
    public void ApplyCaptureRequest(string json)
    {
        if (Session != null || _preview != null) throw new InvalidOperationException("End the current session before changing capture requests.");
#if UNITY_EDITOR
        RefreshSourceMetadata();
#endif
        var request = JsonUtility.FromJson<DamageTextCaptureRequest>(json);
        if (request == null || request.schema != "damage-text-capture-request-v1" || request.plan == null ||
            string.IsNullOrWhiteSpace(request.runId) || request.runId.Length > 128 || string.IsNullOrWhiteSpace(request.clipName) ||
            !IsHash(request.sourceFingerprint) || !string.Equals(request.sourceFingerprint, sourceFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Capture schema, run ID, Clip and exact source fingerprint are required.");
        DamageTextClipAsset selected = null;
        foreach (var candidate in captureClips ?? Array.Empty<DamageTextClipAsset>())
        {
            if (candidate == null || candidate.name != request.clipName) continue;
            if (selected != null) throw new ArgumentException("Capture Clip name is ambiguous.");
            selected = candidate;
        }
        if (selected == null) throw new ArgumentException("Capture Clip is not embedded in this build.");
        request.plan.Validate(selected.Freeze().Duration);
        if (request.plan.Mode != DamageTextBenchmarkMode.Visual && request.plan.SampleFrames < 3000)
            throw new ArgumentException("Formal capture plans require at least 3,000 registered samples.");
        if (request.captureNativeGpu && (!nativeGpuTimingSupported || !IsHash(nativeGpuPluginSHA256) ||
            !IsHash(request.gpuCalibrationSHA256) || request.plan.Mode == DamageTextBenchmarkMode.Visual))
            throw new ArgumentException("Native GPU capture requires a supported build and exact calibration hash.");
        string hash;
        using (var sha = System.Security.Cryptography.SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))).Replace("-", "");
        clip = selected; plan = request.plan.Copy(); visualEvidenceSHA256 = request.visualEvidenceSHA256;
        showControls = plan.Mode == DamageTextBenchmarkMode.Visual; autoExport = true;
        _captureRunId = request.runId; _captureRequestHash = hash; _captureSource = request.sourceFingerprint;
        _capturePlan = JsonUtility.ToJson(plan); _captureClip = selected;
        _captureNativeGpu = request.captureNativeGpu; _captureGpuCalibration = request.gpuCalibrationSHA256;
    }
    public void Begin()
    {
        EndSession("restart");
        Failure = null; LastReportPath = null; _exported = false; _pending = -1; _configurationChanged = false;
        _gpuDrain = 0;
        try
        {
#if UNITY_EDITOR
            RefreshSourceMetadata();
#endif
            if (_captureRequestHash != null && (!string.Equals(_captureSource, sourceFingerprint, StringComparison.OrdinalIgnoreCase) ||
                clip != _captureClip || JsonUtility.ToJson(plan) != _capturePlan))
                throw new InvalidOperationException("Source, Clip or plan changed after capture registration.");
            if (outputCamera == null || atlas == null || styles == null || clip == null) throw new InvalidOperationException("Bind benchmark camera/resources/clip.");
            if (plan.Mode == DamageTextBenchmarkMode.Visual)
            {
                _preview = new DamageTextComparisonPreview();
                _preview.Open(outputCamera, clip.Freeze(), configuration, atlas, styles, meshShader, instanceShader,
                    sideBySidePreview, plan.Geometry, plan.PositionFormat);
                return;
            }
            _oldVsync = QualitySettings.vSyncCount; _oldFrameRate = Application.targetFrameRate; _oldTimeScale = Time.timeScale; _savedSettings = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60; Time.timeScale = 1;
            Session = new DamageTextBenchmark();
            Session.Prepare(clip.Freeze(), configuration, atlas, styles,
                plan.Geometry == DamageTextGeometry.InstancedQuad ? instanceShader : meshShader, outputCamera, plan);
            var report = Session.Report;
            _preparedWidth = report.width; _preparedHeight = report.height; _preparedQuality = report.qualityLevel;
            report.commit = sourceCommit; report.sourceStatus = sourceStatus; report.sourceFingerprint = sourceFingerprint; report.packageVersion = packageVersion;
            report.controlsVisible = showControls; report.formal &= !showControls;
            report.visualEvidenceSHA256 = visualEvidenceSHA256;
            report.captureRunId = _captureRunId; report.captureRequestSHA256 = _captureRequestHash;
            report.visualStatus = IsHash(visualEvidenceSHA256) ? "passed" : "unverified";
            if (_captureNativeGpu)
            {
                _gpuCapture = new DamageTextGpuCapture(outputCamera, plan, nativeGpuPluginSHA256, _captureGpuCalibration);
                report.gpuStatus = "pending-native-timestamps"; report.gpuScope = DamageTextGpuCapture.Scope;
            }
            _mainThread = TryRecorder(ProfilerCategory.Internal, "Main Thread");
            _renderThread = TryRecorder(ProfilerCategory.Internal, "Render Thread");
#if !ENABLE_IL2CPP
            _gc = TryRecorder(ProfilerCategory.Memory, "GC Allocated In Frame");
            report.gcStatus = _gc.Valid ? "profiler-frame-counter; availability checked per frame" : "unavailable";
#else
            // Do not call GC.GetAllocatedBytesForCurrentThread on the known unsafe IL2CPP path.
            report.gcStatus = "unavailable: IL2CPP counters not validated";
#endif
        }
        catch (Exception error) { Failure = error.Message; EndSession("failure: " + Failure); }
    }
    private static bool IsHash(string value) => !string.IsNullOrEmpty(value) && value.Length == 64 && System.Text.RegularExpressions.Regex.IsMatch(value, "\\A[0-9a-fA-F]+\\z");
    private static ProfilerRecorder TryRecorder(ProfilerCategory category, string name)
    {
        try { return ProfilerRecorder.StartNew(category, name, 1); } catch (ArgumentException) { return default; }
    }
    private void Update()
    {
        if (_preview != null) { _preview.Advance(Time.unscaledDeltaTime); return; }
        if (Session == null) return;
        if (_gpuCapture != null && Time.frameCount < _gpuCapture.FirstUnityFrame) return;
        if (_configurationChanged) { Session.Abort("inspector-config-change"); _configurationChanged = false; }
        if (Session.IsRunning && (outputCamera == null || outputCamera.pixelWidth != _preparedWidth || outputCamera.pixelHeight != _preparedHeight ||
            QualitySettings.GetQualityLevel() != _preparedQuality || QualitySettings.vSyncCount != 0 || Application.targetFrameRate != 60 || Time.timeScale != 1))
            Session.Abort("environment-or-viewport-change");
        if (_pending >= 0)
        {
            Session.AttachPreviousFrameCounters(_pending, _mainThread.Valid ? _mainThread.LastValue * 1e-6 : -1,
                _renderThread.Valid ? _renderThread.LastValue * 1e-6 : -1,
                _gc.Valid ? _gc.LastValue : -1, _mainThread.Valid && _mainThread.Count > 0,
                _renderThread.Valid && _renderThread.Count > 0, _gc.Valid && _gc.Count > 0);
            _pending = -1;
        }
        if (Session.IsRunning)
        {
            Session.Advance(Time.unscaledDeltaTime, Time.frameCount); _pending = Session.LastFrameIndex;
            if (_gpuCapture != null)
            {
                try { _gpuCapture.QueueFrame(Time.frameCount); }
                catch (Exception error) { Session.Abort("native-gpu-sequence: " + error.Message); }
            }
        }
        else if (!_exported)
        {
            if (_gpuCapture != null && !Session.Report.aborted && _gpuDrain < DamageTextGpuCapture.DrainFrames)
            {
                try { _gpuCapture.QueueFrame(Time.frameCount); _gpuDrain++; return; }
                catch (Exception error) { Session.Abort("native-gpu-drain: " + error.Message); }
            }
            if (_gpuCapture != null)
            {
                try { _gpuCapture.Complete(Session.Report, _gpuDrain); }
                catch (Exception error) { Failure = "Native GPU export failed: " + error.Message; Session.Report.gpuStatus = "incomplete-native-timestamps"; }
                finally { _gpuCapture.Dispose(); _gpuCapture = null; }
            }
            _exported = true;
            try { if (autoExport) LastReportPath = DamageTextBenchmarkFiles.Save(Session.Report, Path.Combine(Application.persistentDataPath, "DamageTextBenchmark")); }
            catch (Exception error) { Failure = "Export failed: " + error.Message; }
            finally { RestoreSettings(); DisposeCounters(); }
        }
        // UI formatting is outside the session's outer CPU measurement; full-frame metrics still include it.
        // Hide controls for formal timing to avoid IMGUI/frame telemetry contamination.
        if (showControls && Time.unscaledTime >= _nextDisplay)
        {
            _nextDisplay = Time.unscaledTime + .5f;
            _display = Session.Stage + " · " + Session.SampleCount + " frames · " + Session.PlaybackTime.ToString("F3") + "s";
        }
    }
    public void Abort(string reason) { Session?.Abort(reason); }
    public void Stop() { EndSession("stopped"); }
    public void ChangeScheme(DamageTextGeometry geometry, DamageTextPositionFormat format)
    {
        Abort("scheme-or-format-change"); plan.Geometry = geometry; plan.PositionFormat = format;
    }
    public void ChangeTimeOrSpeed() { Abort("pause-seek-step-or-speed-change"); }
    private void OnGUI()
    {
        if (!showControls) return;
        GUILayout.BeginArea(new Rect(12, 12, 620, 220), GUI.skin.box);
        GUILayout.Label("Damage text renderer comparison"); GUILayout.Label(_display);
        if (_preview != null) _preview.DrawControls();
        if (!string.IsNullOrEmpty(Failure)) GUILayout.Label(Failure);
        if (GUILayout.Button("Start configured segment")) Begin();
        if (GUILayout.Button("Abort segment")) Abort("user-abort");
        GUILayout.Label("Hide controls before formal timing. GPU unavailable; device results pending.");
        GUILayout.EndArea();
    }
    private void OnApplicationPause(bool paused) { if (paused) Abort("application-pause"); }
    private void OnValidate() { _configurationChanged = true; }
    private void OnApplicationFocus(bool focused) { if (!focused) Abort("application-focus-lost"); }
    private void OnDisable() { EndSession("disabled"); }
    private void EndSession(string reason)
    {
        _gpuCapture?.Dispose(); _gpuCapture = null;
        _preview?.Dispose(); _preview = null;
        if (Session != null)
        {
            try
            {
                Session.Abort(reason);
                if (!_exported && autoExport && Session.Report?.frames != null)
                    LastReportPath = DamageTextBenchmarkFiles.Save(Session.Report, Path.Combine(Application.persistentDataPath, "DamageTextBenchmark"));
            }
            catch (Exception error) { Failure = "Export failed: " + error.Message; }
            finally { Session.Dispose(); Session = null; DisposeCounters(); RestoreSettings(); }
        }
        DisposeCounters(); RestoreSettings();
    }
    private void RestoreSettings()
    {
        if (!_savedSettings) return;
        QualitySettings.vSyncCount = _oldVsync; Application.targetFrameRate = _oldFrameRate; Time.timeScale = _oldTimeScale; _savedSettings = false;
    }
    private void DisposeCounters() { _mainThread.Dispose(); _renderThread.Dispose(); _gc.Dispose(); }
#if UNITY_EDITOR
    [Serializable] private sealed class PackageMetadata { public string version; }
    public void RefreshSourceMetadata()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")); string repository = Path.GetFullPath(Path.Combine(project, ".."));
        sourceCommit = ReadGit(repository, "rev-parse HEAD");
        string status = ReadGit(repository, "status --porcelain"); sourceStatus = status == "unknown" ? status : string.IsNullOrEmpty(status) ? "clean" : "dirty";
        sourceFingerprint = CalculateSourceFingerprint(project);
        packageVersion = JsonUtility.FromJson<PackageMetadata>(File.ReadAllText(Path.Combine(repository, "com.uidatarender/package.json"))).version;
    }
    public static string CalculateSourceFingerprint(string project)
    {
        return DamageTextHash.Compute(writer =>
        {
            writer.Write("damage-comparison-source-v1"); writer.Write(DamageTextPlaybackRunner.CalculateSourceFingerprint(project));
            foreach (string relative in new[] { "Assets/DamageTextComparison", "ProjectSettings/ProjectSettings.asset" })
            {
                string root = Path.Combine(project, relative);
                string[] paths = File.Exists(root) ? new[] { root } : Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                Array.Sort(paths, StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    writer.Write(relative + "/" + (File.Exists(root) ? "" : path.Substring(root.Length + 1).Replace('\\', '/')));
                    byte[] bytes = File.ReadAllBytes(path); writer.Write(bytes.Length); writer.Write(bytes);
                }
            }
        });
    }
    private static string ReadGit(string directory, string arguments)
    {
        try
        {
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", arguments)
            { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }))
            {
                string output = process.StandardOutput.ReadToEnd().Trim(); process.StandardError.ReadToEnd(); process.WaitForExit();
                return process.ExitCode == 0 ? output : "unknown";
            }
        }
        catch (System.ComponentModel.Win32Exception) { return "unknown"; }
    }
#endif
}
