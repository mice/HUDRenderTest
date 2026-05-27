using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UIData;
using UnityEngine;
using UnityEngine.TestTools;

public class TestEightSlotPerfRunner
{
    private sealed class StubEightSlotPerfTarget : MonoBehaviour, IEightSlotPerfTarget
    {
        private PerfProbe probe = new PerfProbe(8);

        public readonly List<bool> RecreateEnable8SlotHistory = new List<bool>();
        public readonly List<string> RecreateTagHistory = new List<string>();
        public readonly List<bool> RecreateRebuildEveryFrameHistory = new List<bool>();

        public PerfProbe Probe => probe;
        public bool Enable8TexSlots { get; set; }
        public bool RebuildEveryFrame { get; set; }
        public string CsvTag { get; set; } = "initial_tag";
        public string LastCsvPath { get; private set; }

        public void ReCreate()
        {
            probe = new PerfProbe(8);
            probe.Record(Enable8TexSlots ? 8f : 3f, Enable8TexSlots ? 1 : 2);
            LastCsvPath = null;
            RecreateEnable8SlotHistory.Add(Enable8TexSlots);
            RecreateTagHistory.Add(CsvTag);
            RecreateRebuildEveryFrameHistory.Add(RebuildEveryFrame);
        }

        public void FlushProbe()
        {
            LastCsvPath = probe.Flush(CsvTag);
        }
    }

    [UnityTest]
    public IEnumerator Runner_Flushes_ThreeSlot_And_EightSlot_Csvs()
    {
        var go = new GameObject("EightSlotPerfRunner");
        var target = go.AddComponent<StubEightSlotPerfTarget>();
        var runner = go.AddComponent<EightSlotPerfRunner>();
        runner.targetBehaviour = target;
        runner.autoStart = false;
        runner.warmupFrames = 1;
        runner.sampleFrames = 1;
        runner.baseTag = "compare_case";
        runner.forceRebuildEveryFrame = true;
        runner.restoreOriginalStateOnComplete = false;

        runner.StartComparison();

        yield return null;
        yield return null;
        yield return null;
        yield return null;

        Assert.IsFalse(string.IsNullOrEmpty(runner.ThreeSlotCsvPath));
        Assert.IsFalse(string.IsNullOrEmpty(runner.EightSlotCsvPath));
        Assert.IsTrue(File.Exists(runner.ThreeSlotCsvPath));
        Assert.IsTrue(File.Exists(runner.EightSlotCsvPath));
        StringAssert.Contains("compare_case_3slot", runner.ThreeSlotCsvPath);
        StringAssert.Contains("compare_case_8slot", runner.EightSlotCsvPath);

        CollectionAssert.Contains(target.RecreateEnable8SlotHistory, false);
        CollectionAssert.Contains(target.RecreateEnable8SlotHistory, true);
        CollectionAssert.Contains(target.RecreateTagHistory, "compare_case_3slot");
        CollectionAssert.Contains(target.RecreateTagHistory, "compare_case_8slot");
        CollectionAssert.Contains(target.RecreateRebuildEveryFrameHistory, true);

        File.Delete(runner.ThreeSlotCsvPath);
        File.Delete(runner.EightSlotCsvPath);
        Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator Runner_Restores_Target_State_On_Completion()
    {
        var go = new GameObject("EightSlotPerfRunnerRestore");
        var target = go.AddComponent<StubEightSlotPerfTarget>();
        target.Enable8TexSlots = true;
        target.RebuildEveryFrame = false;
        target.CsvTag = "original_tag";

        var runner = go.AddComponent<EightSlotPerfRunner>();
        runner.targetBehaviour = target;
        runner.autoStart = false;
        runner.warmupFrames = 0;
        runner.sampleFrames = 1;
        runner.baseTag = "restore_case";
        runner.forceRebuildEveryFrame = true;
        runner.restoreOriginalStateOnComplete = true;

        runner.StartComparison();

        yield return null;
        yield return null;
        yield return null;
        yield return null;

        Assert.IsTrue(target.Enable8TexSlots);
        Assert.IsFalse(target.RebuildEveryFrame);
        Assert.AreEqual("original_tag", target.CsvTag);

        if (!string.IsNullOrEmpty(runner.ThreeSlotCsvPath) && File.Exists(runner.ThreeSlotCsvPath))
            File.Delete(runner.ThreeSlotCsvPath);
        if (!string.IsNullOrEmpty(runner.EightSlotCsvPath) && File.Exists(runner.EightSlotCsvPath))
            File.Delete(runner.EightSlotCsvPath);

        Object.DestroyImmediate(go);
    }
}
