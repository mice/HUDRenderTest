using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class TestUIText
{
    private sealed class CaptureData : IUIData
    {
        public int TextureIndex { get; set; }
        public int FillVertexCount { get; private set; }
        public int LastFlags { get; private set; }
        public int TransformCount { get; private set; }
        public Matrix4x4 LastMatrix { get; private set; }

        public void FillVertex(VertexHelper toFill, int flags)
        {
            FillVertexCount++;
            LastFlags = flags;
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

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_001.md
    [Test]
    [Category("UT_UI_001")]
    public void DoGenerate_FillsMeshDataWithTextFlag()
    {
        var go = new GameObject("text", typeof(RectTransform), typeof(UIText));
        Font font = null;
        try
        {
            font = ResourceUtility.CreateAsciiFont(24);
            var text = go.GetComponent<UIText>();
            text.font = font;
            text.text = "HUD";
            text.fontSize = 24;
            var data = new CaptureData();

            text.DoGenerate(data);

            Assert.AreEqual(1, data.FillVertexCount);
            Assert.AreEqual(1, data.LastFlags);
            Assert.AreEqual(0, data.TransformCount);
            Assert.IsFalse(text.HasModifier);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(font);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/UI/UT_UI_002.md
    [Test]
    [Category("UT_UI_002")]
    public void DoGenerate_WithRootChild_AppliesRelativeTransform()
    {
        var root = new GameObject("root", typeof(RectTransform));
        var child = new GameObject("text", typeof(RectTransform), typeof(UIText));
        Font font = null;
        try
        {
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(5, 7, 0);

            font = ResourceUtility.CreateAsciiFont(24);
            var text = child.GetComponent<UIText>();
            text.font = font;
            text.text = "A";
            text.fontSize = 24;
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
            Object.DestroyImmediate(font);
        }
    }
}
