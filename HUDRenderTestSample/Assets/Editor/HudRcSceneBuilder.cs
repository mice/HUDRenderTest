using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class HudRcSceneBuilder
{
    [Serializable]
    private sealed class QueryProbeResult
    {
        public string unityVersion;
        public int iterations;
        public double pixelAdjustedRectMs, rectMs, relativeMatrixMs, timestampMs;
        public double generateDiagnosticsOnMs, generateDiagnosticsOffMs;
        public double sink;
    }

    // Local hotspot probe, deliberately separate from acceptance tests and device budgets.
    public static void MeasureGenerationQueriesBatch()
    {
        var root = new GameObject("Query probe", typeof(RectTransform), typeof(Canvas));
        root.SetActive(false);
        var child = new GameObject("Image", typeof(RectTransform));
        child.transform.SetParent(root.transform, false);
        var image = child.AddComponent<UIImage>();
        image.rectTransform.sizeDelta = new Vector2(22, 3);
        var data = new UIMeshDataX();
        bool old = BuildMeshDiagnostics.Enabled;
        bool oldQuad = BuildMeshDiagnostics.ProfileSimpleQuadWrites;
        const int count = 38400;
        var result = new QueryProbeResult { unityVersion = Application.unityVersion, iterations = count };
        try
        {
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = false;
            for (int i = 0; i < 1000; i++) image.DoGenerate(data, root.transform);
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++) result.sink += image.GetPixelAdjustedRect().width;
            result.pixelAdjustedRectMs = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++) result.sink += image.rectTransform.rect.width;
            result.rectMs = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            var rootTransform = root.transform;
            var childTransform = child.transform;
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++) result.sink += Utility.CalcMatrix(rootTransform, childTransform).m00;
            result.relativeMatrixMs = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++) result.sink += System.Diagnostics.Stopwatch.GetTimestamp() & 1;
            result.timestampMs = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            BuildMeshDiagnostics.Enabled = true;
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++) image.DoGenerate(data, rootTransform);
            result.generateDiagnosticsOnMs = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            BuildMeshDiagnostics.Enabled = false;
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++) image.DoGenerate(data, rootTransform);
            result.generateDiagnosticsOffMs = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - start);
            File.WriteAllText(ReadString("-hudQueryProbeOutput", "../Temp/generation-query-probe.json"), JsonUtility.ToJson(result, true));
        }
        finally
        {
            BuildMeshDiagnostics.Enabled = old;
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = oldQuad;
            data.Dispose();
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    public const string ScenePath = "Assets/Scenes/Scene_Perf_HUD_RC.unity";
    private const string BatchKey = "HudRc.Batch";
    private const string StartedKey = "HudRc.Started";
    private const string TimeKey = "HudRc.StartTime";

    [MenuItem("Tools/HUD RC/Create dedicated performance scene")]
    public static void BuildScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("HUD RC Camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 340;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .04f, .055f);
        var root = new GameObject("HUD RC Performance Suite", typeof(HudRcPerformanceRunner));
        var runner = root.GetComponent<HudRcPerformanceRunner>();
        runner.outputCamera = camera;
        runner.batchShader = Shader.Find("Hidden/UIE-AtlasBlit");
        if (runner.batchShader == null) throw new InvalidOperationException("Required package shader is unavailable.");
        SetSourceMetadata(runner);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save dedicated scene.");
        AssetDatabase.SaveAssets();
        Debug.Log("Created " + ScenePath);
    }

    public static void BuildBatch()
    {
        try { BuildScene(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void BuildAndroidBatch()
    {
        string temporaryScenePath = null;
        var androidGroup = BuildPipeline.GetBuildTargetGroup(BuildTarget.Android);
        var previousBackend = PlayerSettings.GetScriptingBackend(androidGroup);
        var previousArchitectures = PlayerSettings.Android.targetArchitectures;
        int exitCode = 1;
        try
        {
            var output = ReadString("-hudRcAndroidOutput", "Builds/HudRc/Xiaomi10/HUDRenderTest.apk");
            var absolute = Path.GetFullPath(output);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var runner = UnityEngine.Object.FindObjectOfType<HudRcPerformanceRunner>();
            if (runner == null) throw new InvalidOperationException("Committed scene has no HUD RC runner.");
            runner.configuration.useSlim = ReadBool("-hudRcSlim", false);
            runner.configuration.profileQuadWrites = ReadBool("-hudRcProfileQuadWrites", false);
            runner.configuration.disableBuildMeshDiagnostics = ReadBool("-hudRcDisableBuildMeshDiagnostics", false);
            runner.configuration.useGenerationBatch = ReadBool("-hudRcGenerationBatch", false);
            runner.configuration.spriteBacked = ReadBool("-hudRcSpriteBacked", false);
            runner.configuration.forceUguiFallback = ReadBool("-hudRcForceUguiFallback", false);
            runner.configuration.holderCount = ReadInt("-hudRcCount", runner.configuration.holderCount);
            runner.configuration.sampleFrames = ReadInt("-hudRcSamples", runner.configuration.sampleFrames);
            runner.configuration.warmupFrames = ReadInt("-hudRcWarmup", runner.configuration.warmupFrames);
            runner.configuration.targetFrameRate = ReadInt("-hudRcFrameRate", runner.configuration.targetFrameRate);
            runner.configuration.Validate();
            SetSourceMetadata(runner);
            temporaryScenePath = "Assets/HudRcBuild_" + Guid.NewGuid().ToString("N") + ".unity";
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), temporaryScenePath, true))
                throw new IOException("Could not save the temporary HUD RC build scene.");
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            var buildOptions = ReadBool("-hudRcDevelopment", false)
                ? BuildOptions.Development | BuildOptions.ConnectWithProfiler
                : BuildOptions.None;
            PlayerSettings.SetScriptingBackend(androidGroup, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            var settings = new BuildPlayerOptions
            {
                scenes = new[] { temporaryScenePath },
                locationPathName = absolute,
                target = BuildTarget.Android,
                options = buildOptions
            };
            var report = BuildPipeline.BuildPlayer(settings);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android HUD RC build failed: {report.summary.result}");
            Debug.Log("HUD_RC_ANDROID_APK=" + absolute);
            exitCode = 0;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            PlayerSettings.SetScriptingBackend(androidGroup, previousBackend);
            PlayerSettings.Android.targetArchitectures = previousArchitectures;
            if (temporaryScenePath != null) AssetDatabase.DeleteAsset(temporaryScenePath);
        }
        EditorApplication.Exit(exitCode);
    }

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var runner = UnityEngine.Object.FindObjectOfType<HudRcPerformanceRunner>();
        runner.autoRun = false;
        SessionState.SetBool(BatchKey, true);
        SessionState.SetBool(StartedKey, false);
        SessionState.SetFloat(TimeKey, (float)EditorApplication.timeSinceStartup);
        InstallBatchPump();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void InstallBatchPump()
    {
        EditorApplication.update -= Pump;
        if (SessionState.GetBool(BatchKey, false)) EditorApplication.update += Pump;
    }

    private static void Pump()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(TimeKey, 0) > 180)
                throw new TimeoutException("HUD RC measurement exceeded its watchdog.");
            if (!EditorApplication.isPlaying) return;
            var runner = UnityEngine.Object.FindObjectOfType<HudRcPerformanceRunner>();
            if (runner == null) throw new InvalidOperationException("Committed scene has no runner.");
            if (!SessionState.GetBool(StartedKey, false))
            {
                runner.configuration.holderCount = ReadInt("-hudRcCount", 256);
                runner.configuration.sampleFrames = ReadInt("-hudRcSamples", 180);
                runner.configuration.warmupFrames = ReadInt("-hudRcWarmup", 60);
                runner.configuration.useSlim = Array.IndexOf(Environment.GetCommandLineArgs(), "-hudRcSlim") >= 0;
                SetSourceMetadata(runner);
                runner.autoExport = true;
                runner.BeginRun();
                SessionState.SetBool(StartedKey, true);
            }
            if (runner.State == HudRcPerformanceRunner.RunState.Failed)
                throw new InvalidOperationException(runner.Failure);
            if (runner.State != HudRcPerformanceRunner.RunState.Completed) return;
            Debug.Log("HUD_RC_REPORT=" + runner.LastReportPath);
            SessionState.SetBool(BatchKey, false);
            EditorApplication.update -= Pump;
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            SessionState.SetBool(BatchKey, false);
            EditorApplication.update -= Pump;
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static int ReadInt(string key, int fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1]) : fallback;
    }

    private static string ReadString(string key, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, key);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    private static bool ReadBool(string key, bool fallback)
    {
        var args = Environment.GetCommandLineArgs();
        return Array.IndexOf(args, key) >= 0 ? true : fallback;
    }

    private static void SetSourceMetadata(HudRcPerformanceRunner runner)
    {
        runner.sourceCommit = Environment.GetEnvironmentVariable("HUD_RC_SOURCE_COMMIT") ?? "unknown";
        runner.sourceStatus = Environment.GetEnvironmentVariable("HUD_RC_SOURCE_STATUS") ?? "unknown";
        runner.sourceFingerprint = Environment.GetEnvironmentVariable("HUD_RC_SOURCE_FINGERPRINT") ?? "unknown";
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UIMeshData).Assembly);
        runner.packageVersion = package != null ? package.version : "unknown";
    }
}
