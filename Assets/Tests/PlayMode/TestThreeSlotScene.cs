using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class TestThreeSlotScene
{
    [UnityTest]
    public IEnumerator BatchMergeBatcherRender_Recreate_StaysSingleBatch_AndReportsNoOverCapacity()
    {
        var root = new GameObject("ThreeSlotRoot");
        var controllerGo = new GameObject("BatchMergeBatcher");
        var canvasGo = new GameObject("Canvas", typeof(Canvas));
        var uiRootGo = new GameObject("UIRoot", typeof(RectTransform));
        var sourceRootGo = new GameObject("RuntimeSourceRoot", typeof(RectTransform));
        var statusGo = new GameObject("StatusText", typeof(RectTransform), typeof(Text));
        var cameraGo = new GameObject("UICamera");

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
            controller.csvTag = "three_slot_test";

            controller.ownerPrefabs.Add(LoadOwnerPrefab("UIName"));
            controller.ownerPrefabs.Add(LoadOwnerPrefab("UITitle"));
            controller.ownerPrefabs.Add(LoadOwnerPrefab("UIBackground"));

            controller.ReCreate();
            yield return null;

            Assert.AreEqual(3, controller.UniqueImageTextureCount);
            Assert.AreEqual(controller.BatchTextureLimit, controller.UniqueImageTextureCount);
            Assert.AreEqual(1, controller.BatchCount);
            StringAssert.Contains("over:no", statusText.text);
            StringAssert.Contains("textures:3/3", statusText.text);

            controller.ReportBatchLayout();
            StringAssert.Contains("overCapacity=no", statusText.text);
        }
        finally
        {
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
