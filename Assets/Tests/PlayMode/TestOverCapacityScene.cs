using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class TestOverCapacityScene
{
    // TestRecord: Documentation~/Testing/Unit/Scenes/UT_SCENE_001.md
    [UnityTest]
    [Category("UT_SCENE_001")]
    public IEnumerator BatchMergeBatcherRender_Recreate_ReportsOverCapacity_AndSplitsBatches()
    {
        var warnings = new List<string>();
        void CaptureLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning)
                warnings.Add(condition);
        }

        var root = new GameObject("OverCapacityRoot");
        var controllerGo = new GameObject("BatchMergeBatcher");
        var canvasGo = new GameObject("Canvas", typeof(Canvas));
        var uiRootGo = new GameObject("UIRoot", typeof(RectTransform));
        var sourceRootGo = new GameObject("RuntimeSourceRoot", typeof(RectTransform));
        var statusGo = new GameObject("StatusText", typeof(RectTransform), typeof(Text));
        var cameraGo = new GameObject("UICamera");

        LogAssert.ignoreFailingMessages = true;
        Application.logMessageReceived += CaptureLog;

        try
        {
            controllerGo.transform.SetParent(root.transform, false);
            canvasGo.transform.SetParent(root.transform, false);
            cameraGo.transform.SetParent(root.transform, false);
            uiRootGo.transform.SetParent(canvasGo.transform, false);
            sourceRootGo.transform.SetParent(uiRootGo.transform, false);
            statusGo.transform.SetParent(canvasGo.transform, false);

            var camera = cameraGo.AddComponent<Camera>();
            var statusText = statusGo.GetComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Assert.IsNotNull(statusText.font);

            var controller = controllerGo.AddComponent<BatchMergeBatcherRender>();
            controller.font = statusText.font;
            controller.uiCamera = camera;
            controller.uiRoot = uiRootGo.transform;
            controller.statusText = statusText;
            controller.instantiateOwnersAtRuntime = true;
            controller.runtimeInstancesPerPrefab = 1;
            controller.runtimeSourceRoot = sourceRootGo.transform;
            controller.useSlim = true;
            controller.enable8TexSlots = false;
            controller.autoRecreateOnStart = false;
            controller.csvTag = "overcapacity_test";

            controller.ownerPrefabs.Add(LoadOwnerPrefab("UIName"));
            controller.ownerPrefabs.Add(LoadOwnerPrefab("UITitle"));
            controller.ownerPrefabs.Add(LoadOwnerPrefab("HUDHero"));
            controller.ownerPrefabs.Add(LoadOwnerPrefab("UIBackground"));
            controller.ownerPrefabs.Add(LoadOwnerPrefab("UIItem"));

            controller.ReCreate();
            yield return null;

            Assert.Greater(controller.UniqueImageTextureCount, controller.BatchTextureLimit);
            Assert.Greater(controller.BatchCount, 1);
            StringAssert.Contains("over:yes", statusText.text);
            StringAssert.Contains(
                $"textures:{controller.UniqueImageTextureCount}/{controller.BatchTextureLimit}",
                statusText.text);

            controller.ReportBatchLayout();
            StringAssert.Contains("Batch layout:", statusText.text);

            Assert.IsTrue(
                warnings.Exists(message => message.Contains("[MergeBatcher] split into")),
                "expected MergeBatcher split warning");
            Assert.IsTrue(
                warnings.Exists(message => message.Contains("[TextureSlotTable] texture count exceeds per-batch limit")),
                "expected TextureSlotTable over-capacity warning");
        }
        finally
        {
            Application.logMessageReceived -= CaptureLog;
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(root);
        }
    }

    private static UIPrefabOwner LoadOwnerPrefab(string prefabName)
    {
        var prefab = Resources.Load<UIPrefabOwner>($"UI/{prefabName}");
        Assert.IsNotNull(prefab, $"Expected Resources/UI/{prefabName}.prefab");
        return prefab;
    }
}
