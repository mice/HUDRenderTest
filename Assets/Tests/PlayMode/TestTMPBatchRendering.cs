using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

public class TestTMPBatchRendering
{
    // TestRecord: Documentation~/Testing/Unit/Scenes/UT_SCENE_004.md
    [UnityTest]
    [Category("UT_SCENE_004")]
    public IEnumerator TMPText_Rebuild_ProducesBatchMesh()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        var ownerGo = new GameObject("Owner", typeof(RectTransform), typeof(UIPrefabOwner));
        var tmpGo = new GameObject("TMP", typeof(RectTransform), typeof(UITMPText));
        HolderBatchRenderer renderer = null;
        Material material = null;

        try
        {
            var owner = ownerGo.GetComponent<UIPrefabOwner>();
            ownerGo.transform.SetParent(canvasGo.transform, false);
            tmpGo.transform.SetParent(ownerGo.transform, false);

            var tmp = tmpGo.GetComponent<UITMPText>();
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null)
                Assert.Ignore("TMP default font asset is not available.");

            tmp.text = "HUD";
            tmp.fontSize = 24;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(200, 80);
            owner.targets = new List<Transform> { tmpGo.transform };

            var holder = new DataPrefabHolder<UIMeshData>();
            holder.SetTarget(owner);
            var manager = UIPrefabManager.Instance;
            manager.Register(holder);
            var registration = manager.Generate(holder);
            yield return null;

            renderer = new HolderBatchRenderer(3);
            renderer.Rebuild(
                new[] { holder },
                new Dictionary<IUIPrefabHolder, Vector3> { [holder] = Vector3.zero },
                () =>
                {
                    material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
                    manager.UpdateTextTexture(material, registration);
                    return material;
                },
                manager.UpdateTexture);

            var mesh = renderer.GetBatchMesh(0);
            Assert.NotNull(mesh);
            Assert.Greater(mesh.vertexCount, 0);
            Assert.Greater(renderer.BatchCount, 0);
        }
        finally
        {
            renderer?.Dispose();
            if (material != null)
                Object.DestroyImmediate(material);
            Object.DestroyImmediate(tmpGo);
            Object.DestroyImmediate(ownerGo);
            Object.DestroyImmediate(canvasGo);
        }
    }
}
