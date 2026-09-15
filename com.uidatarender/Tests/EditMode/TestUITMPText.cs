using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TestUITMPText
{
    private sealed class CaptureData : IUIData
    {
        public int TextureIndex { get; set; }
        public int FillVertexCount { get; private set; }
        public int LastFlags { get; private set; }
        public int VertexCount { get; private set; }
        public int IndexCount { get; private set; }
        public int TransformCount { get; private set; }
        public Matrix4x4 LastMatrix { get; private set; }

        public void FillVertex(VertexHelper toFill, int flags)
        {
            FillVertexCount++;
            LastFlags = flags;
            VertexCount = toFill.currentVertCount;
            IndexCount = toFill.currentIndexCount;
        }

        public void TransformVertex(Matrix4x4 mtx)
        {
            TransformCount++;
            LastMatrix = mtx;
        }

        public void UpdateTextureIndex(int index) => TextureIndex = index;
        public void FillToTriangleData(List<int> triangles_, Vector3 localPosition) { }
        public void FillToDrawData(List<Vector3> vertList, List<Vector4> uvs, List<Color32> colors, List<int> triangles, Vector3 localPosition) { }
        public void FillWithMatrix(List<Vector3> vertList, List<Vector4> uvs, List<Color32> colors, List<int> triangles, Matrix4x4 mtx) { }
        public void Dispose() { }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_003.md
    [Test]
    [Category("UT_UI_003")]
    public void DoGenerate_FillsMeshDataFromTMP()
    {
        var canvas = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
        var go = new GameObject("tmp", typeof(RectTransform), typeof(UITMPText));
        try
        {
            go.transform.SetParent(canvas.transform, false);
            var text = go.GetComponent<UITMPText>();
            Configure(text, "HUD");
            var data = new CaptureData();

            text.DoGenerate(data);

            Assert.AreEqual(1, data.FillVertexCount);
            Assert.AreEqual(1, data.LastFlags);
            Assert.Greater(data.VertexCount, 0);
            Assert.Greater(data.IndexCount, 0);
            Assert.AreEqual(0, data.TextureIndex);
            Assert.NotNull(text.TextTexture);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(canvas);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_004.md
    [Test]
    [Category("UT_UI_004")]
    public void DoGenerate_WithRootChild_AppliesRelativeTransform()
    {
        var root = new GameObject("root", typeof(RectTransform));
        var child = new GameObject("tmp", typeof(RectTransform), typeof(UITMPText));
        try
        {
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(5, 7, 0);

            var text = child.GetComponent<UITMPText>();
            Configure(text, "A");
            var data = new CaptureData();

            text.DoGenerate(data, root.transform);

            Assert.AreEqual(1, data.FillVertexCount);
            Assert.AreEqual(1, data.TransformCount);
            Assert.That(data.LastMatrix.m03, Is.EqualTo(5).Within(0.0001f));
            Assert.That(data.LastMatrix.m13, Is.EqualTo(7).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(child);
            Object.DestroyImmediate(root);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_005.md
    [Test]
    [Category("UT_UI_005")]
    public void TextProperty_UpdatesTMPText()
    {
        var canvas = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
        var go = new GameObject("tmp", typeof(RectTransform), typeof(UITMPText));
        try
        {
            go.transform.SetParent(canvas.transform, false);
            var text = go.GetComponent<UITMPText>();
            Configure(text, "old");

            ((IUITextDrawTarget)text).Text = "new";

            Assert.AreEqual("new", text.text);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(canvas);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_006.md
    [Test]
    [Category("UT_UI_006")]
    public void BindTmpSdf_CopiesAtlasAndOutlineProperties()
    {
        var shader = Shader.Find("Hidden/UIE-TMP-SDF");
        Assert.NotNull(shader, "Hidden/UIE-TMP-SDF shader must be available.");

        var target = new Material(shader);
        var source = new Material(Shader.Find("TextMeshPro/Distance Field"));
        var texture = new Texture2D(4, 4);
        try
        {
            source.SetColor("_FaceColor", Color.red);
            source.SetColor("_OutlineColor", Color.black);
            source.SetFloat("_OutlineWidth", 0.2f);

            MaterialBinder.BindTmpSdf(target, texture, source);

            Assert.AreSame(texture, target.GetTexture("_MainTex"));
            Assert.AreEqual(Color.red, target.GetColor("_FaceColor"));
            Assert.AreEqual(Color.black, target.GetColor("_OutlineColor"));
            Assert.That(target.GetFloat("_OutlineWidth"), Is.EqualTo(0.2f).Within(0.0001f));
        }
        finally
        {
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(texture);
        }
    }

    private static void Configure(TMP_Text text, string value)
    {
        text.font = TMP_Settings.defaultFontAsset;
        if (text.font == null)
            Assert.Ignore("TMP default font asset is not available.");

        text.text = value;
        text.fontSize = 24;
        text.color = Color.white;
        text.rectTransform.sizeDelta = new Vector2(200, 80);
    }
}
