using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class TestHudRcPerformanceRunner
{
    private GameObject root;
    private GameObject cameraObject;
    private HudRcPerformanceRunner runner;
    private Scene loadedScene;

    private HudRcPerformanceRunner Create(bool slim = false)
    {
        root = new GameObject("HUD RC test");
        cameraObject = new GameObject("HUD RC test camera", typeof(Camera));
        runner = root.AddComponent<HudRcPerformanceRunner>();
        runner.autoRun = runner.autoExport = false;
        runner.outputCamera = cameraObject.GetComponent<Camera>();
        runner.batchShader = Shader.Find("Hidden/UIE-AtlasBlit");
        Configure(runner, slim);
        return runner;
    }

    private static void Configure(HudRcPerformanceRunner value, bool slim)
    {
        value.configuration.holderCount = 8;
        value.configuration.quadsPerHolder = 2;
        value.configuration.warmupFrames = 2;
        value.configuration.sampleFrames = 6;
        value.configuration.burstEveryFrames = 3;
        value.configuration.partialPercent = 25;
        value.configuration.useSlim = slim;
    }

    private IEnumerator Complete()
    {
        for (int i = 0; i < 100 && runner.State != HudRcPerformanceRunner.RunState.Completed; i++)
        {
            Assert.AreNotEqual(HudRcPerformanceRunner.RunState.Failed, runner.State, runner.Failure);
            yield return null;
        }
        Assert.AreEqual(HudRcPerformanceRunner.RunState.Completed, runner.State);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (runner != null) runner.ResetRun();
        if (root != null) Object.Destroy(root);
        if (cameraObject != null) Object.Destroy(cameraObject);
        if (loadedScene.IsValid() && loadedScene.isLoaded) yield return SceneManager.UnloadSceneAsync(loadedScene);
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_012.md
    [UnityTest]
    [Category("UT_PERF_012")]
    public IEnumerator DedicatedScene_HasBindings_AndProducesRealGeometry()
    {
#if UNITY_EDITOR
        loadedScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Scene_Perf_HUD_RC.unity", new LoadSceneParameters(LoadSceneMode.Additive));
        // PlayMode scene loading completes on the next frame before roots are available.
        yield return null;
        foreach (var go in loadedScene.GetRootGameObjects())
        {
            var candidate = go.GetComponent<HudRcPerformanceRunner>();
            if (candidate != null) runner = candidate;
        }
        Assert.NotNull(runner);
        runner.autoRun = runner.autoExport = false;
        Configure(runner, false);
        Assert.NotNull(runner.outputCamera);
        Assert.NotNull(runner.batchShader);
        runner.BeginRun();
        Assert.AreEqual(8, runner.LiveHolderCount);
        Assert.AreEqual(64, runner.VertexCount);
        Assert.AreEqual(96, runner.IndexCount);
        yield return Complete();
        Assert.AreEqual(18, runner.SampleCount);
#else
        Assert.Ignore("Committed scene asset loading uses the Editor PlayMode entry.");
        yield break;
#endif
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_013.md
    [UnityTest]
    [Category("UT_PERF_013")]
    public IEnumerator Modes_ReplayWorkload_ThroughBothMeshPaths()
    {
        Create();
        runner.BeginRun();
        yield return Complete();
        var first = runner.CreateReport();
        for (int i = 0; i < 18; i++)
        {
            var s = first.frames[i];
            int expected = i < 6 ? 0 : i < 12 ? 2 : (i % 6 == 0 || i % 6 == 3 ? 8 : 0);
            Assert.AreEqual(expected, s.updatedHolders);
            Assert.AreEqual(64, s.vertices);
            Assert.Greater(s.batches, 0);
        }
        runner.ResetRun();
        runner.configuration.useSlim = true;
        runner.BeginRun();
        yield return Complete();
        for (int i = 0; i < 18; i++)
        {
            Assert.AreEqual(first.frames[i].selectionChecksum, runner.GetSample(i).selectionChecksum);
            Assert.AreEqual(first.frames[i].vertices, runner.GetSample(i).vertices);
            Assert.AreEqual(first.frames[i].updatedHolders, runner.GetSample(i).updatedHolders);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_014.md
    [UnityTest]
    [Category("UT_PERF_014")]
    public IEnumerator ResetDuringSampling_RestoresSettings_AndCleansObjects()
    {
        float scale = Time.timeScale;
        int sync = QualitySettings.vSyncCount, rate = Application.targetFrameRate;
        try
        {
            Time.timeScale = .5f;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 37;
            Create();
            runner.BeginRun();
            yield return null;
            runner.ResetRun();
            Assert.AreEqual(.5f, Time.timeScale);
            Assert.AreEqual(1, QualitySettings.vSyncCount);
            Assert.AreEqual(37, Application.targetFrameRate);
            Assert.AreEqual(0, runner.LiveHolderCount);
            Assert.AreEqual(0, runner.SampleCount);
            yield return null;
            Assert.AreEqual(0, root.transform.childCount);
            runner.BeginRun();
            yield return Complete();
            Assert.AreEqual(.5f, Time.timeScale);
            Assert.AreEqual(1, QualitySettings.vSyncCount);
            Assert.AreEqual(37, Application.targetFrameRate);
        }
        finally
        {
            if (runner != null) runner.ResetRun();
            Time.timeScale = scale;
            QualitySettings.vSyncCount = sync;
            Application.targetFrameRate = rate;
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_015.md
    [UnityTest]
    [Category("UT_PERF_015")]
    public IEnumerator Report_PreservesConfiguration_AndSeparatesBurstRefreshFrames()
    {
        Create();
        runner.BeginRun();
        // Inspector changes during a run must not alter the recorded workload snapshot.
        runner.configuration.holderCount = 100;
        yield return Complete();
        var report = runner.CreateReport();
        Assert.AreEqual(8, report.configuration.holderCount);
        Assert.AreEqual(4, report.summaries.Length);
        Assert.AreEqual("refresh-frames", report.summaries[3].subset);
        Assert.AreEqual(2, report.summaries[3].samples);
        foreach (var s in report.frames)
        {
            if (!s.mainThreadAvailable) Assert.AreEqual(-1, s.mainThreadMs);
            if (!s.frameGcAvailable) Assert.AreEqual(-1, s.frameGcBytes);
        }
        string path = null;
        try
        {
            runner.ExportReport();
            path = runner.LastReportPath;
            var restored = JsonUtility.FromJson<HudRcPerformanceRunner.Report>(File.ReadAllText(path));
            Assert.AreEqual(18, restored.frames.Length);
            Assert.AreEqual(8, restored.configuration.holderCount);
            Assert.AreEqual(19, File.ReadAllLines(Path.ChangeExtension(path, ".csv")).Length);
        }
        finally
        {
            if (path != null) { File.Delete(path); File.Delete(Path.ChangeExtension(path, ".csv")); }
        }
    }
}
