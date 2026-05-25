using System.Collections;
using System.IO;
using NUnit.Framework;
using UIData;
using UnityEngine;
using UnityEngine.TestTools;

public class TestPerfBaselineRunner
{
    private sealed class StubProbeSource : MonoBehaviour, IPerfProbeSource
    {
        public PerfProbe Probe { get; } = new PerfProbe(4);
    }

    [UnityTest]
    public IEnumerator Runner_Flushes_SourceProbe_AfterWindow()
    {
        var go = new GameObject("PerfRunner");
        var source = go.AddComponent<StubProbeSource>();
        source.Probe.Record(1.25f, 2);

        var runner = go.AddComponent<PerfBaselineRunner>();
        runner.probeSourceBehaviour = source;
        runner.warmupFrames = 0;
        runner.sampleFrames = 1;
        runner.deviceTag = "runner_source";
        runner.autoStart = false;
        runner.StartSampling();

        yield return null;
        yield return null;

        Assert.IsFalse(string.IsNullOrEmpty(runner.LastCsvPath));
        Assert.IsTrue(File.Exists(runner.LastCsvPath));
        string text = File.ReadAllText(runner.LastCsvPath);
        StringAssert.Contains("fill_ms", text);

        File.Delete(runner.LastCsvPath);
        Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator Runner_Fallback_Records_FrameTime_When_SourceMissing()
    {
        var go = new GameObject("PerfRunnerFallback");
        var runner = go.AddComponent<PerfBaselineRunner>();
        runner.warmupFrames = 0;
        runner.sampleFrames = 2;
        runner.deviceTag = "runner_fallback";
        runner.autoStart = false;
        runner.recordFrameTimeWhenSourceMissing = true;
        runner.StartSampling();

        yield return null;
        yield return null;
        yield return null;

        Assert.IsNotNull(runner.Probe);
        Assert.Greater(runner.Probe.Count, 0);
        Assert.IsFalse(string.IsNullOrEmpty(runner.LastCsvPath));
        Assert.IsTrue(File.Exists(runner.LastCsvPath));

        File.Delete(runner.LastCsvPath);
        Object.DestroyImmediate(go);
    }
}
