using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UIData;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>Sample-only, bounded HUD workload through the production holder/mesh/batch path.</summary>
public sealed class HudRcPerformanceRunner : MonoBehaviour
{
    private static readonly ProfilerMarker RunnerBookkeepingMarker = new ProfilerMarker("HUDRC.Runner.Bookkeeping");
    private static readonly ProfilerMarker HolderSelectionMarker = new ProfilerMarker("HUDRC.Runner.HolderSelection");
    private static readonly ProfilerMarker BuildMeshMarker = new ProfilerMarker("HUDRC.Runner.BuildMesh");
    private static readonly ProfilerMarker RebuildMarker = new ProfilerMarker("HUDRC.Runner.Rebuild");
    private static readonly ProfilerMarker DrawMarker = new ProfilerMarker("HUDRC.Runner.Draw");
    public enum Workload { Static, Partial, Burst }
    public enum RunState { Idle, Preparing, Warming, Sampling, Completed, Failed }

    [Serializable]
    public sealed class Configuration
    {
        public int holderCount = 256;
        public int quadsPerHolder = 3;
        public int partialPercent = 10;
        public int burstEveryFrames = 30;
        public int warmupFrames = 60;
        public int sampleFrames = 180;
        public int seed = 12345;
        public bool useSlim;
        public int targetFrameRate = -1;
        public Configuration Copy() => (Configuration)MemberwiseClone();
        public void Validate()
        {
            if (holderCount < 1 || holderCount > 4096 || quadsPerHolder < 1 || quadsPerHolder > 16 ||
                (long)holderCount * quadsPerHolder * 4 > 60000 || partialPercent < 1 || partialPercent > 100 ||
                burstEveryFrames < 1 || warmupFrames < 2 || sampleFrames < 2 || sampleFrames > 10000 ||
                burstEveryFrames > sampleFrames)
                throw new ArgumentOutOfRangeException(nameof(Configuration), "Invalid/beyond bounded workload configuration.");
        }
    }

    [Serializable]
    public struct FrameSample
    {
        public int unityFrame, sampleFrame, mode, updatedHolders, vertices, indices, batches;
        public double generationMs, mergeMs, drawSubmitMs, frameIntervalMs, mainThreadMs;
        public double runnerBookkeepingMs, holderSelectionMs, buildMeshMs;
        public double buildMeshSetupMs, buildMeshDataInitMs, buildMeshDispatchMs, buildMeshDoGenerateMs;
        public double buildMeshSimplePathMs, buildMeshUguiFallbackMs, buildMeshTransformMs;
        public double buildMeshSpriteDataMs, buildMeshDrawingDimensionsMs, buildMeshMeshWriteMs;
        public double buildMeshWriteStorageMs, buildMeshWriteVertexMs, buildMeshWriteUvMs, buildMeshWriteColorMs, buildMeshWriteIndexMs;
        public double mergePlanMs, mergeFillMs, meshUploadMs;
        public long generationBytes, mergeBytes, measuredThreadBytes, frameGcBytes;
        public bool mainThreadAvailable, frameGcAvailable, scopedAllocationAvailable;
        public int selectionChecksum;
    }

    [Serializable]
    public sealed class Summary
    {
        public string workload, subset;
        public int samples, mainThreadSamples, frameGcSamples;
        public double regenerationMedianMs, regenerationP95Ms, generationP95Ms, mergeP95Ms;
        public double frameIntervalMedianMs, frameIntervalP95Ms, mainThreadMedianMs, mainThreadP95Ms;
        public long generationBytesMax, mergeBytesMax, measuredThreadBytesMax;
    }

    [Serializable]
    public sealed class Report
    {
        public string schema = "hud-rc-v1";
        public string utc, unityVersion, os, cpu, graphics, executionMode, scriptingBackend;
        public string commit, sourceStatus, sourceFingerprint, packageVersion;
        public string measurementNotes;
        public Configuration configuration;
        public FrameSample[] frames;
        public Summary[] summaries;
    }

    public Configuration configuration = new Configuration();
    public Camera outputCamera;
    public Shader batchShader;
    public bool autoRun = true;
    public bool autoExport = true;
    public string sourceCommit = "unknown";
    public string sourceStatus = "unknown";
    public string sourceFingerprint = "unknown";
    public string packageVersion = "unknown";

    public RunState State { get; private set; }
    public Workload Mode => (Workload)mode;
    public int SampleCount => sampleCount;
    public int GeneratedHolderCount { get; private set; }
    public int VertexCount { get; private set; }
    public int IndexCount { get; private set; }
    public int LiveHolderCount => holders.Count;
    public string LastReportPath { get; private set; }
    public string Failure { get; private set; }
    public FrameSample GetSample(int index) => samples[index];

    private readonly List<IUIPrefabHolder> holders = new List<IUIPrefabHolder>();
    private readonly Dictionary<IUIPrefabHolder, Vector3> positions = new Dictionary<IUIPrefabHolder, Vector3>();
    private Configuration active;
    private GameObject templateRoot;
    private UIImage[] images;
    private IUIDrawTarget[] draws;
    private HolderBatchRenderer renderer;
    private Func<Material> createMaterial;
    private Action<Material, RenderBatch> bindTextures;
    private FrameSample[] samples;
    private FrameSample pending;
    private bool hasPending, savedSettings, scopedAllocationAvailable;
    private float oldTimeScale;
    private int oldVSync, oldTargetRate;
    private int mode, modeFrame, sampleCount;
    private long previousUpdate;
    private ProfilerRecorder mainThread, frameGc;

    private void Start() { if (autoRun) BeginRun(); }

    [ContextMenu("Run HUD RC suite")]
    public void BeginRun()
    {
        ResetRun();
        configuration.Validate();
        if (outputCamera == null || batchShader == null)
            throw new InvalidOperationException("Scene camera and batch shader must be bound.");
            active = configuration.Copy();
            BuildMeshDiagnostics.Enabled = true;
        State = RunState.Preparing;
        oldTimeScale = Time.timeScale;
        oldVSync = QualitySettings.vSyncCount;
        oldTargetRate = Application.targetFrameRate;
        savedSettings = true;
        Time.timeScale = 1f;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = active.targetFrameRate;
        try
        {
            // Unity 2022 Android IL2CPP can crash in the runtime implementation of this API.
            // Keep it for Mono/Editor diagnostics, but never call it from an IL2CPP Player.
#if ENABLE_IL2CPP
            scopedAllocationAvailable = false;
#else
            long calibrationStart = GC.GetAllocatedBytesForCurrentThread();
            var calibration = new byte[4096];
            scopedAllocationAvailable = GC.GetAllocatedBytesForCurrentThread() - calibrationStart >= 4096;
            GC.KeepAlive(calibration);
#endif
            samples = new FrameSample[3 * active.sampleFrames];
            PrepareHolders();
            createMaterial = () =>
            {
                var material = new Material(batchShader);
                material.SetTexture("_MainTex0", Texture2D.whiteTexture);
                material.renderQueue = 3000;
                return material;
            };
            bindTextures = (material, batch) => material.SetTexture("_MainTex0", Texture2D.whiteTexture);
            renderer = new HolderBatchRenderer(3, new PerfProbe(3 * (active.warmupFrames + active.sampleFrames)));
            Rebuild();
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            frameGc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            previousUpdate = Stopwatch.GetTimestamp();
            State = RunState.Warming;
        }
        catch
        {
            ResetRun();
            throw;
        }
    }

    private void PrepareHolders()
    {
        templateRoot = new GameObject("HUD RC generation template", typeof(RectTransform), typeof(Canvas), typeof(UIPrefabOwner));
        templateRoot.transform.SetParent(transform, false);
        // Explicit generation uses UIImage.DoGenerate. Disable automatic Canvas drawing/rebuilds.
        templateRoot.SetActive(false);
        var owner = templateRoot.GetComponent<UIPrefabOwner>();
        owner.targets = new List<Transform>();
        images = new UIImage[active.quadsPerHolder];
        draws = new IUIDrawTarget[images.Length];
        for (int i = 0; i < images.Length; i++)
        {
            var go = new GameObject("Bar " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(UIImage));
            go.transform.SetParent(templateRoot.transform, false);
            var image = go.GetComponent<UIImage>();
            image.rectTransform.sizeDelta = new Vector2(22, 3);
            image.rectTransform.anchoredPosition = new Vector2(0, i * 4);
            image.color = i % 2 == 0 ? Color.green : Color.cyan;
            image.raycastTarget = false;
            images[i] = image;
            draws[i] = image;
            owner.targets.Add(go.transform);
        }
        int columns = Mathf.CeilToInt(Mathf.Sqrt(active.holderCount));
        for (int i = 0; i < active.holderCount; i++)
        {
            IUIPrefabHolder holder = active.useSlim
                ? (IUIPrefabHolder)new DataPrefabHolder<UIMeshDataX>() : new DataPrefabHolder<UIMeshData>();
            holder.SetTarget(owner);
            // Direct public holder API avoids registering temporary owners in the global owner registry.
            holder.BuildMesh(draws);
            holders.Add(holder);
            positions.Add(holder, new Vector3((i % columns - columns / 2f) * 28, (i / columns - columns / 2f) * 18, 0));
        }
        GeneratedHolderCount = holders.Count;
    }

    private void Rebuild()
    {
        renderer.Rebuild(holders, positions, createMaterial, bindTextures);
        VertexCount = 0;
        IndexCount = 0;
        for (int i = 0; i < renderer.BatchCount; i++)
        {
            var mesh = renderer.GetBatchMesh(i);
            VertexCount += mesh.vertexCount;
            IndexCount += (int)mesh.GetIndexCount(0);
        }
        if (VertexCount == 0 || IndexCount == 0)
            throw new InvalidOperationException("The workload generated no geometry.");
    }

    private void Update()
    {
        if (State != RunState.Warming && State != RunState.Sampling) return;
        try { AdvanceFrame(); }
        catch (Exception error)
        {
            Failure = error.ToString();
            ReleaseResources();
            State = RunState.Failed;
            Debug.LogException(error);
        }
    }

    private void AdvanceFrame()
    {
        long advanceStart = Stopwatch.GetTimestamp();
        long now = Stopwatch.GetTimestamp();
        // Recorder.LastValue describes the completed previous Unity frame; align with its pending workload.
        using (RunnerBookkeepingMarker.Auto())
        {
            if (hasPending)
            {
                pending.frameIntervalMs = Milliseconds(now - previousUpdate);
                pending.mainThreadAvailable = mainThread.Valid && mainThread.Count > 0 && mainThread.LastValue > 0;
                pending.mainThreadMs = pending.mainThreadAvailable ? mainThread.LastValue / 1000000.0 : -1;
                pending.frameGcAvailable = frameGc.Valid && frameGc.Count > 0;
                pending.frameGcBytes = pending.frameGcAvailable ? frameGc.LastValue : -1;
                samples[sampleCount++] = pending;
                hasPending = false;
            }
            previousUpdate = now;
            if (modeFrame == active.warmupFrames + active.sampleFrames)
            {
                mode++;
                modeFrame = 0;
                if (mode == 3)
                {
                    State = RunState.Completed;
                    ReleaseResources();
                    if (autoExport) ExportReport();
                    return;
                }
            }
        }
        long bookkeepingEnd = Stopwatch.GetTimestamp();

        bool sampling = modeFrame >= active.warmupFrames;
        State = sampling ? RunState.Sampling : RunState.Warming;
        int frame = sampling ? modeFrame - active.warmupFrames : modeFrame;
        // Warm every holder/mesh before each measurement mode, including the static baseline.
        int count = sampling ? PlannedUpdates((Workload)mode, frame, active) : active.holderCount;
        int start = (int)(((long)frame * Math.Max(1, count) + (uint)active.seed) % active.holderCount);
        long bytes = scopedAllocationAvailable ? GC.GetAllocatedBytesForCurrentThread() : 0;
        long selectionStart = Stopwatch.GetTimestamp();
        int checksum = 0;
        long selectionTicks = 0;
        long buildTicks = 0;
        double buildMeshSetupMs = 0;
        double buildMeshDataInitMs = 0;
        double buildMeshDispatchMs = 0;
        double buildMeshDoGenerateMs = 0;
        double buildMeshSimplePathMs = 0;
        double buildMeshUguiFallbackMs = 0;
        double buildMeshTransformMs = 0;
        double buildMeshSpriteDataMs = 0;
        double buildMeshDrawingDimensionsMs = 0;
        double buildMeshMeshWriteMs = 0;
        double buildMeshWriteStorageMs = 0;
        double buildMeshWriteVertexMs = 0;
        double buildMeshWriteUvMs = 0;
        double buildMeshWriteColorMs = 0;
        double buildMeshWriteIndexMs = 0;
        using (HolderSelectionMarker.Auto())
        {
            for (int j = 0; j < count; j++)
            {
                long selectionItemStart = Stopwatch.GetTimestamp();
                int index = (start + j) % active.holderCount;
                float width = 12 + ((index + frame + (uint)active.seed) % 11);
                for (int k = 0; k < images.Length; k++)
                    images[k].rectTransform.sizeDelta = new Vector2(width, 3);
                selectionTicks += Stopwatch.GetTimestamp() - selectionItemStart;
                long buildStart = Stopwatch.GetTimestamp();
                using (BuildMeshMarker.Auto())
                    holders[index].BuildMesh(draws);
                if (holders[index] is IBuildMeshStageSource stageSource)
                {
                    var stage = stageSource.LastBuildMeshTimings;
                    buildMeshSetupMs += stage.SetupMs;
                    buildMeshDataInitMs += stage.MeshDataInitMs;
                    buildMeshDispatchMs += stage.DrawDispatchMs;
                    buildMeshDoGenerateMs += stage.DoGenerateMs;
                    buildMeshSimplePathMs += stage.SimplePathMs;
                    buildMeshUguiFallbackMs += stage.UguiFallbackMs;
                    buildMeshTransformMs += stage.TransformMs;
                    buildMeshSpriteDataMs += stage.SimpleSpriteDataMs;
                    buildMeshDrawingDimensionsMs += stage.SimpleDrawingDimensionsMs;
                    buildMeshMeshWriteMs += stage.SimpleMeshWriteMs;
                    buildMeshWriteStorageMs += stage.SimpleQuadWrite.StorageMs;
                    buildMeshWriteVertexMs += stage.SimpleQuadWrite.VertexMs;
                    buildMeshWriteUvMs += stage.SimpleQuadWrite.UvMs;
                    buildMeshWriteColorMs += stage.SimpleQuadWrite.ColorMs;
                    buildMeshWriteIndexMs += stage.SimpleQuadWrite.IndexMs;
                }
                buildTicks += Stopwatch.GetTimestamp() - buildStart;
                checksum = unchecked(checksum * 31 + index + 1);
            }
        }
        long selectionEnd = Stopwatch.GetTimestamp();
        long afterGeneration = Stopwatch.GetTimestamp();
        long generationBytes = scopedAllocationAvailable ? GC.GetAllocatedBytesForCurrentThread() - bytes : -1;
        long mergeStartBytes = scopedAllocationAvailable ? GC.GetAllocatedBytesForCurrentThread() : 0;
        if (count > 0)
        {
            using (RebuildMarker.Auto())
                Rebuild();
        }
        long afterMerge = Stopwatch.GetTimestamp();
        long mergeBytes = scopedAllocationAvailable ? GC.GetAllocatedBytesForCurrentThread() - mergeStartBytes : -1;
        long drawStart = Stopwatch.GetTimestamp();
        using (DrawMarker.Auto())
            renderer.Draw(Matrix4x4.identity, gameObject.layer, outputCamera);
        long afterDraw = Stopwatch.GetTimestamp();
        long measuredBytes = scopedAllocationAvailable ? GC.GetAllocatedBytesForCurrentThread() - bytes : -1;
        if (sampling)
        {
            pending = new FrameSample
            {
                unityFrame = Time.frameCount, sampleFrame = frame, mode = mode, updatedHolders = count,
                vertices = VertexCount, indices = IndexCount, batches = renderer.BatchCount,
                generationMs = Milliseconds(afterGeneration - selectionStart), mergeMs = Milliseconds(afterMerge - afterGeneration),
                runnerBookkeepingMs = Milliseconds(bookkeepingEnd - advanceStart),
                holderSelectionMs = Milliseconds(selectionTicks),
                buildMeshMs = Milliseconds(buildTicks),
                buildMeshSetupMs = buildMeshSetupMs, buildMeshDataInitMs = buildMeshDataInitMs,
                buildMeshDispatchMs = buildMeshDispatchMs, buildMeshDoGenerateMs = buildMeshDoGenerateMs,
                buildMeshSimplePathMs = buildMeshSimplePathMs, buildMeshUguiFallbackMs = buildMeshUguiFallbackMs,
                buildMeshTransformMs = buildMeshTransformMs,
                buildMeshSpriteDataMs = buildMeshSpriteDataMs, buildMeshDrawingDimensionsMs = buildMeshDrawingDimensionsMs,
                buildMeshMeshWriteMs = buildMeshMeshWriteMs,
                buildMeshWriteStorageMs = buildMeshWriteStorageMs, buildMeshWriteVertexMs = buildMeshWriteVertexMs,
                buildMeshWriteUvMs = buildMeshWriteUvMs, buildMeshWriteColorMs = buildMeshWriteColorMs,
                buildMeshWriteIndexMs = buildMeshWriteIndexMs,
                mergePlanMs = renderer.LastPlanMs, mergeFillMs = renderer.LastFillMs, meshUploadMs = renderer.LastMeshUploadMs,
                drawSubmitMs = Milliseconds(afterDraw - drawStart), generationBytes = scopedAllocationAvailable ? generationBytes : -1,
                mergeBytes = scopedAllocationAvailable ? mergeBytes : -1, measuredThreadBytes = scopedAllocationAvailable ? measuredBytes : -1,
                scopedAllocationAvailable = scopedAllocationAvailable, selectionChecksum = checksum
            };
            hasPending = true;
        }
        modeFrame++;
    }

    public static int PlannedUpdates(Workload workload, int frame, Configuration config)
    {
        if (workload == Workload.Static) return 0;
        if (workload == Workload.Burst) return frame % config.burstEveryFrames == 0 ? config.holderCount : 0;
        return Math.Max(1, (config.holderCount * config.partialPercent + 99) / 100);
    }

    private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    public Report CreateReport()
    {
        if (State != RunState.Completed) throw new InvalidOperationException("Complete the measurement before exporting.");
        var report = new Report
        {
            utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
            os = SystemInfo.operatingSystem, cpu = SystemInfo.processorType, graphics = SystemInfo.graphicsDeviceType.ToString(),
            executionMode = Application.isEditor ? "Editor PlayMode" : "Player",
#if ENABLE_IL2CPP
            scriptingBackend = "IL2CPP",
#else
            scriptingBackend = "Mono",
#endif
            commit = sourceCommit, sourceStatus = sourceStatus, sourceFingerprint = sourceFingerprint, packageVersion = packageVersion,
            configuration = active.Copy(), frames = (FrameSample[])samples.Clone(),
            measurementNotes = "generation=UIImage.DoGenerate + UIMeshData/ X.FillVertex + transforms; not extraction-only. " +
                "merge=production HolderBatchRenderer.Rebuild including its allocations. measuredThreadBytes includes generation, merge, draw submission. " +
                "frame interval=Update-to-Update wall time including editor/pacing; Main Thread and frame GC are previous-frame profiler counters aligned by Unity frame. " +
                "Scoped allocation API is calibrated with a known allocation before measurement; failure marks scoped bytes unavailable. Unavailable counters use flags and -1, CSV uses unavailable. No whole-application zero-GC claim."
        };
        var summaries = new List<Summary>();
        for (int m = 0; m < 3; m++)
        {
            summaries.Add(Summarize(m, false));
            if (m == (int)Workload.Burst) summaries.Add(Summarize(m, true));
        }
        report.summaries = summaries.ToArray();
        return report;
    }

    private Summary Summarize(int workload, bool refreshOnly)
    {
        var regeneration = new List<double>();
        var generation = new List<double>();
        var merge = new List<double>();
        var intervals = new List<double>();
        var main = new List<double>();
        var summary = new Summary { workload = ((Workload)workload).ToString(), subset = refreshOnly ? "refresh-frames" : "all-frames", generationBytesMax = -1, mergeBytesMax = -1, measuredThreadBytesMax = -1 };
        for (int i = 0; i < sampleCount; i++)
        {
            var s = samples[i];
            if (s.mode != workload || (refreshOnly && s.updatedHolders == 0)) continue;
            regeneration.Add(s.generationMs + s.mergeMs);
            generation.Add(s.generationMs);
            merge.Add(s.mergeMs);
            intervals.Add(s.frameIntervalMs);
            if (s.mainThreadAvailable) main.Add(s.mainThreadMs);
            if (s.frameGcAvailable) summary.frameGcSamples++;
            summary.generationBytesMax = Math.Max(summary.generationBytesMax, s.generationBytes);
            summary.mergeBytesMax = Math.Max(summary.mergeBytesMax, s.mergeBytes);
            summary.measuredThreadBytesMax = Math.Max(summary.measuredThreadBytesMax, s.measuredThreadBytes);
        }
        summary.samples = regeneration.Count;
        summary.mainThreadSamples = main.Count;
        summary.regenerationMedianMs = Percentile(regeneration, .5);
        summary.regenerationP95Ms = Percentile(regeneration, .95);
        summary.generationP95Ms = Percentile(generation, .95);
        summary.mergeP95Ms = Percentile(merge, .95);
        summary.frameIntervalMedianMs = Percentile(intervals, .5);
        summary.frameIntervalP95Ms = Percentile(intervals, .95);
        summary.mainThreadMedianMs = Percentile(main, .5);
        summary.mainThreadP95Ms = Percentile(main, .95);
        return summary;
    }

    private static double Percentile(List<double> values, double fraction)
    {
        if (values.Count == 0) return -1;
        values.Sort();
        return values[Math.Max(0, (int)Math.Ceiling(values.Count * fraction) - 1)];
    }

    [ContextMenu("Export completed HUD RC report")]
    public void ExportReport()
    {
        var report = CreateReport();
        string directory = PerfProbe.GetOutputDirectory();
        Directory.CreateDirectory(directory);
        string stem = "hud_rc_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string jsonPath = Path.Combine(directory, stem + ".json");
        string csvPath = Path.Combine(directory, stem + ".csv");
        File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        using (var writer = new StreamWriter(csvPath, false, new UTF8Encoding(false)))
        {
            writer.WriteLine("unity_frame,sample_frame,mode,updated_holders,vertices,indices,batches,generation_ms,merge_ms,draw_submit_ms,frame_interval_ms,main_thread_ms,build_mesh_setup_ms,build_mesh_data_init_ms,build_mesh_dispatch_ms,build_mesh_do_generate_ms,build_mesh_simple_path_ms,build_mesh_sprite_data_ms,build_mesh_drawing_dimensions_ms,build_mesh_mesh_write_ms,build_mesh_write_storage_ms,build_mesh_write_vertex_ms,build_mesh_write_uv_ms,build_mesh_write_color_ms,build_mesh_write_index_ms,build_mesh_ugui_fallback_ms,build_mesh_transform_ms,generation_bytes,merge_bytes,measured_thread_bytes,frame_gc_bytes,selection_checksum");
            foreach (var s in report.frames)
                writer.WriteLine(string.Join(",", s.unityFrame, s.sampleFrame, (Workload)s.mode, s.updatedHolders, s.vertices, s.indices, s.batches,
                    F(s.generationMs), F(s.mergeMs), F(s.drawSubmitMs), F(s.frameIntervalMs), s.mainThreadAvailable ? F(s.mainThreadMs) : "unavailable",
                    F(s.buildMeshSetupMs), F(s.buildMeshDataInitMs), F(s.buildMeshDispatchMs), F(s.buildMeshDoGenerateMs), F(s.buildMeshSimplePathMs), F(s.buildMeshSpriteDataMs), F(s.buildMeshDrawingDimensionsMs), F(s.buildMeshMeshWriteMs), F(s.buildMeshWriteStorageMs), F(s.buildMeshWriteVertexMs), F(s.buildMeshWriteUvMs), F(s.buildMeshWriteColorMs), F(s.buildMeshWriteIndexMs), F(s.buildMeshUguiFallbackMs), F(s.buildMeshTransformMs),
                    s.scopedAllocationAvailable ? s.generationBytes.ToString() : "unavailable", s.scopedAllocationAvailable ? s.mergeBytes.ToString() : "unavailable", s.scopedAllocationAvailable ? s.measuredThreadBytes.ToString() : "unavailable", s.frameGcAvailable ? s.frameGcBytes.ToString(CultureInfo.InvariantCulture) : "unavailable", s.selectionChecksum));
        }
        LastReportPath = jsonPath;
    }

    private static string F(double number) => number.ToString("R", CultureInfo.InvariantCulture);

    [ContextMenu("Reset HUD RC suite")]
    public void ResetRun()
    {
        BuildMeshDiagnostics.Enabled = false;
        ReleaseResources();
        samples = null;
        active = null;
        mode = modeFrame = sampleCount = 0;
        hasPending = false;
        GeneratedHolderCount = VertexCount = IndexCount = 0;
        LastReportPath = Failure = null;
        State = RunState.Idle;
    }

    private void ReleaseResources()
    {
        mainThread.Dispose();
        frameGc.Dispose();
        renderer?.Dispose();
        renderer = null;
        foreach (var holder in holders)
            foreach (var mesh in holder.UIMeshDatas) mesh?.Dispose();
        holders.Clear();
        positions.Clear();
        if (templateRoot != null)
        {
            if (Application.isPlaying) Destroy(templateRoot); else DestroyImmediate(templateRoot);
            templateRoot = null;
        }
        images = null;
        draws = null;
        if (savedSettings)
        {
            Time.timeScale = oldTimeScale;
            QualitySettings.vSyncCount = oldVSync;
            Application.targetFrameRate = oldTargetRate;
            savedSettings = false;
        }
    }

    private void OnDisable()
    {
        if (State == RunState.Warming || State == RunState.Sampling || State == RunState.Preparing) ResetRun();
    }
    private void OnDestroy() { ReleaseResources(); }
}
