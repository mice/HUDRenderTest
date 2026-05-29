using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UIData;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class TestPerfProbeRuntime
{
    private sealed class StubMesh : IUIData
    {
        public int TextureIndex { get; set; } = 1;
        public void UpdateTextureIndex(int index) => TextureIndex = index;
        public void FillVertex(VertexHelper toFill, int flags) { }
        public void TransformVertex(Matrix4x4 mtx) { }
        public void FillToTriangleData(List<int> triangles, Vector3 localPosition) { }

        public void FillToDrawData(
            List<Vector3> vertices,
            List<Vector4> uvs,
            List<Color32> colors,
            List<int> triangles,
            Vector3 localPosition)
        {
            int offset = vertices.Count;
            vertices.Add(localPosition + new Vector3(0, 0));
            vertices.Add(localPosition + new Vector3(0, 1));
            vertices.Add(localPosition + new Vector3(1, 1));
            vertices.Add(localPosition + new Vector3(1, 0));
            for (int i = 0; i < 4; i++)
            {
                uvs.Add(new Vector4(0, 0, TextureIndex, 0));
                colors.Add(Color.white);
            }
            triangles.Add(offset + 0);
            triangles.Add(offset + 1);
            triangles.Add(offset + 2);
            triangles.Add(offset + 2);
            triangles.Add(offset + 3);
            triangles.Add(offset + 0);
        }

        public void FillWithMatrix(List<Vector3> vertices, List<Vector4> uvs, List<Color32> colors, List<int> triangles, Matrix4x4 mtx) { }
        public void Dispose() { }
    }

    private sealed class StubHolder : IUIPrefabHolder
    {
        private readonly IUIData[] meshes = { new StubMesh() };

        public UIPrefabOwner Target => null;
        public Vector3 Position => Vector3.zero;
        public UIPrefabRegistration wrapper => null;
        public IList<IUIData> UIMeshDatas => meshes;
        public void SetTarget(UIPrefabOwner target) { }
        public void SetWrapper(UIPrefabRegistration wrapper) { }
        public void BuildMesh(IUIDrawTarget[] draws) { }

        public void Fill(List<Vector3> vertices, List<Vector4> uvs, List<Color32> colors, List<int> triangles, Vector3 localPosition)
        {
            meshes[0].FillToDrawData(vertices, uvs, colors, triangles, localPosition);
        }

        public void Fill(List<int> triangles, Vector3 localPosition) { }
    }

    // TestRecord: Documentation~/Testing/Unit/Diagnostics/UT_DIAG_009.md
    [UnityTest]
    [Category("UT_DIAG_009")]
    public IEnumerator ProbeFlush_AfterRuntimeRebuild_WritesCsv()
    {
        var probe = new PerfProbe(windowSize: 4);
        var renderer = new HolderBatchRenderer(maxSlots: 3, probe: probe);
        renderer.Rebuild(
            new IUIPrefabHolder[] { new StubHolder() },
            null,
            () => new Material(Shader.Find("Sprites/Default")),
            (_, _) => { });

        yield return null;

        string path = probe.Flush("playmode_runtime");
        Assert.IsTrue(File.Exists(path));
        string text = File.ReadAllText(path);
        StringAssert.Contains("fill_ms", text);
        StringAssert.Contains("draw_calls", text);
        Assert.Greater(probe.Count, 0);

        File.Delete(path);
        renderer.Dispose();
    }
}
