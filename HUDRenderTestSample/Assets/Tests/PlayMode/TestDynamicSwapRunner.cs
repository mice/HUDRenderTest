using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UIData;
using UnityEngine;
using UnityEngine.TestTools;

public class TestDynamicSwapRunner
{
    private sealed class StubDynamicSwapTarget : MonoBehaviour, IDynamicSwapTarget
    {
        private PerfProbe probe = new PerfProbe(8);

        public PerfProbe Probe => probe;
        public string CsvTag { get; set; } = "initial_dynamic_tag";
        public string LastCsvPath { get; private set; }
        public int CycleCount { get; private set; }

        public void CycleSprite()
        {
            CycleCount++;
            probe.Record(CycleCount, 1);
        }

        public void FlushProbe()
        {
            LastCsvPath = probe.Flush(CsvTag);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_006.md
    [UnityTest]
    [Category("UT_PERF_006")]
    public IEnumerator Runner_Cycles_Target_And_Flushes_Csv()
    {
        var go = new GameObject("DynamicSwapRunner");
        var target = go.AddComponent<StubDynamicSwapTarget>();
        var runner = go.AddComponent<DynamicSwapRunner>();
        runner.targetBehaviour = target;
        runner.autoStart = false;
        runner.warmupFrames = 1;
        runner.swapCount = 3;
        runner.framesBetweenSwaps = 1;
        runner.baseTag = "dynamic_case";
        runner.flushAfterCompletion = true;
        runner.restoreOriginalCsvTag = true;

        runner.StartSequence();

        yield return null;
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        Assert.AreEqual(3, target.CycleCount);
        Assert.IsFalse(string.IsNullOrEmpty(runner.LastCsvPath));
        Assert.IsTrue(File.Exists(runner.LastCsvPath));
        StringAssert.Contains("dynamic_case_3swaps", runner.LastCsvPath);
        Assert.AreEqual("initial_dynamic_tag", target.CsvTag);

        File.Delete(runner.LastCsvPath);
        Object.DestroyImmediate(go);
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_001.md
    [UnityTest]
    [Category("UT_PERF_001")]
    public IEnumerator BatchMergeBatcherRender_Button_Starts_DynamicSwapSequence()
    {
        var go = new GameObject("DynamicSwapRunnerButton");
        var target = go.AddComponent<StubDynamicSwapTarget>();
        var controller = go.AddComponent<BatchMergeBatcherRender>();
        var runner = go.AddComponent<DynamicSwapRunner>();
        runner.targetBehaviour = target;
        runner.autoStart = false;
        runner.warmupFrames = 0;
        runner.swapCount = 2;
        runner.framesBetweenSwaps = 1;
        runner.baseTag = "button_dynamic";
        runner.flushAfterCompletion = true;
        runner.restoreOriginalCsvTag = false;

        controller.StartDynamicSwapSequence();

        yield return null;
        yield return null;
        yield return null;
        yield return null;

        Assert.AreEqual(2, target.CycleCount);
        Assert.IsFalse(string.IsNullOrEmpty(runner.LastCsvPath));
        Assert.IsTrue(File.Exists(runner.LastCsvPath));

        File.Delete(runner.LastCsvPath);
        Object.DestroyImmediate(go);
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_005.md
    [Test]
    [Category("UT_PERF_005")]
    public void CycleSelection_UsesPrefabAndInstance_WhenConfigured()
    {
        var go = new GameObject("CycleSelection");
        try
        {
            var render = go.AddComponent<BatchMergeBatcherRender>();
            render.instantiateOwnersAtRuntime = true;
            render.runtimeInstancesPerPrefab = 3;
            render.cycleOwnerSelectionMode = BatchMergeBatcherRender.CycleOwnerSelectionMode.PrefabAndInstance;
            render.cycleOwnerPrefabIndex = 2;
            render.cycleOwnerInstanceIndex = 1;
            render.ownerPrefabs.Add(null);
            render.ownerPrefabs.Add(null);
            render.ownerPrefabs.Add(null);

            var method = typeof(BatchMergeBatcherRender).GetMethod(
                "TryResolveCycleHolderIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            object[] args = { 18, null, null };
            bool result = (bool)method.Invoke(render, args);

            Assert.IsTrue(result);
            Assert.AreEqual(7, args[1]);
            Assert.IsNull(args[2]);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_004.md
    [Test]
    [Category("UT_PERF_004")]
    public void CycleSelection_ReportsHelpfulError_WhenInstanceOutOfRange()
    {
        var go = new GameObject("CycleSelectionError");
        try
        {
            var render = go.AddComponent<BatchMergeBatcherRender>();
            render.instantiateOwnersAtRuntime = true;
            render.runtimeInstancesPerPrefab = 3;
            render.cycleOwnerSelectionMode = BatchMergeBatcherRender.CycleOwnerSelectionMode.PrefabAndInstance;
            render.cycleOwnerPrefabIndex = 2;
            render.cycleOwnerInstanceIndex = 5;
            render.ownerPrefabs.Add(null);
            render.ownerPrefabs.Add(null);
            render.ownerPrefabs.Add(null);

            var method = typeof(BatchMergeBatcherRender).GetMethod(
                "TryResolveCycleHolderIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            object[] args = { 18, null, null };
            bool result = (bool)method.Invoke(render, args);

            Assert.IsFalse(result);
            StringAssert.Contains("cycleOwnerInstanceIndex 5", (string)args[2]);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_003.md
    [Test]
    [Category("UT_PERF_003")]
    public void CycleDrawSelection_UsesTargetName_WhenConfigured()
    {
        var go = new GameObject("CycleDrawSelection");
        var ownerGo = new GameObject("HUDHero");
        try
        {
            var render = go.AddComponent<BatchMergeBatcherRender>();
            render.cycleDrawSelectionMode = BatchMergeBatcherRender.CycleDrawSelectionMode.TargetName;
            render.cycleDrawTargetName = "img_icon";

            var owner = ownerGo.AddComponent<UIPrefabOwner>();
            var icon = new GameObject("img_icon").transform;
            icon.SetParent(ownerGo.transform, false);
            var name = new GameObject("txt_name").transform;
            name.SetParent(ownerGo.transform, false);
            owner.targets = new List<Transform> { ownerGo.transform, icon, name };

            var method = typeof(BatchMergeBatcherRender).GetMethod(
                "TryResolveCycleDrawIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            object[] args = { owner, owner.targets.Count, null, null };
            bool result = (bool)method.Invoke(render, args);

            Assert.IsTrue(result);
            Assert.AreEqual(1, args[2]);
            Assert.IsNull(args[3]);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
            Object.DestroyImmediate(go);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_002.md
    [Test]
    [Category("UT_PERF_002")]
    public void CycleDrawSelection_ReportsAvailableTargets_WhenNameMissing()
    {
        var go = new GameObject("CycleDrawSelectionMissing");
        var ownerGo = new GameObject("HUDHero");
        try
        {
            var render = go.AddComponent<BatchMergeBatcherRender>();
            render.cycleDrawSelectionMode = BatchMergeBatcherRender.CycleDrawSelectionMode.TargetName;
            render.cycleDrawTargetName = "missing_icon";

            var owner = ownerGo.AddComponent<UIPrefabOwner>();
            var icon = new GameObject("img_icon").transform;
            icon.SetParent(ownerGo.transform, false);
            var level = new GameObject("txt_lvl").transform;
            level.SetParent(ownerGo.transform, false);
            owner.targets = new List<Transform> { ownerGo.transform, icon, level };

            var method = typeof(BatchMergeBatcherRender).GetMethod(
                "TryResolveCycleDrawIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            object[] args = { owner, owner.targets.Count, null, null };
            bool result = (bool)method.Invoke(render, args);

            Assert.IsFalse(result);
            StringAssert.Contains("missing_icon", (string)args[3]);
            StringAssert.Contains("1:img_icon", (string)args[3]);
        }
        finally
        {
            Object.DestroyImmediate(ownerGo);
            Object.DestroyImmediate(go);
        }
    }
}
