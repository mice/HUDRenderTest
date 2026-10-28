using System;
using System.IO;
using UnityEngine;

/// <summary>Preview/UI adapter. Detailed renderer comparisons belong to the subsequent change.</summary>
public sealed class DamageTextPlaybackRunner : MonoBehaviour
{
    public Camera outputCamera;
    public DamageTextGlyphAtlas atlas;
    public DamageTextStyles styles;
    public Shader shader;
    public DamageTextClipAsset[] clips;
    public DamageTextConfiguration configuration = new DamageTextConfiguration();
    public bool autoPlay = true, showControls = true, showStatistics = true, exportSegments = true;
    public float fixedStep = 1f / 60f;
    public string sourceCommit = "unknown", sourceStatus = "unknown", sourceFingerprint = "unknown";
    public string packageVersion = "unknown";
    private DamageTextPlayback _playback;
    private int _clipIndex;
    private string[] _clipNames;
    private string _statisticsText = "", _jumpText = "0", _failure;
    private float _nextStatistics;
    private bool _stepRequested;
    private bool _started;
    public DamageTextPlayback Playback => _playback;
    public string LastReportPath { get; private set; }

    [Serializable]
    public sealed class Report
    {
        public string schema = "damage-text-playback-v1";
        public string utc, commit, sourceStatus, sourceFingerprint, buildGuid, unityVersion, device, operatingSystem;
        public string graphicsApi, graphicsDevice, packageVersion, scheme = "B-PersistentMesh-Float32";
        public string gpuStatus = "unavailable", gcStatus = "unavailable", measurementScope = "functional-playback";
        public string clipHash, atlasHash, styleHash, configurationHash;
        public string sourceFingerprintSchema = "damage-source-v1";
        public bool traceabilityComplete;
        public bool orthographic;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Rect viewport;
        public float fieldOfView, orthographicSize, nearClipPlane, farClipPlane;
        public int width, height, restoredEvents;
        public float speed;
        public DamageTextPlaybackSegment segment;
    }

    private void Start() { _started = true; StartClip(0); }
    private void OnEnable() { if (_started) StartClip(_clipIndex); }
    private void OnDisable() { _playback?.Dispose(); _playback = null; }
    private void LateUpdate()
    {
        if (_playback == null || _playback.Clip == null) return;
        try
        {
            if (_stepRequested) { _stepRequested = false; _playback.Step(fixedStep); }
            else _playback.Advance(Time.unscaledDeltaTime);
        }
        catch (Exception error) { _failure = error.Message; }
        if (_playback.State == DamageTextPlaybackState.Failed) _failure = _playback.Failure;
        if (showStatistics && Time.unscaledTime >= _nextStatistics)
        {
            _nextStatistics = Time.unscaledTime + .2f;
            var s = _playback.Renderer.Statistics;
            _statisticsText = string.Format(
                "{0}  {1:F3}/{2:F2}s\nLive {3} strings / {4} digits   Submitted {5} / Expired resident {6}\nBuckets {7} / Draws {8}   Pending {9}\nAccepted {10} / Published {11} / Ended {12} / Recycled buckets {13}\nRejected {14} / Missed presentation {15}   Layouts {16} / Uploads {17}\nCamera eligible {19} / Near-plane clipped {20} · Pending age {21:F3}s\n68 B vertices · GPU / GC unavailable · {18}",
                _playback.State, _playback.PlaybackTime, _playback.Clip.Duration, s.LiveEvents, s.LiveCharacters,
                s.SubmittedCharacters, s.ExpiredResidentCharacters, s.Buckets, s.Draws, s.PendingEvents,
                s.Accepted, s.Published, s.Ended, s.RecycledBuckets, s.Rejected, s.MissedPresentation,
                s.Layouts, s.VertexUploads, _playback.Recovered ? "restored preview" : "normal replay",
                s.CameraEligibleEvents, s.NearPlaneClippedEvents, s.MaximumPendingAge);
        }
    }

    public void StartClip(int index)
    {
        try
        {
            if (clips == null || index < 0 || index >= clips.Length || clips[index] == null || outputCamera == null)
                throw new InvalidOperationException("Bind the damage-text Sample camera and clip assets.");
#if UNITY_EDITOR
            RefreshEditorSourceMetadata();
#endif
            _playback?.Dispose(); _playback = new DamageTextPlayback();
            _playback.SegmentEnded += OnSegmentEnded;
            _clipIndex = index; _failure = null; _stepRequested = false;
            _clipNames = new string[clips.Length];
            for (int i = 0; i < clips.Length; i++) _clipNames[i] = clips[i] == null ? "Missing clip" : clips[i].name;
            _playback.Prepare(clips[index].Freeze(), configuration, atlas, styles, shader, outputCamera);
            if (autoPlay) _playback.Play();
        }
        catch (Exception error) { _failure = error.Message; }
    }

    private void OnGUI()
    {
        if (!showControls && !showStatistics) return;
        GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(780, Screen.width - 24), Screen.height - 24), GUI.skin.box);
        GUILayout.Label("Damage text playback · Built-in · Float32 persistent Mesh");
        if (!string.IsNullOrEmpty(_failure)) GUILayout.Label(_failure);
        if (_playback != null && _playback.Clip != null && showControls)
        {
            try { DrawControls(); } catch (Exception error) { _failure = error.Message; }
        }
        if (showStatistics) GUILayout.Label(_statisticsText);
        GUILayout.EndArea();
    }

    private void DrawControls()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Play")) _playback.Play();
        if (GUILayout.Button("Pause")) _playback.Pause();
        if (GUILayout.Button("Restart")) { _playback.Restart(); _playback.Play(); }
        if (GUILayout.Button("Step")) { _playback.AbortSampling("step"); _playback.Pause(); _stepRequested = true; }
        if (GUILayout.Button("Sample segment")) _playback.BeginSampling();
        if (GUILayout.Button("Export state")) ExportState();
        GUILayout.EndHorizontal();
        float time = GUILayout.HorizontalSlider(_playback.PlaybackTime, 0, _playback.Clip.Duration);
        if (!Mathf.Approximately(time, _playback.PlaybackTime)) _playback.Seek(time);
        GUILayout.BeginHorizontal();
        _jumpText = GUILayout.TextField(_jumpText, GUILayout.Width(90));
        if (GUILayout.Button("Jump", GUILayout.Width(70)) && float.TryParse(_jumpText,
            System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float jump)) _playback.Seek(jump);
        GUILayout.Label("Speed " + _playback.Speed.ToString("F2"));
        float speed = GUILayout.HorizontalSlider(_playback.Speed, .1f, 4);
        if (!Mathf.Approximately(speed, _playback.Speed)) _playback.SetSpeed(speed);
        GUILayout.EndHorizontal();
        int selected = GUILayout.Toolbar(_clipIndex, _clipNames);
        if (selected != _clipIndex) { StartClip(selected); return; }
        GUILayout.BeginHorizontal();
        GUILayout.Toggle(true, "B · Persistent Mesh", GUI.skin.button);
        bool oldEnabled = GUI.enabled; GUI.enabled = false;
        GUILayout.Button("A · Instancing"); GUILayout.Button("FP16"); GUILayout.Button("D · Business ECS");
        GUI.enabled = oldEnabled; GUILayout.EndHorizontal();
    }

    private void OnSegmentEnded(DamageTextPlaybackSegment segment) { if (exportSegments) WriteReport(segment); }
    public void ExportState()
    {
        if (_playback == null) return;
        WriteReport(new DamageTextPlaybackSegment
        {
            Formal = false, Recovered = _playback.Recovered, Reason = "preview-state",
            EndTime = _playback.PlaybackTime, ConsumedEvents = _playback.ConsumedEvents,
            Statistics = _playback.Renderer.Statistics
        });
    }
    private void WriteReport(DamageTextPlaybackSegment segment)
    {
        var report = new Report
        {
            utc = DateTime.UtcNow.ToString("O"), commit = sourceCommit, sourceStatus = sourceStatus,
            sourceFingerprint = sourceFingerprint, buildGuid = Application.buildGUID,
            unityVersion = Application.unityVersion, device = SystemInfo.deviceModel, operatingSystem = SystemInfo.operatingSystem,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(), graphicsDevice = SystemInfo.graphicsDeviceName,
            packageVersion = packageVersion, width = outputCamera != null ? outputCamera.pixelWidth : 0,
            height = outputCamera != null ? outputCamera.pixelHeight : 0,
            restoredEvents = _playback.RestoredEvents, speed = _playback.Speed, segment = segment,
            clipHash = _playback.Clip.Hash, atlasHash = _playback.Renderer.AtlasHash,
            styleHash = _playback.Renderer.StyleHash, configurationHash = _playback.Renderer.ConfigurationHash,
            traceabilityComplete = sourceCommit != "unknown" && sourceFingerprint != "unknown" && sourceStatus != "unknown" && packageVersion != "unknown"
        };
        if (outputCamera != null)
        {
            report.orthographic = outputCamera.orthographic; report.cameraPosition = outputCamera.transform.position;
            report.cameraRotation = outputCamera.transform.rotation; report.viewport = outputCamera.rect;
            report.fieldOfView = outputCamera.fieldOfView; report.orthographicSize = outputCamera.orthographicSize;
            report.nearClipPlane = outputCamera.nearClipPlane; report.farClipPlane = outputCamera.farClipPlane;
        }
        string directory = Path.Combine(Application.persistentDataPath, "DamageText");
        Directory.CreateDirectory(directory);
        LastReportPath = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(LastReportPath, JsonUtility.ToJson(report, true));
    }

#if UNITY_EDITOR
    [Serializable] private sealed class PackageMetadata { public string version; }
    public void RefreshEditorSourceMetadata()
    {
        string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string repositoryPath = Path.GetFullPath(Path.Combine(projectPath, ".."));
        sourceCommit = ReadGit(repositoryPath, "rev-parse HEAD");
        string status = ReadGit(repositoryPath, "status --porcelain");
        sourceStatus = status == "unknown" ? "unknown" : string.IsNullOrEmpty(status) ? "clean" : "dirty";
        sourceFingerprint = CalculateSourceFingerprint(projectPath);
        packageVersion = JsonUtility.FromJson<PackageMetadata>(File.ReadAllText(Path.Combine(repositoryPath, "com.uidatarender/package.json"))).version;
    }
    private static string ReadGit(string directory, string arguments)
    {
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo("git", arguments)
            { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (var process = System.Diagnostics.Process.Start(start))
            {
                string output = process.StandardOutput.ReadToEnd().Trim(); process.StandardError.ReadToEnd();
                process.WaitForExit(); return process.ExitCode == 0 ? output : "unknown";
            }
        }
        catch (System.ComponentModel.Win32Exception) { return "unknown"; }
    }
    public static string CalculateSourceFingerprint(string projectPath)
    {
        return DamageTextHash.Compute(writer =>
        {
            writer.Write("damage-source-v1");
            foreach (string relativeRoot in new[]
            {
                "../com.uidatarender/Runtime", "Assets/Scripts/HOT", "Assets/DamageText",
                "../com.uidatarender/package.json", "Packages/manifest.json", "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset"
            })
            {
                string root = Path.GetFullPath(Path.Combine(projectPath, relativeRoot));
                var paths = File.Exists(root) ? new[] { root } : Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                Array.Sort(paths, StringComparer.Ordinal);
                foreach (var path in paths)
                {
                    writer.Write(relativeRoot + "/" + (File.Exists(root) ? "" : path.Substring(root.Length + 1).Replace('\\', '/')));
                    byte[] bytes = File.ReadAllBytes(path); writer.Write(bytes.Length); writer.Write(bytes);
                }
            }
        });
    }
#endif
}
