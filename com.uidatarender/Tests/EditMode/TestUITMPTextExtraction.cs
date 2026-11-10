using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

public class TestUITMPTextExtraction
{
    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_007.md
    [Test]
    [TestCase(false)]
    [TestCase(true)]
    [Category("UT_UI_007")]
    public void Fill_WarmFixedAndChangingGeometry_HasNoAllocationEvents(bool slim)
    {
        var first = CreateInfo(2, 0);
        var second = CreateInfo(2, 10);
        using (IUIData data = slim ? (IUIData)new UIMeshDataX() : new UIMeshData())
        {
            for (int i = 0; i < 32; i++)
            {
                TMPMeshDataUtility.Fill(first, data);
                TMPMeshDataUtility.Fill(second, data);
            }
            int fixedEvents = MeasureEvents(() =>
            {
                for (int i = 0; i < 128; i++) TMPMeshDataUtility.Fill(first, data);
            });
            int changingEvents = MeasureEvents(() =>
            {
                for (int i = 0; i < 128; i++) TMPMeshDataUtility.Fill((i & 1) == 0 ? first : second, data);
            });
            TestContext.Out.WriteLine($"TMP extraction slim={slim} fixedEvents={fixedEvents} changingEvents={changingEvents}");
            Assert.AreEqual(0, fixedEvents, "Warm fixed TMP extraction allocated.");
            Assert.AreEqual(0, changingEvents, "Warm changing TMP extraction allocated.");
        }
    }

    [TearDown]
    public void TearDown() => TMPMeshDataUtility.ReleaseScratch();

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_008.md
    [Test]
    [TestCase(false)]
    [TestCase(true)]
    [Category("UT_UI_008")]
    public void Fill_GrowingShrinkingEmptyAndMultipleMaterials_MatchesSource(bool slim)
    {
        using (IUIData data = slim ? (IUIData)new UIMeshDataX() : new UIMeshData())
        {
            TMPMeshDataUtility.ReleaseScratch();
            foreach (int glyphs in new[] { 0, 1, 20, 2, 0, 3 })
            {
                var info = CreateInfo(glyphs, glyphs * 2);
                if (glyphs == 3)
                {
                    info.meshInfo = new[] { info.meshInfo[0], CreateInfo(1, 100).meshInfo[0] };
                    info.characterInfo[1].isVisible = false;
                    info.characterInfo[2].materialReferenceIndex = 1;
                    info.characterInfo[2].vertexIndex = 0;
                }
                data.TextureIndex = 5;
                TMPMeshDataUtility.Fill(info, data);
                AssertSource(info, data);
            }
            TMPMeshDataUtility.Fill(null, data);
            AssertSource(CreateInfo(0, 0), data);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_009.md
    [Test]
    [Category("UT_UI_009")]
    public void ReleaseScratch_RepeatedReleaseAndRecreation_PreservesOutput()
    {
        using (var data = new UIMeshData())
        {
            for (int i = 0; i < 3; i++)
            {
                TMPMeshDataUtility.ReleaseScratch();
                TMPMeshDataUtility.ReleaseScratch();
                var info = CreateInfo(i, 17);
                TMPMeshDataUtility.Fill(info, data);
                AssertSource(info, data);
            }
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_010.md
    [Test]
    [Category("UT_UI_010")]
    public void Fill_NestedCallbackReleaseAndException_PreservesOuterAndRecovers()
    {
        var outer = CreateInfo(3, 12);
        var inner = CreateInfo(1, 40);
        using (var data = new UIMeshData())
        using (var nested = new UIMeshDataX())
        {
            var callback = new CallbackData();
            VertexHelper active = null;
            callback.OnFill = helper =>
            {
                active = helper;
                TMPMeshDataUtility.Fill(inner, nested);
                AssertSource(inner, nested);
                TMPMeshDataUtility.ReleaseScratch();
                Assert.AreEqual(12, helper.currentVertCount);
                data.FillVertex(helper, 1);
                AssertSource(outer, data);
            };
            TMPMeshDataUtility.Fill(outer, callback);
            Assert.AreEqual(0, active.currentVertCount, "Callback must not retain current geometry.");
            callback.OnFill = helper =>
            {
                TMPMeshDataUtility.ReleaseScratch();
                TMPMeshDataUtility.Fill(inner, nested);
                throw new InvalidOperationException("callback failure");
            };
            Assert.Throws<InvalidOperationException>(() => TMPMeshDataUtility.Fill(outer, callback));
            TMPMeshDataUtility.Fill(inner, data);
            AssertSource(inner, data);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_011.md
    [Test]
    [TestCase(false)]
    [TestCase(true)]
    [Category("UT_UI_011")]
    public void DoGenerate_WarmFixedAndChangingText_HasNoAllocationEvents(bool slim)
    {
        var canvas = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
        var go = new GameObject("tmp", typeof(RectTransform), typeof(UITMPText));
        try
        {
            go.transform.SetParent(canvas.transform, false);
            var text = go.GetComponent<UITMPText>();
            text.font = TMP_Settings.defaultFontAsset;
            if (text.font == null) Assert.Ignore("Optional TMP default font unavailable.");
            text.fontSize = 24;
            text.rectTransform.sizeDelta = new Vector2(400, 80);
            var values = new[] { "HP 1000", "HP 1010" };
            using (IUIData data = slim ? (IUIData)new UIMeshDataX() : new UIMeshData())
            {
                for (int i = 0; i < 32; i++)
                {
                    text.text = values[i & 1];
                    text.DoGenerate(data);
                }
                int fixedEvents = MeasureEvents(() =>
                {
                    for (int i = 0; i < 128; i++) text.DoGenerate(data);
                });
                int changingEvents = MeasureEvents(() =>
                {
                    for (int i = 0; i < 128; i++)
                    {
                        text.text = values[i & 1];
                        text.DoGenerate(data);
                    }
                });
                TestContext.Out.WriteLine($"TMP DoGenerate slim={slim} fixedEvents={fixedEvents} changingEvents={changingEvents}");
                Assert.AreEqual(0, fixedEvents);
                Assert.AreEqual(0, changingEvents);
                AssertSource(text.textInfo, data);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(canvas);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_012.md
    [Test]
    [Category("UT_UI_012")]
    public void DoGenerate_StageTimings_ClearOnNullDisabledAndFailure()
    {
        var go = new GameObject("tmp", typeof(RectTransform), typeof(UITMPText));
        bool previous = BuildMeshDiagnostics.Enabled;
        try
        {
            var tmp = go.GetComponent<UITMPText>();
            tmp.font = TMP_Settings.defaultFontAsset;
            if (tmp.font == null) Assert.Ignore("Optional TMP default font unavailable.");
            using (var data = new UIMeshData())
            {
                BuildMeshDiagnostics.Enabled = true;
                tmp.text = "Changed"; tmp.DoGenerate(data);
                Assert.AreEqual(1, tmp.LastGenerationStageTimings.Samples);
                tmp.text = ""; tmp.DoGenerate(data);
                Assert.AreEqual(1, tmp.LastGenerationStageTimings.Samples);
                tmp.DoGenerate(null);
                Assert.AreEqual(0, tmp.LastGenerationStageTimings.Samples);
                tmp.DoGenerate(data);
                var failure = new CallbackData { OnFill = helper => throw new InvalidOperationException() };
                Assert.Throws<InvalidOperationException>(() => tmp.DoGenerate(failure));
                Assert.AreEqual(0, tmp.LastGenerationStageTimings.Samples);
                BuildMeshDiagnostics.Enabled = false;
                tmp.DoGenerate(data);
                Assert.AreEqual(0, tmp.LastGenerationStageTimings.Samples);
            }
        }
        finally
        {
            BuildMeshDiagnostics.Enabled = previous;
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void AssertSource(TMP_TextInfo info, IUIData data)
    {
        var vertices = new List<Vector3>();
        var uvs = new List<Vector4>();
        var colors = new List<Color32>();
        var indices = new List<int>();
        data.FillToDrawData(vertices, uvs, colors, indices, Vector3.zero);
        int target = 0;
        for (int i = 0; i < info.characterCount; i++)
        {
            var character = info.characterInfo[i];
            if (!character.isVisible) continue;
            var mesh = info.meshInfo[character.materialReferenceIndex];
            for (int v = 0; v < 4; v++)
            {
                int source = character.vertexIndex + v;
                Assert.AreEqual(mesh.vertices[source], vertices[target + v]);
                Assert.AreEqual(new Vector4(mesh.uvs0[source].x, mesh.uvs0[source].y, 0, 1), uvs[target + v]);
                Assert.AreEqual(mesh.colors32[source], colors[target + v]);
            }
            int index = target / 4 * 6;
            CollectionAssert.AreEqual(new[] { target, target + 1, target + 2, target + 2, target + 3, target }, indices.GetRange(index, 6));
            target += 4;
        }
        Assert.AreEqual(target, vertices.Count);
        Assert.AreEqual(target, uvs.Count);
        Assert.AreEqual(target, colors.Count);
        Assert.AreEqual(target / 4 * 6, indices.Count);
        Assert.AreEqual(0, data.TextureIndex);
    }

    private sealed class CallbackData : IUIData
    {
        public Action<VertexHelper> OnFill;
        public int TextureIndex { get; set; }
        public void FillVertex(VertexHelper helper, int flags)
        {
            Assert.AreEqual(1, flags);
            OnFill(helper);
        }
        public void UpdateTextureIndex(int index) => TextureIndex = index;
        public void TransformVertex(Matrix4x4 matrix) { }
        public void FillToTriangleData(List<int> indices, Vector3 position) { }
        public void FillToDrawData(List<Vector3> vertices, List<Vector4> uvs, List<Color32> colors, List<int> indices, Vector3 position) { }
        public void FillWithMatrix(List<Vector3> vertices, List<Vector4> uvs, List<Color32> colors, List<int> indices, Matrix4x4 matrix) { }
        public void WriteSimpleQuad(Vector4 drawing, Vector4 uv, Color32 color) { }
        public void Dispose() { }
    }

    private static int MeasureEvents(Action operation)
    {
        using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 4096,
            ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
        {
            GC.KeepAlive(new byte[4096]);
            recorder.Stop();
            Assert.IsTrue(recorder.Valid, "GC.Alloc recorder unavailable.");
            Assert.Greater(recorder.Count, 0, "Positive allocation control was not observed.");
            recorder.Reset();
            recorder.Start();
            operation();
            recorder.Stop();
            Assert.Less(recorder.Count, recorder.Capacity, "Recorder saturated.");
            return recorder.Count;
        }
    }

    private static TMP_TextInfo CreateInfo(int glyphs, float offset)
    {
        var info = new TMP_TextInfo
        {
            characterCount = glyphs,
            characterInfo = new TMP_CharacterInfo[glyphs],
            meshInfo = new TMP_MeshInfo[1]
        };
        var mesh = new TMP_MeshInfo
        {
            vertices = new Vector3[glyphs * 4],
            uvs0 = new Vector2[glyphs * 4],
            colors32 = new Color32[glyphs * 4]
        };
        for (int i = 0; i < glyphs; i++)
        {
            info.characterInfo[i] = new TMP_CharacterInfo { isVisible = true, vertexIndex = i * 4, materialReferenceIndex = 0 };
            for (int v = 0; v < 4; v++)
            {
                int index = i * 4 + v;
                mesh.vertices[index] = new Vector3(offset + index, v, i);
                mesh.uvs0[index] = new Vector2(index / 16f, v / 4f);
                mesh.colors32[index] = new Color32((byte)(index * 7), 120, 230, 200);
            }
        }
        info.meshInfo[0] = mesh;
        return info;
    }
}
