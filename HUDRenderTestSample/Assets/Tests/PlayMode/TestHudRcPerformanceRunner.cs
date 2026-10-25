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
    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_020.md
    [UnityTest]
    [Category("UT_PERF_020")]
    public IEnumerator BucketStrategies_PreserveRequestsAndConvergedOutput_AndReportSecondFrameWork()
    {
        Create(true);
        runner.configuration.holderCount = 129;
        runner.configuration.useBucketScheduling = true;
        runner.configuration.disableBuildMeshDiagnostics = true;
        runner.configuration.useGenerationBatch = true;
        HudRcPerformanceRunner.Report reference = null;
        foreach (var strategy in new[] { HolderBucketStrategy.SingleBucket, HolderBucketStrategy.SameFrame, HolderBucketStrategy.Rotating })
        {
            runner.configuration.bucketStrategy = strategy;
            runner.BeginRun();
            yield return Complete();
            var report = runner.CreateReport();
            Assert.AreEqual("hud-rc-buckets-v1", report.schema);
            Assert.AreEqual(24, report.frames.Length, "Each of three modes includes two explicit drain frames.");
            int drains = 0, secondFrameWork = 0;
            foreach (var frame in report.frames)
            {
                if (frame.drainFrame) { drains++; Assert.AreEqual(0, frame.requestedHolders); }
                if (frame.mode == (int)HudRcPerformanceRunner.Workload.Burst && !frame.drainFrame && frame.requestedHolders == 0 && frame.completedHolders > 0) secondFrameWork++;
                Assert.AreEqual(strategy == HolderBucketStrategy.SingleBucket ? 1 : 2, frame.logicalBuckets);
                Assert.AreEqual(frame.logicalBuckets, frame.batches);
                Assert.LessOrEqual(frame.completedFirstPendingAge, 1);
                Assert.GreaterOrEqual(frame.hudCpuMs, frame.updateCpuMs + frame.drawSubmitMs - 1e-6);
                Assert.That(frame.generationMs + frame.mergeMs, Is.EqualTo(frame.updateCpuMs).Within(1e-9));
                Assert.IsFalse(frame.buildMeshStagesAvailable);
                Assert.AreEqual(frame.refreshedBuckets * 2, frame.generationBatchCount);
            }
            Assert.AreEqual(6, drains);
            Assert.AreEqual(strategy == HolderBucketStrategy.Rotating ? 2 : 0, secondFrameWork);
            Assert.AreEqual(strategy == HolderBucketStrategy.Rotating ? 4 : 2, report.summaries[3].samples);
            foreach (var request in report.bucketRequests)
            {
                Assert.That(request.completedFrame - request.requestFrame, Is.InRange(0, 1));
                Assert.GreaterOrEqual(request.completedVersion, request.requestVersion);
            }
            foreach (var final in report.bucketFinalStates) Assert.AreEqual(final.requestedVersion, final.completedVersion);
            if (reference == null) reference = report;
            else
            {
                CollectionAssert.AreEqual(reference.bucketOutputChecksums, report.bucketOutputChecksums);
                Assert.AreEqual(reference.bucketRequests.Length, report.bucketRequests.Length);
                for (int i = 0; i < report.bucketRequests.Length; i++)
                {
                    Assert.AreEqual(reference.bucketRequests[i].holder, report.bucketRequests[i].holder);
                    Assert.AreEqual(reference.bucketRequests[i].requestVersion, report.bucketRequests[i].requestVersion);
                    Assert.AreEqual(reference.bucketRequests[i].requestFrame, report.bucketRequests[i].requestFrame);
                }
            }
            string path = null;
            try
            {
                runner.ExportReport(); path = runner.LastReportPath;
                var restored = JsonUtility.FromJson<HudRcPerformanceRunner.Report>(File.ReadAllText(path));
                Assert.AreEqual(report.bucketRequests.Length, restored.bucketRequests.Length);
                Assert.AreEqual(25, File.ReadAllLines(Path.ChangeExtension(path, null) + "-buckets.csv").Length);
            }
            finally
            {
                if (path != null)
                { File.Delete(path); File.Delete(Path.ChangeExtension(path, ".csv")); File.Delete(Path.ChangeExtension(path, null) + "-buckets.csv"); }
            }
            Assert.AreEqual(0, runner.outputCamera.GetCommandBuffers(UnityEngine.Rendering.CameraEvent.AfterForwardAlpha).Length);
            runner.ResetRun();
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_018.md
    [UnityTest]
    [Category("UT_PERF_018")]
    public IEnumerator StageDiagnosticsOff_PreservesWorkload_ReportsUnavailable_AndRestoresSettings()
    {
        Create();
        bool originalEnabled = BuildMeshDiagnostics.Enabled;
        bool originalQuad = BuildMeshDiagnostics.ProfileSimpleQuadWrites;
        try
        {
            runner.BeginRun();
            yield return Complete();
            var baseline = runner.CreateReport();
            runner.ResetRun();
            BuildMeshDiagnostics.Enabled = true;
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = true;
            runner.configuration.disableBuildMeshDiagnostics = true;
            runner.configuration.useGenerationBatch = true;
            runner.configuration.profileQuadWrites = true;
            runner.BeginRun();
            Assert.IsFalse(BuildMeshDiagnostics.Enabled);
            Assert.IsFalse(BuildMeshDiagnostics.ProfileSimpleQuadWrites);
            runner.configuration.disableBuildMeshDiagnostics = false;
            yield return Complete();
            var candidate = runner.CreateReport();
            Assert.IsTrue(candidate.configuration.disableBuildMeshDiagnostics, "Run uses a configuration snapshot.");
            Assert.IsTrue(BuildMeshDiagnostics.Enabled);
            Assert.IsTrue(BuildMeshDiagnostics.ProfileSimpleQuadWrites);
            Assert.AreEqual(baseline.frames.Length, candidate.frames.Length);
            for (int i = 0; i < candidate.frames.Length; i++)
            {
                var a = baseline.frames[i];
                var b = candidate.frames[i];
                Assert.AreEqual(a.updatedHolders, b.updatedHolders);
                Assert.AreEqual(a.selectionChecksum, b.selectionChecksum);
                Assert.AreEqual(a.vertices, b.vertices);
                Assert.AreEqual(a.indices, b.indices);
                Assert.AreEqual(a.batches, b.batches);
                Assert.AreEqual(b.updatedHolders > 0 ? 2 : 0, b.generationBatchCount);
                Assert.AreEqual(0, a.generationBatchCount);
                Assert.IsFalse(b.buildMeshStagesAvailable);
                Assert.IsFalse(b.buildMeshWriteAvailable);
                Assert.AreEqual(-1, b.buildMeshMs);
                Assert.AreEqual(-1, b.holderSelectionMs);
                Assert.AreEqual(-1, b.buildMeshDoGenerateMs);
                Assert.AreEqual(-1, b.buildMeshDrawingDimensionsMs);
                Assert.AreEqual(-1, b.buildMeshWriteVertexMs);
                Assert.GreaterOrEqual(b.generationMs, 0);
                Assert.GreaterOrEqual(b.mergeMs, 0);
            }
            runner.ExportReport();
            var csv = File.ReadAllLines(Path.ChangeExtension(runner.LastReportPath, ".csv"));
            StringAssert.Contains("build_mesh_stages_available", csv[0]);
            StringAssert.Contains(",unavailable,unavailable,unavailable,", csv[1]);
            File.Delete(runner.LastReportPath);
            File.Delete(Path.ChangeExtension(runner.LastReportPath, ".csv"));
            runner.BeginRun();
            runner.ResetRun();
            Assert.IsTrue(BuildMeshDiagnostics.Enabled, "Cancellation restores settings too.");
        }
        finally
        {
            runner.ResetRun();
            BuildMeshDiagnostics.Enabled = originalEnabled;
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = originalQuad;
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_017.md
    [UnityTest]
    [Category("UT_PERF_017")]
    public IEnumerator SpriteComparison_UsesSelectedPath_AndRestoresSwitchAndResources()
    {
        Create();
        bool original = UIImage.EnableSimpleMeshFastPath;
        try
        {
            foreach (bool slim in new[] { false, true })
            foreach (bool fallback in new[] { false, true })
            {
                UIImage.EnableSimpleMeshFastPath = fallback;
                runner.configuration.useSlim = slim;
                runner.configuration.spriteBacked = true;
                runner.configuration.forceUguiFallback = fallback;
                runner.BeginRun();
                var images = root.GetComponentsInChildren<UIImage>(true);
                Assert.AreEqual(2, images.Length);
                var sprite = images[0].sprite;
                Assert.IsNotNull(sprite);
                foreach (var image in images)
                    Assert.AreEqual(!fallback, image.LastGenerationUsedSimpleMeshFastPath);
                runner.configuration.forceUguiFallback = !fallback;
                yield return Complete();
                var report = runner.CreateReport();
                Assert.IsTrue(report.configuration.spriteBacked);
                Assert.AreEqual(fallback, report.configuration.forceUguiFallback);
                Assert.AreEqual(2, report.preparedSpriteImages);
                Assert.AreEqual(fallback ? 0 : 2, report.preparedFastPathImages);
                Assert.AreEqual(fallback, UIImage.EnableSimpleMeshFastPath, "Completion restores the pre-run switch.");
                foreach (var frame in report.frames)
                {
                    Assert.AreEqual(64, frame.vertices);
                    Assert.AreEqual(96, frame.indices);
                    if (frame.updatedHolders == 0) continue;
                    Assert.AreEqual(0, fallback ? frame.buildMeshSimplePathMs : frame.buildMeshUguiFallbackMs);
                }
                runner.ResetRun();
                yield return null;
                Assert.IsTrue(sprite == null, "Owned workload sprite must be destroyed.");
                Assert.AreEqual(0, root.GetComponentsInChildren<UIImage>(true).Length);
            }
        }
        finally
        {
            runner.ResetRun();
            UIImage.EnableSimpleMeshFastPath = original;
        }
    }

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
        bool diagnostics = BuildMeshDiagnostics.Enabled, quad = BuildMeshDiagnostics.ProfileSimpleQuadWrites;
        try
        {
            BuildMeshDiagnostics.Enabled = false;
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = true;
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
            Assert.IsFalse(BuildMeshDiagnostics.Enabled);
            Assert.IsTrue(BuildMeshDiagnostics.ProfileSimpleQuadWrites);
            Assert.AreEqual(0, runner.LiveHolderCount);
            Assert.AreEqual(0, runner.SampleCount);
            yield return null;
            Assert.AreEqual(0, root.transform.childCount);
            runner.BeginRun();
            yield return Complete();
            Assert.AreEqual(.5f, Time.timeScale);
            Assert.AreEqual(1, QualitySettings.vSyncCount);
            Assert.AreEqual(37, Application.targetFrameRate);
            Assert.IsFalse(BuildMeshDiagnostics.Enabled);
            Assert.IsTrue(BuildMeshDiagnostics.ProfileSimpleQuadWrites);
        }
        finally
        {
            if (runner != null) runner.ResetRun();
            Time.timeScale = scale;
            QualitySettings.vSyncCount = sync;
            Application.targetFrameRate = rate;
            BuildMeshDiagnostics.Enabled = diagnostics;
            BuildMeshDiagnostics.ProfileSimpleQuadWrites = quad;
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_016.md
    [UnityTest]
    [Category("UT_PERF_016")]
    public IEnumerator QuadWriteReport_ExportsAvailability_AndCountsThroughBothPaths()
    {
        Create();
        foreach (bool slim in new[] { false, true })
        foreach (bool enabled in new[] { false, true })
        {
            runner.ResetRun();
            runner.configuration.useSlim = slim;
            runner.configuration.profileQuadWrites = enabled;
            runner.BeginRun();
            runner.configuration.profileQuadWrites = !enabled;
            yield return Complete();
            string path = null;
            try
            {
                runner.ExportReport();
                path = runner.LastReportPath;
                var report = JsonUtility.FromJson<HudRcPerformanceRunner.Report>(File.ReadAllText(path));
                Assert.AreEqual("hud-rc-v2", report.schema);
                Assert.AreEqual(enabled, report.configuration.profileQuadWrites);
                Assert.AreEqual(slim, report.configuration.useSlim);
                Assert.Greater(report.quadWriteTimestampFrequency, 0);
                var rows = File.ReadAllLines(Path.ChangeExtension(path, ".csv"));
                var header = rows[0].Split(',');
                int storageColumn = System.Array.IndexOf(header, "build_mesh_write_storage_ms");
                int countColumn = System.Array.IndexOf(header, "build_mesh_write_samples");
                int availableColumn = System.Array.IndexOf(header, "build_mesh_write_available");
                Assert.GreaterOrEqual(storageColumn, 0);
                Assert.GreaterOrEqual(countColumn, 0);
                Assert.GreaterOrEqual(availableColumn, 0);
                for (int i = 0; i < report.frames.Length; i++)
                {
                    var sample = report.frames[i];
                    bool available = enabled && sample.updatedHolders > 0;
                    Assert.AreEqual(available, sample.buildMeshWriteAvailable);
                    Assert.AreEqual(enabled ? sample.updatedHolders * 2 : 0, sample.buildMeshWriteSamples);
                    var row = rows[i + 1].Split(',');
                    Assert.AreEqual(header.Length, row.Length);
                    Assert.AreEqual(sample.buildMeshWriteSamples.ToString(), row[countColumn]);
                    Assert.AreEqual(available.ToString(), row[availableColumn]);
                    var stages = new[] { sample.buildMeshWriteStorageMs, sample.buildMeshWriteVertexMs,
                        sample.buildMeshWriteUvMs, sample.buildMeshWriteColorMs, sample.buildMeshWriteIndexMs };
                    for (int stage = 0; stage < stages.Length; stage++)
                    {
                        if (available)
                        {
                            Assert.GreaterOrEqual(stages[stage], 0);
                            Assert.That(double.Parse(row[storageColumn + stage],
                                System.Globalization.CultureInfo.InvariantCulture),
                                Is.EqualTo(stages[stage]).Within(1e-12));
                        }
                        else
                        {
                            Assert.AreEqual(-1, stages[stage]);
                            Assert.AreEqual("unavailable", row[storageColumn + stage]);
                        }
                    }
                }
            }
            finally
            {
                if (path != null) { File.Delete(path); File.Delete(Path.ChangeExtension(path, ".csv")); }
            }
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
