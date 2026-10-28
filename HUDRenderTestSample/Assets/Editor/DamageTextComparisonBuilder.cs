using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Creates new comparison-owned assets only; never overwrites the playback Sample or user scenes.</summary>
public static class DamageTextComparisonBuilder
{
    public const string ScenePath = "Assets/Scenes/Scene_DamageTextComparison.unity";
    public const string ResourcePath = "Assets/DamageTextComparison";
    [MenuItem("Tools/HUD Damage Text/Create renderer comparison Sample")]
    public static void CreateSample()
    {
        if (File.Exists(ScenePath) || Directory.Exists(ResourcePath)) throw new InvalidOperationException("Comparison assets already exist; open the existing scene.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var atlas = AssetDatabase.LoadAssetAtPath<DamageTextGlyphAtlas>("Assets/DamageText/Digits.asset");
        var styles = AssetDatabase.LoadAssetAtPath<DamageTextStyles>("Assets/DamageText/Styles.asset");
        if (atlas == null || styles == null) throw new InvalidOperationException("Create the baseline playback Sample first.");
        Directory.CreateDirectory(ResourcePath); AssetDatabase.Refresh();
        DamageTextClipAsset first = null;
        foreach (int digits in new[] { 2, 5, 8 })
        {
            var clip = Save("Static128_" + digits, Events(128, 1, digits, false), 2);
            if (first == null) first = clip;
        }
        Save("Static128_8_HighOverlap", Events(128, 1, 8, true), 2);
        foreach (int count in new[] { 127, 128, 129, 256 }) Save("Bursts" + count + "_100", Events(count, 100, 8, false), 150);
        var continuous = new DamageTextRequest[128 * 60];
        for (int i = 0; i < continuous.Length; i++)
        {
            continuous[i] = Make((uint)i + 1, i % 3 == 0 ? 42u : i % 3 == 1 ? 12345u : 12345678u, i / 128f, i, false);
        }
        Save("Continuous60s", continuous, 61.2f);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Damage comparison camera", typeof(Camera)).GetComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 6.5f; camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .04f, .065f);
        var runner = new GameObject("Damage text benchmark", typeof(DamageTextBenchmarkRunner)).GetComponent<DamageTextBenchmarkRunner>();
        runner.outputCamera = camera; runner.atlas = atlas; runner.styles = styles; runner.clip = first;
        runner.meshShader = Shader.Find("UIData/DamageText"); runner.instanceShader = Shader.Find("UIData/DamageTextInstanced");
        if (runner.meshShader == null || runner.instanceShader == null) throw new InvalidOperationException("Import comparison Shaders first.");
        runner.plan.Phase = DamageTextBenchmarkPhase.DrawOnly; runner.showControls = true; runner.autoRun = false;
        runner.RefreshSourceMetadata();
        AssetDatabase.SaveAssets(); if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save comparison scene.");
        AssetDatabase.Refresh();
    }
    private static DamageTextRequest[] Events(int count, int windows, int digits, bool overlap)
    {
        var events = new DamageTextRequest[count * windows]; uint amount = digits == 2 ? 42u : digits == 5 ? 12345u : 12345678u;
        for (int window = 0; window < windows; window++) for (int i = 0; i < count; i++)
            events[window * count + i] = Make((uint)(window * count + i + 1), amount, window * 1.5f, i, overlap, (count + 15) / 16);
        return events;
    }
    private static DamageTextRequest Make(uint id, uint amount, float time, int index, bool overlap, int rows = 8) => new DamageTextRequest
    {
        EventId = id, Amount = amount, SpawnTime = time, Kind = (DamageTextKind)(index % 3), RandomSeed = id * 71 + 3,
        WorldPosition = overlap ? Vector3.zero : new Vector3((index % 16 - 7.5f) * 1.05f, (index / 16 % rows - (rows - 1) * .5f) * .65f - .8f, 0)
    };
    private static DamageTextClipAsset Save(string name, DamageTextRequest[] events, float duration)
    {
        var clip = ScriptableObject.CreateInstance<DamageTextClipAsset>(); clip.Events = events; clip.Duration = duration; clip.Version = "comparison-clips-v1";
        clip.CameraKeys = new[] { new DamageTextCameraKey { Time = 0, Position = new Vector3(0, 0, -10), Rotation = Quaternion.identity,
            FieldOfView = 60, OrthographicSize = 6.5f } };
        clip.Freeze(); AssetDatabase.CreateAsset(clip, ResourcePath + "/" + name + ".asset"); return clip;
    }
    public static void CreateBatch()
    {
        try { CreateSample(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [Serializable] private sealed class AndroidBuildMetadata
    {
        public string schema = "damage-comparison-android-build-v1";
        public string commit, sourceFingerprint, buildGuid, backend = "IL2CPP", api = "Vulkan", architecture = "ARM64", buildType = "Release";
        public string deviceValidation = "deferred; APK not installed or run";
        public string applicationIdentifier;
        public bool frameTimingProbe, precisionProbe, frameTimingStatsEnabled, probeInBenchmarkPackage;
        public bool nativeVulkanProbe;
        public bool nativeGpuTiming;
        public string nativePluginSHA256;
        public int embeddedClips, meshVulkanCompilerEntries, instancedVulkanCompilerEntries;
    }
    /// <summary>Run only in an isolated clean checkout; settings and the committed scene are restored.</summary>
    public static void BuildAndroidBatch()
    {
        string temporaryScene = null;
        string temporaryPlugin = null;
        byte[] playerSettings = File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
        byte[] graphicsSettings = File.ReadAllBytes("ProjectSettings/GraphicsSettings.asset");
        var group = BuildTargetGroup.Android;
        var previousBackend = PlayerSettings.GetScriptingBackend(group);
        var previousArchitectures = PlayerSettings.Android.targetArchitectures;
        bool previousFrameStats = PlayerSettings.enableFrameTimingStats;
        string previousIdentifier = PlayerSettings.GetApplicationIdentifier(group);
        var previousApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        bool previousDefaultApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
        bool previousBundle = EditorUserBuildSettings.buildAppBundle;
        var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var stripping = graphics.FindProperty("m_InstancingStripping");
        int previousStripping = stripping.intValue;
        int exitCode = 1;
        try
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use the isolated batch build command.");
            string[] arguments = Environment.GetCommandLineArgs(); string output = null;
            bool timingProbe = Array.IndexOf(arguments, "-damageComparisonTimingProbe") >= 0;
            bool precisionProbe = Array.IndexOf(arguments, "-damageComparisonPrecisionProbe") >= 0;
            bool probeInBenchmarkPackage = Array.IndexOf(arguments, "-damageComparisonProbeInBenchmarkPackage") >= 0;
            bool nativeVulkanProbe = Array.IndexOf(arguments, "-damageComparisonNativeVulkanProbe") >= 0;
            bool nativeGpuTiming = Array.IndexOf(arguments, "-damageComparisonNativeGpuTiming") >= 0;
            if (nativeGpuTiming && (timingProbe || precisionProbe)) throw new ArgumentException("Choose formal native capture or a diagnostic probe.");
            string nativeLibrary = null, nativeHash = null;
            for (int i = 0; i + 1 < arguments.Length; i++) if (arguments[i] == "-damageComparisonNativeLibrary") nativeLibrary = arguments[i + 1];
            if (nativeVulkanProbe && !timingProbe) throw new ArgumentException("Native Vulkan calibration requires the timing probe.");
            if (timingProbe && precisionProbe) throw new ArgumentException("Choose one diagnostic probe per build.");
            if (probeInBenchmarkPackage && !timingProbe && !precisionProbe) throw new ArgumentException("Package reuse requires a diagnostic probe.");
            for (int i = 0; i + 1 < arguments.Length; i++) if (arguments[i] == "-damageComparisonOutput") output = arguments[i + 1];
            if (string.IsNullOrWhiteSpace(output)) throw new ArgumentException("Specify -damageComparisonOutput.");
            output = Path.GetFullPath(output);
            if (File.Exists(output)) throw new IOException("Refusing to overwrite an existing capture APK.");
            if (nativeVulkanProbe || nativeGpuTiming)
            {
                if (string.IsNullOrEmpty(nativeLibrary) || !File.Exists(nativeLibrary)) throw new IOException("Build the registered native timestamp library first.");
                temporaryPlugin = "Assets/libDamageTextGpuTiming.so";
                if (File.Exists(temporaryPlugin) || File.Exists(temporaryPlugin + ".meta"))
                { temporaryPlugin = null; throw new IOException("Refusing to replace an existing native plugin asset."); }
                File.Copy(nativeLibrary, temporaryPlugin);
                AssetDatabase.ImportAsset(temporaryPlugin, ImportAssetOptions.ForceSynchronousImport);
                var importer = (PluginImporter)AssetImporter.GetAtPath(temporaryPlugin);
                importer.SetCompatibleWithAnyPlatform(false); importer.SetCompatibleWithEditor(false);
                importer.SetCompatibleWithPlatform(BuildTarget.Android, true);
                importer.SetPlatformData(BuildTarget.Android, "CPU", "ARM64"); importer.SaveAndReimport();
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    nativeHash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(temporaryPlugin))).Replace("-", "");
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var runner = UnityEngine.Object.FindObjectOfType<DamageTextBenchmarkRunner>();
            if (runner == null) throw new InvalidOperationException("Comparison runner is missing.");
            string[] clipGuids = AssetDatabase.FindAssets("t:DamageTextClipAsset", new[] { ResourcePath });
            Array.Sort(clipGuids, StringComparer.Ordinal);
            runner.captureClips = new DamageTextClipAsset[clipGuids.Length];
            for (int i = 0; i < clipGuids.Length; i++)
                runner.captureClips[i] = AssetDatabase.LoadAssetAtPath<DamageTextClipAsset>(AssetDatabase.GUIDToAssetPath(clipGuids[i]));
            if (runner.captureClips.Length != 9) throw new InvalidOperationException("Expected all nine comparison Clips.");
            runner.RefreshSourceMetadata();
            string commit = Environment.GetEnvironmentVariable("DAMAGE_COMPARISON_SOURCE_COMMIT");
            if (string.IsNullOrWhiteSpace(commit)) throw new InvalidOperationException("Build wrapper source registration is required.");
            runner.sourceCommit = commit; runner.sourceStatus = "clean checkout; see build source manifest and declared overrides";
            runner.readCaptureRequestOnStart = runner.autoRun = runner.autoExport = true;
            runner.showControls = runner.sideBySidePreview = false;
            runner.nativeGpuTimingSupported = nativeGpuTiming; runner.nativeGpuPluginSHA256 = nativeHash;
            if (timingProbe)
            {
                var probe = runner.gameObject.AddComponent<DamageTextFrameTimingProbe>();
                probe.runner = runner; probe.nativeVulkanTimestamps = nativeVulkanProbe; probe.nativePluginSHA256 = nativeHash;
                PlayerSettings.enableFrameTimingStats = true;
                if (!probeInBenchmarkPackage) PlayerSettings.SetApplicationIdentifier(group, "com.uidatarender.damageprobe");
            }
            if (precisionProbe)
            {
                runner.gameObject.AddComponent<DamageTextDevicePrecisionProbe>().runner = runner;
                if (!probeInBenchmarkPackage) PlayerSettings.SetApplicationIdentifier(group, "com.uidatarender.damageprobe");
            }
            temporaryScene = "Assets/DamageComparisonBuild_" + Guid.NewGuid().ToString("N") + ".unity";
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), temporaryScene, true)) throw new IOException("Could not save the build scene.");
            PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            EditorUserBuildSettings.buildAppBundle = false;
            // Runtime-created instancing materials are absent from Unity's scene usage scan.
            stripping.intValue = 2; graphics.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            DamageTextComparisonShaderAudit.Begin();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { temporaryScene }, locationPathName = output, target = BuildTarget.Android,
                options = BuildOptions.CleanBuildCache
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Damage comparison Android build: " + report.summary.result);
            if (DamageTextComparisonShaderAudit.MeshEntries == 0 || DamageTextComparisonShaderAudit.InstancedEntries == 0)
                throw new BuildFailedException("Vulkan mesh/INSTANCING_ON variants were not observed in the shader compiler input.");
            File.WriteAllText(output + ".build.json", JsonUtility.ToJson(new AndroidBuildMetadata
            {
                commit = commit, sourceFingerprint = runner.sourceFingerprint, buildGuid = report.summary.guid.ToString(),
                embeddedClips = runner.captureClips.Length, meshVulkanCompilerEntries = DamageTextComparisonShaderAudit.MeshEntries,
                instancedVulkanCompilerEntries = DamageTextComparisonShaderAudit.InstancedEntries,
                applicationIdentifier = PlayerSettings.GetApplicationIdentifier(group), frameTimingProbe = timingProbe, precisionProbe = precisionProbe,
                probeInBenchmarkPackage = probeInBenchmarkPackage,
                nativeVulkanProbe = nativeVulkanProbe, nativePluginSHA256 = nativeHash,
                nativeGpuTiming = nativeGpuTiming,
                frameTimingStatsEnabled = PlayerSettings.enableFrameTimingStats
            }, true));
            Debug.Log("DAMAGE_COMPARISON_APK=" + output); exitCode = 0;
        }
        catch (Exception error) { Debug.LogException(error); }
        finally
        {
            DamageTextComparisonShaderAudit.Collecting = false;
            PlayerSettings.SetScriptingBackend(group, previousBackend);
            PlayerSettings.Android.targetArchitectures = previousArchitectures;
            PlayerSettings.enableFrameTimingStats = previousFrameStats;
            PlayerSettings.SetApplicationIdentifier(group, previousIdentifier);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, previousApis);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, previousDefaultApis);
            EditorUserBuildSettings.buildAppBundle = previousBundle;
            stripping.intValue = previousStripping; graphics.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            if (temporaryScene != null) AssetDatabase.DeleteAsset(temporaryScene);
            if (temporaryPlugin != null) AssetDatabase.DeleteAsset(temporaryPlugin);
            File.WriteAllBytes("ProjectSettings/ProjectSettings.asset", playerSettings);
            File.WriteAllBytes("ProjectSettings/GraphicsSettings.asset", graphicsSettings);
        }
        EditorApplication.Exit(exitCode);
    }
}

/// <summary>Records compiler inputs only; successful compilation does not prove device pixel equivalence.</summary>
public sealed class DamageTextComparisonShaderAudit : IPreprocessShaders
{
    public static bool Collecting;
    public static int MeshEntries, InstancedEntries;
    public int callbackOrder => int.MaxValue;
    public static void Begin() { MeshEntries = InstancedEntries = 0; Collecting = true; }
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (!Collecting || (shader.name != "UIData/DamageText" && shader.name != "UIData/DamageTextInstanced")) return;
        foreach (var entry in data)
        {
            if (entry.shaderCompilerPlatform.ToString() != "Vulkan") continue;
            if (shader.name == "UIData/DamageText") MeshEntries++;
            else foreach (var keyword in entry.shaderKeywordSet.GetShaderKeywords())
                if (keyword.name == "INSTANCING_ON") { InstancedEntries++; break; }
        }
    }
}
