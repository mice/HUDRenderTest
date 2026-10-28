using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public sealed class TestDamageTextBenchmark
{
    [Serializable] private sealed class InputRegistry
    {
        public string schema = "damage-comparison-local-inputs-v1";
        public string sourceFingerprint, atlasHash, styleHash, sceneSHA256;
        public string deviceCapture = "deferred; no APK or device timing recorded";
        public RegistryClip[] clips;
    }
    [Serializable] private struct RegistryClip { public string name, clipHash, assetSHA256; public int events; public float duration; }
    private Camera _camera;
    private DamageTextGlyphAtlas _atlas;
    private DamageTextStyles _styles;
    private DamageTextConfiguration _configuration;

    [SetUp]
    public void SetUp()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics context required by renderer adapters.");
        _camera = new GameObject("Benchmark test camera", typeof(Camera)).GetComponent<Camera>();
        _camera.enabled = false; _camera.orthographic = true; _camera.transform.position = new Vector3(0, 0, -10);
        _atlas = ScriptableObject.CreateInstance<DamageTextGlyphAtlas>();
        var glyphs = new DamageTextGlyph[10];
        for (int i = 0; i < glyphs.Length; i++) glyphs[i] = new DamageTextGlyph
        { Available = true, Size = new Vector2(5, 7), Advance = 6, Uv = new Rect(0, 0, 1, 1) };
        _atlas.Initialize(Texture2D.whiteTexture, 24, glyphs, "benchmark-fixture-v1", "white-fixture");
        _styles = ScriptableObject.CreateInstance<DamageTextStyles>();
        _configuration = new DamageTextConfiguration { MaximumBuckets = 4, MaximumPendingEvents = 512 };
    }
    [TearDown]
    public void TearDown()
    {
        if (_camera != null) UnityEngine.Object.DestroyImmediate(_camera.gameObject);
        UnityEngine.Object.DestroyImmediate(_atlas); UnityEngine.Object.DestroyImmediate(_styles);
    }
    private static DamageTextRequest Request(uint id, float time, DamageTextKind kind = DamageTextKind.Normal)
        => new DamageTextRequest { EventId = id, Amount = id == 1 ? 99999999u : 10, Kind = kind, SpawnTime = time, RandomSeed = id };
    private DamageTextClip Clip() => new DamageTextClip(new[] { Request(1, 0), Request(2, .2f, DamageTextKind.Critical),
        Request(3, 1, DamageTextKind.Healing) }, 3, "benchmark-lifecycle-v1", 1);
    private void Prepare(DamageTextBenchmark benchmark, DamageTextBenchmarkPlan plan, DamageTextClip clip = null)
        => benchmark.Prepare(clip ?? Clip(), _configuration, _atlas, _styles,
            Shader.Find(plan.Geometry == DamageTextGeometry.InstancedQuad ? "UIData/DamageTextInstanced" : "UIData/DamageText"), _camera, plan);
    private static void Complete(DamageTextBenchmark benchmark)
    {
        for (int i = 0; benchmark.IsRunning && i < 100; i++) benchmark.Advance(1.0 / 60, i);
        Assert.AreEqual(DamageTextBenchmarkStage.Completed, benchmark.Stage, benchmark.Report.reason);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_117.md
    [UnityTest]
    [Category("UT_DAMAGE_117")]
    public IEnumerator FixedLifecycle_AdaptersConsumeSameTimesAndLiveSetAfterWarmupBarrier()
    {
        if (!SystemInfo.supportsInstancing) Assert.Ignore("Instancing unavailable.");
        DamageTextBenchmarkReport reference = null;
        foreach (var geometry in new[] { DamageTextGeometry.PersistentMesh, DamageTextGeometry.InstancedQuad })
        {
            using (var benchmark = new DamageTextBenchmark())
            {
                Prepare(benchmark, new DamageTextBenchmarkPlan { Geometry = geometry, WarmupFrames = 2, SampleFrames = 7 });
                Complete(benchmark);
                var report = benchmark.Report; Assert.AreEqual(7, report.samples); Assert.AreEqual(3, report.consumedEvents);
                Assert.IsTrue(report.completeEventInterval); Assert.AreEqual(report.expectedRequestHash, report.consumedRequestHash);
                int warmup = 0, barriers = 0, samples = 0;
                foreach (var frame in report.frames)
                {
                    if (frame.stage == DamageTextBenchmarkStage.Warming) warmup++;
                    if (frame.stage == DamageTextBenchmarkStage.Barrier) barriers++;
                    if (frame.stage != DamageTextBenchmarkStage.Sampling) continue;
                    Assert.IsTrue(frame.stateMatches, "Oracle mismatch at " + frame.time);
                    Assert.AreEqual((float)report.registeredTimes[samples++], frame.time);
                    Assert.AreEqual(-1, frame.gpuMs);
                }
                Assert.AreEqual(2, warmup); Assert.AreEqual(2, barriers); Assert.AreEqual(7, samples);
                Assert.AreEqual(3, report.completedWindows);
                if (reference == null) reference = report;
                else
                {
                    Assert.AreEqual(reference.timesHash, report.timesHash); Assert.AreEqual(reference.workloadHash, report.workloadHash);
                    Assert.AreEqual(reference.consumedRequestHash, report.consumedRequestHash);
                    for (int i = 0; i < report.frames.Length; i++)
                        Assert.AreEqual(reference.frames[i].actualLiveChecksum, report.frames[i].actualLiveChecksum);
                }
            }
            Assert.AreEqual(0, _camera.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Length);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_118.md
    [UnityTest]
    [Category("UT_DAMAGE_118")]
    public IEnumerator DrawOnly_NormalReplayCheckpointRetainsExpiredGeometryThroughoutSampling()
    {
        var clip = new DamageTextClip(new[] { Request(1, 0), Request(2, 0, DamageTextKind.Healing) }, 2, "frozen-history-v1", 1);
        using (var benchmark = new DamageTextBenchmark())
        {
            Prepare(benchmark, new DamageTextBenchmarkPlan { Phase = DamageTextBenchmarkPhase.DrawOnly, Checkpoint = .8f,
                WarmupFrames = 2, SampleFrames = 4 }, clip);
            Assert.AreEqual(8, benchmark.Renderer.Statistics.ExpiredResidentCharacters);
            Complete(benchmark);
            foreach (var frame in benchmark.Report.frames)
            {
                Assert.AreEqual(10, frame.statistics.SubmittedCharacters); Assert.AreEqual(8, frame.statistics.ExpiredResidentCharacters);
                Assert.AreEqual(.8f, frame.time); Assert.IsFalse(frame.refreshFrame); Assert.IsTrue(frame.stateMatches);
                Assert.AreEqual(1, frame.statistics.VertexUploads);
            }
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_119.md
    [UnityTest]
    [Category("UT_DAMAGE_119")]
    public IEnumerator SlowRealtimeFrame_RecordsConsumedButNeverPublishedEventWithoutChangingClockHistory()
    {
        var clip = new DamageTextClip(new[] { Request(1, 0), Request(2, 1, DamageTextKind.Healing) }, 3, "slow-frame-v1", 1);
        using (var benchmark = new DamageTextBenchmark())
        {
            Prepare(benchmark, new DamageTextBenchmarkPlan { Mode = DamageTextBenchmarkMode.Realtime, WarmupFrames = 0, SampleFrames = 3 }, clip);
            benchmark.Advance(0, 0); benchmark.Advance(2.5, 1); benchmark.Advance(.5, 2);
            Assert.AreEqual(DamageTextBenchmarkStage.Completed, benchmark.Stage);
            Assert.AreEqual(2, benchmark.Report.consumedEvents); Assert.AreEqual(0, benchmark.Report.registeredTimes.Length);
            Assert.AreEqual(2.5f, benchmark.Report.events[1].consumedAt); Assert.AreEqual(-1, benchmark.Report.events[1].firstPublishedAt);
            Assert.IsTrue(benchmark.Report.events[1].missed); Assert.IsTrue(benchmark.Report.frames[1].stateMatches);
            Assert.AreEqual(1, benchmark.Report.frames[1].statistics.MissedPresentation);
            Assert.AreEqual(0, benchmark.Report.frames[1].statistics.LiveCharacters);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_120.md
    [UnityTest]
    [Category("UT_DAMAGE_120")]
    public IEnumerator UserIntervention_AbortsSegmentAndPreservesRawPartialFrames()
    {
        using (var benchmark = new DamageTextBenchmark())
        {
            Prepare(benchmark, new DamageTextBenchmarkPlan { WarmupFrames = 0, SampleFrames = 7 });
            benchmark.Advance(0, 0); benchmark.Advance(.1, 1); benchmark.Abort("pause-seek-step-or-speed-change");
            Assert.IsTrue(benchmark.Report.aborted); Assert.AreEqual(2, benchmark.Report.samples);
            Assert.AreEqual(2, benchmark.Report.frames.Length); Assert.AreEqual("pause-seek-step-or-speed-change", benchmark.Report.reason);
            benchmark.Advance(1, 2); Assert.AreEqual(2, benchmark.Report.frames.Length);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_121.md
    [UnityTest]
    [Category("UT_DAMAGE_121")]
    public IEnumerator Export_WritesRawJsonCsvAndVerifiableArtifactManifestAfterCompletion()
    {
        string directory = Path.Combine(Application.temporaryCachePath, "DamageBenchmark-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var benchmark = new DamageTextBenchmark())
            {
                Prepare(benchmark, new DamageTextBenchmarkPlan { WarmupFrames = 0, SampleFrames = 8 }); Complete(benchmark);
                benchmark.Report.formal = false; benchmark.Report.reason = "functional serializer smoke; not performance evidence";
                string path = DamageTextBenchmarkFiles.Save(benchmark.Report, directory);
                var saved = JsonUtility.FromJson<DamageTextBenchmarkReport>(File.ReadAllText(path));
                Assert.AreEqual(8, saved.samples); Assert.AreEqual(8, saved.frames.Length); Assert.AreEqual(3, saved.events.Length);
                Assert.AreEqual(saved.timesHash, DamageTextHash.Compute(writer =>
                {
                    writer.Write("damage-times-v1"); foreach (string bits in saved.registeredTimeBits)
                        writer.Write(long.Parse(bits, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture));
                }));
                for (int i = 0; i < saved.registeredTimeBits.Length; i++)
                    Assert.AreEqual((float)BitConverter.Int64BitsToDouble(long.Parse(saved.registeredTimeBits[i],
                        System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)), saved.frames[i].time);
                StringAssert.Contains("stride=68|Position:Float32x2@0#0", saved.vertexLayout);
                Assert.AreEqual(1, saved.shaderPassCount);
                string stem = path.Substring(0, path.Length - ".report.json".Length);
                string manifest = File.ReadAllText(stem + ".manifest.json");
                StringAssert.Contains(DamageTextBenchmarkFiles.HashFile(path), manifest);
                StringAssert.Contains(DamageTextBenchmarkFiles.HashFile(stem + ".frames.csv"), manifest);
                Assert.AreEqual(9, File.ReadAllLines(stem + ".frames.csv").Length);
                Assert.AreEqual("unavailable", saved.gpuStatus); Assert.AreEqual(-1, saved.frames[0].gpuMs);
                string evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "../../TestResults/DamageTextComparison/ActualSerializer"));
                Directory.CreateDirectory(evidence);
                foreach (string artifact in Directory.GetFiles(directory)) File.Copy(artifact, Path.Combine(evidence, Path.GetFileName(artifact)), true);
            }
        }
        finally
        {
            if (Directory.Exists(directory)) { foreach (string path in Directory.GetFiles(directory)) File.Delete(path); Directory.Delete(directory); }
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_122.md
    [UnityTest]
    [Category("UT_DAMAGE_122")]
    public IEnumerator DualPreview_UsesOneClockAndRestoresOriginalCameraWithoutFormalSamples()
    {
        if (!SystemInfo.supportsInstancing) Assert.Ignore("Instancing unavailable.");
        int camerasBefore = UnityEngine.Object.FindObjectsOfType<Camera>().Length;
        _camera.enabled = true;
        using (var preview = new DamageTextComparisonPreview())
        {
            preview.Open(_camera, Clip(), _configuration, _atlas, _styles, Shader.Find("UIData/DamageText"),
                Shader.Find("UIData/DamageTextInstanced"), true);
            Assert.IsFalse(preview.Formal); Assert.IsFalse(_camera.enabled);
            preview.Step(.5); Assert.AreEqual(preview.Left.PlaybackTime, preview.Right.PlaybackTime);
            Assert.AreEqual(preview.Left.Renderer.Statistics.LiveEventChecksum, preview.Right.Renderer.Statistics.LiveEventChecksum);
            preview.Seek(.1f); Assert.IsTrue(preview.Paused);
            Assert.AreEqual(.1f, preview.Left.PlaybackTime); Assert.AreEqual(.1f, preview.Right.PlaybackTime);
            preview.Advance(1); Assert.AreEqual(.1f, preview.PlaybackTime);
        }
        Assert.IsTrue(_camera.enabled);
        yield return null;
        Assert.AreEqual(camerasBefore, UnityEngine.Object.FindObjectsOfType<Camera>().Length, "Preview cameras are owned and released.");
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_123.md
    [UnityTest]
    [Category("UT_DAMAGE_123")]
    public IEnumerator PrecisionFailure_SelectsFloat32BeforeWarmupAndKeepsRequestedFormatInReport()
    {
        var glyphs = new DamageTextGlyph[10];
        for (int i = 0; i < glyphs.Length; i++) glyphs[i] = new DamageTextGlyph
        { Available = true, Size = new Vector2(5.0179f, 7.0133f), Advance = 6.1337f, Uv = new Rect(0, 0, 1, 1) };
        _atlas.Initialize(Texture2D.whiteTexture, 24, glyphs, "fractional-precision-v1", "white-fixture");
        var critical = _styles.Critical; critical.PixelSize = 512; critical.PopAmplitude = 4; critical.Tracking = 1.1234f; _styles.Critical = critical;
        var clip = new DamageTextClip(new[] { Request(1, 0, DamageTextKind.Critical) }, 2, "maximum-scale-v1", 1);
        using (var benchmark = new DamageTextBenchmark())
        {
            Prepare(benchmark, new DamageTextBenchmarkPlan { PositionFormat = DamageTextPositionFormat.Float16, WarmupFrames = 2, SampleFrames = 4 }, clip);
            Assert.AreEqual("Float16", benchmark.Report.requestedFormat); Assert.AreEqual("Float32", benchmark.Report.effectiveFormat);
            Assert.Greater(benchmark.Report.precision.maximumPixelError, .25f);
            Assert.AreEqual("precision-exceeds-0.25px-candidate-threshold", benchmark.Report.formatReason);
            Complete(benchmark); Assert.AreEqual(68, benchmark.Renderer.Statistics.VertexStride);
            Assert.AreEqual("Float32", benchmark.Report.effectiveFormat);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_124.md
    [UnityTest]
    [Category("UT_DAMAGE_124")]
    public IEnumerator ComparisonAssets_ProvideRegisteredMatrixAnd100CompleteBurstWindows()
    {
#if UNITY_EDITOR
        var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Scene_DamageTextComparison.unity",
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
        yield return null;
        try
        {
            DamageTextBenchmarkRunner runner = null;
            foreach (var root in scene.GetRootGameObjects()) { runner = root.GetComponent<DamageTextBenchmarkRunner>(); if (runner != null) break; }
            Assert.IsNotNull(runner, "Comparison runner"); Assert.IsFalse(runner.autoRun);
            Assert.IsNotNull(runner.meshShader, "Mesh Shader"); Assert.IsNotNull(runner.instanceShader, "Instancing Shader");
            string[] names = { "Static128_2", "Static128_5", "Static128_8", "Static128_8_HighOverlap", "Continuous60s", "Bursts127_100", "Bursts128_100", "Bursts129_100", "Bursts256_100" };
            var registered = new RegistryClip[names.Length];
            for (int clipIndex = 0; clipIndex < names.Length; clipIndex++)
            {
                string name = names[clipIndex];
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageTextClipAsset>("Assets/DamageTextComparison/" + name + ".asset");
                Assert.IsNotNull(asset, name); var frozen = asset.Freeze(); Assert.Greater(frozen.Count, 0);
                registered[clipIndex] = new RegistryClip { name = name, clipHash = frozen.Hash, events = frozen.Count, duration = frozen.Duration,
                    assetSHA256 = DamageTextBenchmarkFiles.HashFile(Path.Combine(Application.dataPath, "DamageTextComparison/" + name + ".asset")) };
            }
            runner.styles.Freeze(out string styleHash);
            string evidenceDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../TestResults/DamageTextComparison")); Directory.CreateDirectory(evidenceDirectory);
            File.WriteAllText(Path.Combine(evidenceDirectory, "input-registry.json"), JsonUtility.ToJson(new InputRegistry
            {
                sourceFingerprint = DamageTextBenchmarkRunner.CalculateSourceFingerprint(Path.GetFullPath(Path.Combine(Application.dataPath, ".."))),
                atlasHash = runner.atlas.Freeze().Hash, styleHash = styleHash, clips = registered,
                sceneSHA256 = DamageTextBenchmarkFiles.HashFile(Path.Combine(Application.dataPath, "Scenes/Scene_DamageTextComparison.unity"))
            }, true));
            var burst = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageTextClipAsset>("Assets/DamageTextComparison/Bursts256_100.asset");
            using (var benchmark = new DamageTextBenchmark())
            {
                benchmark.Prepare(burst.Freeze(), runner.configuration, runner.atlas, runner.styles, runner.meshShader, _camera,
                    new DamageTextBenchmarkPlan { WarmupFrames = 0, SampleFrames = 3000, BurstWorkload = true });
                for (int i = 0; benchmark.IsRunning && i < 3001; i++) benchmark.Advance(1.0 / 60, i);
                Assert.AreEqual(DamageTextBenchmarkStage.Completed, benchmark.Stage, benchmark.Report.reason);
                Assert.AreEqual(25600, benchmark.Report.consumedEvents); Assert.AreEqual(100, benchmark.Report.completedWindows);
                foreach (var frame in benchmark.Report.frames)
                {
                    Assert.IsTrue(frame.stateMatches); Assert.AreEqual(0, frame.statistics.Rejected); Assert.AreEqual(0, frame.statistics.MissedPresentation);
                }
            }
        }
        finally { UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene); }
#endif
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_126.md
    [UnityTest]
    [Category("UT_DAMAGE_126")]
    public IEnumerator FrozenDraw_ResourceChangeAbortsInsteadOfRecordingEmptyDrawFrames()
    {
        using (var benchmark = new DamageTextBenchmark())
        {
            Prepare(benchmark, new DamageTextBenchmarkPlan { Phase = DamageTextBenchmarkPhase.DrawOnly, WarmupFrames = 0, SampleFrames = 7 });
            benchmark.Advance(0, 0);
            _atlas.Initialize(Texture2D.whiteTexture, 24, new DamageTextGlyph[10], "changed-resource-v2", "white-fixture");
            benchmark.Advance(.1, 1);
            Assert.AreEqual(DamageTextBenchmarkStage.Aborted, benchmark.Stage); Assert.IsTrue(benchmark.Report.aborted);
            Assert.AreEqual("draw-resource-unavailable-or-changed", benchmark.Report.reason);
            Assert.AreEqual(1, benchmark.Report.samples); Assert.AreEqual(1, benchmark.Report.frames.Length);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_127.md
    [UnityTest]
    [Category("UT_DAMAGE_127")]
    public IEnumerator FinalFrame_DefersReportAllocationUntilAfterFrameCountersAreAttached()
    {
        using (var benchmark = new DamageTextBenchmark())
        {
            Prepare(benchmark, new DamageTextBenchmarkPlan { WarmupFrames = 0, SampleFrames = 2 });
            benchmark.Advance(0, 0); benchmark.Advance(.1, 1);
            Assert.AreEqual(DamageTextBenchmarkStage.Completed, benchmark.Stage);
            Assert.IsFalse(benchmark.IsReportFinalized);
            benchmark.AttachPreviousFrameCounters(1, 1.2, .3, 0, true, true, true);
            var report = benchmark.Report;
            Assert.IsTrue(benchmark.IsReportFinalized); Assert.IsTrue(report.frames[1].gcAvailable);
            Assert.AreEqual(0, report.frames[1].gcBytes); Assert.AreEqual(1.2, report.frames[1].mainThreadMs);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_128.md
    [UnityTest]
    [Category("UT_DAMAGE_128")]
    public IEnumerator ReturnedReportMetadata_CannotMutateTheRegisteredClockOrWarmupResetConfiguration()
    {
        using (var benchmark = new DamageTextBenchmark())
        {
            Prepare(benchmark, new DamageTextBenchmarkPlan { WarmupFrames = 2, SampleFrames = 2 });
            var metadata = benchmark.Report; metadata.plan.SampleFrames = 99; metadata.registeredTimes[0] = 2;
            metadata.rendererConfiguration.MaximumBuckets = 1;
            Complete(benchmark);
            Assert.AreEqual(2, benchmark.SampleCount); Assert.AreEqual(4, benchmark.Renderer.Statistics.PoolBuckets);
            foreach (var frame in benchmark.Report.frames)
                if (frame.stage == DamageTextBenchmarkStage.Sampling && frame.index == 0) Assert.AreEqual(0, frame.time);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_129.md
    [UnityTest]
    [Category("UT_DAMAGE_129")]
    public IEnumerator CaptureRequest_SelectsBothAdaptersFromOneEmbeddedCatalogAndRecordsIdentity()
    {
#if UNITY_EDITOR
        if (!SystemInfo.supportsInstancing) Assert.Ignore("Instancing unavailable.");
        var asset = ScriptableObject.CreateInstance<DamageTextClipAsset>();
        asset.name = "CaptureFixture"; asset.Duration = 3; asset.Events = new[] { Request(1, 0) };
        try
        {
            string source = null;
            foreach (var geometry in new[] { DamageTextGeometry.InstancedQuad, DamageTextGeometry.PersistentMesh })
            {
                var go = new GameObject("Capture request runner");
                try
                {
                    var runner = go.AddComponent<DamageTextBenchmarkRunner>();
                    runner.outputCamera = _camera; runner.atlas = _atlas; runner.styles = _styles; runner.configuration = _configuration;
                    runner.meshShader = Shader.Find("UIData/DamageText"); runner.instanceShader = Shader.Find("UIData/DamageTextInstanced");
                    runner.captureClips = new[] { asset }; runner.RefreshSourceMetadata();
                    var request = new DamageTextCaptureRequest
                    {
                        schema = "damage-text-capture-request-v1", runId = geometry.ToString(), clipName = asset.name,
                        sourceFingerprint = runner.sourceFingerprint,
                        plan = new DamageTextBenchmarkPlan { Geometry = geometry, Phase = DamageTextBenchmarkPhase.DrawOnly, SampleFrames = 3000, WarmupFrames = 0 }
                    };
                    string json = JsonUtility.ToJson(request);
                    runner.ApplyCaptureRequest(json); runner.autoExport = false; runner.Begin();
                    Assert.IsNull(runner.Failure); Assert.IsNotNull(runner.Session);
                    var report = runner.Session.Report;
                    Assert.AreEqual(geometry, report.plan.Geometry); Assert.AreEqual(request.runId, report.captureRunId);
                    Assert.AreEqual(64, report.captureRequestSHA256.Length); Assert.IsFalse(report.controlsVisible);
                    if (source == null) source = report.sourceFingerprint; else Assert.AreEqual(source, report.sourceFingerprint);
                    request.plan.SampleFrames = 2; Assert.AreEqual(3000, runner.plan.SampleFrames);
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(asset); }
#endif
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_130.md
    [UnityTest]
    [Category("UT_DAMAGE_130")]
    public IEnumerator CaptureRequest_RejectsWrongSourceUnknownClipShortPlansAndMidSessionChanges()
    {
#if UNITY_EDITOR
        var asset = ScriptableObject.CreateInstance<DamageTextClipAsset>();
        asset.name = "CaptureFixture"; asset.Duration = 3; asset.Events = new[] { Request(1, 0) };
        var go = new GameObject("Invalid capture request runner");
        try
        {
            var runner = go.AddComponent<DamageTextBenchmarkRunner>(); runner.captureClips = new[] { asset };
            runner.outputCamera = _camera; runner.atlas = _atlas; runner.styles = _styles; runner.configuration = _configuration;
            runner.meshShader = Shader.Find("UIData/DamageText"); runner.RefreshSourceMetadata();
            var request = new DamageTextCaptureRequest { schema = "damage-text-capture-request-v1", runId = "B1", clipName = asset.name,
                sourceFingerprint = new string('0', 64), plan = new DamageTextBenchmarkPlan { SampleFrames = 3000 } };
            Assert.Throws<ArgumentException>(() => runner.ApplyCaptureRequest(JsonUtility.ToJson(request)));
            request.sourceFingerprint = runner.sourceFingerprint; request.clipName = "not-embedded";
            Assert.Throws<ArgumentException>(() => runner.ApplyCaptureRequest(JsonUtility.ToJson(request)));
            request.clipName = asset.name; request.plan.SampleFrames = 2;
            Assert.Throws<ArgumentException>(() => runner.ApplyCaptureRequest(JsonUtility.ToJson(request)));
            Assert.IsNull(runner.clip); Assert.IsTrue(runner.showControls); Assert.IsNull(runner.Session);
            request.plan.SampleFrames = 3000;
            runner.ApplyCaptureRequest(JsonUtility.ToJson(request)); runner.autoExport = false; runner.Begin();
            Assert.IsNull(runner.Failure); Assert.IsNotNull(runner.Session);
            Assert.Throws<InvalidOperationException>(() => runner.ApplyCaptureRequest(JsonUtility.ToJson(request)));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(asset); }
#endif
        yield return null;
    }
}
