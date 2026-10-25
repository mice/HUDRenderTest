using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public class TestHolderBucketRendering
{
    private struct Snapshot { public Color32 Color; public Vector3 Position; public int Slot; }
    private sealed class Holder : IUIPrefabHolder, IDisposable
    {
        private readonly IUIData[] _data = { new UIMeshDataX() };
        public UIPrefabOwner Target => null;
        public Vector3 Position => Vector3.zero;
        public UIPrefabRegistration wrapper => null;
        public IList<IUIData> UIMeshDatas => _data;
        public void SetTarget(UIPrefabOwner target) { }
        public void SetWrapper(UIPrefabRegistration registration) { }
        public void BuildMesh(IUIDrawTarget[] draws) { }
        public void Fill(List<Vector3> v, List<Vector4> uv, List<Color32> c, List<int> t, Vector3 position) => _data[0].FillToDrawData(v, uv, c, t, position);
        public void Fill(List<int> t, Vector3 position) => _data[0].FillToTriangleData(t, position);
        public void Dispose() => _data[0].Dispose();
    }
    private sealed class Applier : IHolderStateApplier<Snapshot>
    {
        public void BeginBatch() { }
        public Vector3 Apply(IUIPrefabHolder holder, in Snapshot state)
        {
            holder.UIMeshDatas[0].TextureIndex = state.Slot;
            holder.UIMeshDatas[0].WriteSimpleQuad(new Vector4(-.8f, -.8f, .8f, .8f), new Vector4(0, 0, 1, 1), state.Color);
            return state.Position;
        }
        public void EndBatch() { }
    }

    // TestRecord: Documentation~/Testing/Unit/Performance/UT_PERF_019.md
    [UnityTest]
    [Category("UT_PERF_019")]
    public IEnumerator BucketBoundary_PreservesTransparentPixelsAndCameraCommandLifetime()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Actual graphics device required for transparent pixel acceptance.");
        if (GraphicsSettings.currentRenderPipeline != null) Assert.Ignore("Built-in camera contract only.");
        var cameraObject = new GameObject("bucket pixel camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var holders = new Holder[129];
        var states = new Snapshot[129];
        var previousTarget = RenderTexture.active;
        try
        {
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 0;
            target.Create(); camera.targetTexture = target;
            for (int i = 0; i < holders.Length; i++) { holders[i] = new Holder(); states[i].Color = new Color32(0, 0, 0, 0); }
            states[127].Color = new Color32(255, 0, 0, 128);
            states[128].Color = new Color32(0, 0, 255, 128);
            foreach (bool textureSplit in new[] { false, true })
            {
            for (int i = 0; i < states.Length; i++) states[i].Slot = textureSplit ? (i == 127 ? 2 : 1) : 0;
            Color32[] reference = null;
            foreach (var strategy in new[] { HolderBucketStrategy.SingleBucket, HolderBucketStrategy.SameFrame, HolderBucketStrategy.Rotating })
            {
                states[127].Position = states[128].Position = Vector3.zero;
                using (var scheduler = new HolderBucketScheduler<Snapshot>(1, new Applier(),
                    () => new Material(Shader.Find("Hidden/UIE-AtlasBlit")), (material, batch) =>
                    {
                        material.SetTexture("_MainTex0", Texture2D.whiteTexture);
                        foreach (var slot in batch.BuildLocalSlotMap())
                            material.SetTexture("_MainTex" + slot.Value, slot.Key == 2 ? Texture2D.grayTexture : Texture2D.whiteTexture);
                    }, () => 0, strategy))
                {
                    if (textureSplit)
                    {
                        int batches = strategy == HolderBucketStrategy.SingleBucket ? 3 : 2;
                        LogAssert.Expect(LogType.Warning, new Regex("split into " + batches + " batches"));
                        LogAssert.Expect(LogType.Warning, new Regex("split into " + batches + " batches"));
                    }
                    Assert.AreEqual(HolderBucketStatus.Ready, scheduler.ReplaceMembers(holders, states, 0).Status);
                    using (var pass = new HolderBucketCameraPass(camera, scheduler))
                    {
                        Assert.AreEqual(1, camera.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Length);
                        // Change both positions; overlap remains, while mesh bounds change across the bucket boundary.
                        states[127].Position = new Vector3(.05f, 0, -.2f);
                        states[128].Position = new Vector3(-.05f, 0, .2f);
                        scheduler.Submit(scheduler.GetHandle(127), states[127], 1);
                        scheduler.Submit(scheduler.GetHandle(128), states[128], 1);
                        scheduler.Tick(1); pass.Refresh(Matrix4x4.identity);
                        Assert.AreEqual(textureSplit ? 3 : strategy == HolderBucketStrategy.SingleBucket ? 1 : 2, pass.LastDrawCount);
                        camera.Render();
                        scheduler.Tick(2); // Camera pre-cull must refresh a newly published front revision.
                        Assert.AreEqual(1, camera.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Length);
                        camera.Render();
                        RenderTexture.active = target;
                        readback.ReadPixels(new Rect(0, 0, 32, 32), 0, 0); readback.Apply();
                        var pixels = readback.GetPixels32();
                        var center = pixels[16 * 32 + 16];
                        Assert.Greater(center.r, 10, "Reference must actually draw red behind blue.");
                        Assert.Greater(center.b, center.r, "Last holder must cover the preceding holder.");
                        if (reference == null) reference = pixels;
                        else CollectionAssert.AreEqual(reference, pixels, "Splitting and rotating must preserve the final transparent image.");
                    }
                    Assert.AreEqual(0, camera.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Length);
                }
                yield return null;
            }
            }
        }
        finally
        {
            RenderTexture.active = previousTarget;
            camera.targetTexture = null;
            foreach (var holder in holders) holder?.Dispose();
            target.Release();
            UnityEngine.Object.DestroyImmediate(readback);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
