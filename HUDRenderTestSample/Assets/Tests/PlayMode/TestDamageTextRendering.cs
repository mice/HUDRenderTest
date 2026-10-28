using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public sealed class TestDamageTextRendering
{
    private Camera _camera;
    private RenderTexture _target;
    private DamageTextGlyphAtlas _atlas;
    private DamageTextStyles _styles;
    private DamageTextRenderer _renderer;
    private DamageTextConfiguration _configuration;
    private Shader _shader;

    [SetUp]
    public void SetUp()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Actual graphics device required.");
        if (GraphicsSettings.currentRenderPipeline != null) Assert.Ignore("Built-in camera contract.");
        _camera = new GameObject("Damage text test camera", typeof(Camera)).GetComponent<Camera>();
        _camera.enabled = false; _camera.orthographic = true; _camera.orthographicSize = 4;
        _camera.transform.position = new Vector3(0, 0, -10);
        _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = Color.black; _camera.cullingMask = 0;
        _target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32); _target.Create(); _camera.targetTexture = _target;
        _atlas = ScriptableObject.CreateInstance<DamageTextGlyphAtlas>();
        var digits = new DamageTextGlyph[10];
        for (int i = 0; i < digits.Length; i++) digits[i] = new DamageTextGlyph
        { Available = true, Size = new Vector2(8, 12), Advance = 10, Uv = new Rect(0, 0, 1, 1) };
        _atlas.Initialize(Texture2D.whiteTexture, 24, digits, "pixel-fixture-v1", "white-pixel-fixture-v1");
        _styles = ScriptableObject.CreateInstance<DamageTextStyles>();
        _configuration = new DamageTextConfiguration { MaximumBuckets = 2, MaximumPendingEvents = 256 };
        _shader = Shader.Find("UIData/DamageText"); Assert.IsNotNull(_shader); Assert.IsTrue(_shader.isSupported);
        _renderer = new DamageTextRenderer(); Prepare();
    }
    [TearDown]
    public void TearDown()
    {
        _renderer?.Dispose();
        if (_camera != null) _camera.targetTexture = null;
        if (_target != null) { _target.Release(); UnityEngine.Object.DestroyImmediate(_target); }
        if (_camera != null) UnityEngine.Object.DestroyImmediate(_camera.gameObject);
        if (_atlas != null) UnityEngine.Object.DestroyImmediate(_atlas);
        if (_styles != null) UnityEngine.Object.DestroyImmediate(_styles);
    }
    private void Prepare() { _renderer.Prepare(_configuration, _atlas, _styles, _shader); _renderer.BindCamera(_camera); }
    private static DamageTextRequest Request(uint id, DamageTextKind kind = DamageTextKind.Normal, Vector3 position = default)
        => new DamageTextRequest { EventId = id, Amount = 10, Kind = kind, WorldPosition = position, RandomSeed = id };
    private Color32[] Render(string evidenceName = null)
    {
        _camera.Render(); var old = RenderTexture.active;
        var texture = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = _target; texture.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0); texture.Apply();
            if (evidenceName != null)
            {
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../TestResults/DamageText/Images"));
                Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, evidenceName + ".png"), texture.EncodeToPNG());
            }
            return texture.GetPixels32();
        }
        finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(texture); }
    }
    private static int Lit(Color32[] pixels)
    {
        int count = 0; foreach (var pixel in pixels) if (pixel.r > 5 || pixel.g > 5 || pixel.b > 5) count++; return count;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_101.md
    [UnityTest]
    [Category("UT_DAMAGE_101")]
    public IEnumerator CrossBucketTransparentOrder_MatchesSingleBucketPixels()
    {
        var normal = _styles.Normal; normal.Color = new Color32(255, 0, 0, 128); normal.PopAmplitude = 0; _styles.Normal = normal;
        var healing = normal; healing.Color = new Color32(0, 0, 255, 128); _styles.Healing = healing;
        Color32[] reference = null;
        foreach (int capacity in new[] { 256, 128 })
        {
            _configuration.BucketCapacity = capacity; Prepare();
            for (uint id = 1; id <= 129; id++)
                _renderer.Spawn(Request(id, id == 129 ? DamageTextKind.Healing : DamageTextKind.Normal,
                    id < 128 ? new Vector3(1000, 0, 0) : Vector3.zero));
            _renderer.Tick(0, 0);
            Assert.AreEqual(2, _camera.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Length);
            var pixels = Render(capacity == 128 ? "cross-bucket-order" : "single-bucket-reference");
            var center = pixels[64 * 128 + 68]; Assert.Greater(center.r, 10); Assert.Greater(center.b, center.r);
            if (reference == null) reference = pixels; else CollectionAssert.AreEqual(reference, pixels);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_102.md
    [UnityTest]
    [Category("UT_DAMAGE_102")]
    public IEnumerator CachedCommands_AnimateAndPauseWithoutGeometryUpdates()
    {
        _renderer.Spawn(Request(1)); _renderer.Tick(0, 0);
        var initial = Render("animation-start"); long revision = _renderer.DrawRevision;
        _renderer.Tick(.25f, 1); var moved = Render("animation-quarter");
        Assert.AreEqual(revision, _renderer.DrawRevision); Assert.AreEqual(1, _renderer.Statistics.VertexUploads);
        CollectionAssert.AreNotEqual(initial, moved); Assert.Greater(Lit(moved), 10);
        _renderer.Tick(.25f, 2); CollectionAssert.AreEqual(moved, Render());
        _renderer.Tick(_styles.Normal.Lifetime, 3);
        Assert.AreEqual(0, Lit(Render("exact-expiry"))); Assert.AreEqual(0, _renderer.CameraPass.LastDrawCount);
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_103.md
    [UnityTest]
    [Category("UT_DAMAGE_103")]
    public IEnumerator FarAnchorCameraAndViewport_KeepPixelSizeAndClipNearPlane()
    {
        _renderer.Spawn(Request(1, DamageTextKind.Normal, new Vector3(1000, 0, 0))); _renderer.Tick(0, 0);
        Assert.AreEqual(0, Lit(Render()));
        _camera.transform.position = new Vector3(1000, 0, -10);
        var centered = Render("far-anchor"); Assert.Greater(Lit(centered), 100);
        _camera.rect = new Rect(.25f, .25f, .5f, .5f);
        Assert.That(Lit(Render("viewport-half")), Is.EqualTo(Lit(centered)).Within(40));
        _camera.rect = new Rect(0, 0, 1, 1);
        _camera.transform.position = new Vector3(995.8f, 0, -10);
        int edgePixels = Lit(Render("viewport-edge"));
        Assert.Greater(edgePixels, 0, "A partially visible string must not be culled using its local glyph bounds.");
        Assert.Less(edgePixels, Lit(centered));
        _camera.transform.position = new Vector3(1000, 0, -.1f);
        Assert.AreEqual(0, Lit(Render("near-plane")));
        Assert.AreEqual(1, _renderer.Statistics.NearPlaneClippedEvents);
        _camera.transform.position = new Vector3(1000, 0, 1);
        Assert.AreEqual(0, Lit(Render("behind-camera")));
        Assert.AreEqual(1, _renderer.Statistics.VertexUploads);
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_104.md
    [UnityTest]
    [Category("UT_DAMAGE_104")]
    public IEnumerator PlaybackSeekAndPause_ReproduceTheSameImage()
    {
        _renderer.Clear();
        using (var playback = new DamageTextPlayback())
        {
            var a = Request(1); var b = Request(2, DamageTextKind.Healing, new Vector3(1, 0, 0)); b.SpawnTime = .1f;
            var clip = new DamageTextClip(new[] { a, b }, 3, "visual-clip-v1", 42);
            playback.Prepare(clip, _configuration, _atlas, _styles, _shader, _camera);
            playback.AdvanceTo(.25f); var reference = Render("normal-replay-checkpoint");
            Assert.Greater(Lit(reference), 50);
            playback.Pause(); playback.Advance(1); CollectionAssert.AreEqual(reference, Render());
            playback.AdvanceTo(1.5); playback.Seek(.25f);
            CollectionAssert.AreEqual(reference, Render("restored-checkpoint"));
        }
        Assert.AreEqual(0, _camera.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Length);
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_105.md
    [UnityTest]
    [Category("UT_DAMAGE_105")]
    public IEnumerator AllModesAndRepeatedClear_RenderAndReleaseOwnedResources()
    {
        for (int iteration = 0; iteration < 4; iteration++)
        {
            Prepare();
            _renderer.Spawn(Request(1, DamageTextKind.Normal, new Vector3(-2, 0, 0)));
            _renderer.Spawn(Request(2, DamageTextKind.Critical));
            _renderer.Spawn(Request(3, DamageTextKind.Healing, new Vector3(2, 0, 0))); _renderer.Tick(0, 0);
            var pixels = Render(iteration == 0 ? "three-modes" : null);
            bool orange = false, green = false;
            foreach (var pixel in pixels)
            {
                // The target may encode linear shader output as sRGB; compare color ordering rather than raw bytes.
                orange |= pixel.r > pixel.g && pixel.g > pixel.b && pixel.r - pixel.b > 40;
                green |= pixel.g > 100 && pixel.r < pixel.g * .7f;
            }
            Assert.IsTrue(orange); Assert.IsTrue(green);
            var mesh = _renderer.GetPublishedMesh(0);
            _renderer.Clear(); _renderer.Clear();
            Assert.AreEqual(0, _camera.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Length);
            Assert.IsNotNull(_atlas.Texture); Assert.AreEqual(0, Lit(Render()));
            yield return null;
            Assert.IsTrue(mesh == null, "Owned mesh must be destroyed after the frame boundary.");
        }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_108.md
    [UnityTest]
    [Category("UT_DAMAGE_108")]
    public IEnumerator ExpiredResidentDigits_DisappearWhileTheirBucketStillDraws()
    {
        var normal = _styles.Normal; normal.Color = new Color32(255, 0, 0, 255); normal.RisePixels = normal.PopAmplitude = 0; _styles.Normal = normal;
        var healing = _styles.Healing; healing.Color = new Color32(0, 0, 255, 255); healing.RisePixels = healing.PopAmplitude = 0; _styles.Healing = healing;
        Prepare();
        _renderer.Spawn(Request(1, DamageTextKind.Normal, new Vector3(-1, 0, 0)));
        _renderer.Spawn(Request(2, DamageTextKind.Healing, new Vector3(1, 0, 0))); _renderer.Tick(0, 0);
        Assert.Greater(Render()[64 * 128 + 52].r, 200);
        _renderer.Tick(normal.Lifetime, 1); var pixels = Render("expired-resident");
        Assert.AreEqual(1, _renderer.CameraPass.LastDrawCount); Assert.AreEqual(2, _renderer.Statistics.ExpiredResidentCharacters);
        Assert.Less(pixels[64 * 128 + 52].r, 10); Assert.Greater(pixels[64 * 128 + 84].b, 100);
        Assert.AreEqual(1, _renderer.Statistics.VertexUploads);
        yield return null;
    }

#if UNITY_EDITOR
    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_106.md
    [UnityTest]
    [Category("UT_DAMAGE_106")]
    public IEnumerator CommittedSampleScene_ReplaysEveryClipWithoutRejectedOrMissedEvents()
    {
        _renderer.Dispose(); _renderer = null;
        _camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(_camera.gameObject); _camera = null;
        var load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/Scene_DamageTextPlayback.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        while (!load.isDone) yield return null;
        yield return null;
        var runner = UnityEngine.Object.FindObjectOfType<DamageTextPlaybackRunner>(); Assert.IsNotNull(runner);
        try
        {
            runner.showControls = runner.showStatistics = false; runner.exportSegments = false;
            _target.Release(); UnityEngine.Object.DestroyImmediate(_target);
            _target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32); _target.Create();
            _camera = runner.outputCamera; _camera.enabled = false; _camera.targetTexture = _target; _camera.backgroundColor = Color.black;
            for (int clipIndex = 0; clipIndex < runner.clips.Length; clipIndex++)
            {
                runner.StartClip(clipIndex); Assert.IsNotNull(runner.Playback); runner.Playback.Pause();
                Assert.AreNotEqual(DamageTextPlaybackState.Failed, runner.Playback.State);
                if (clipIndex == 0) { runner.Playback.AdvanceTo(.2f); Assert.Greater(Lit(Render("sample-fixed-atlas")), 50); }
                float duration = runner.Playback.Clip.Duration;
                for (int frame = 1; runner.Playback.PlaybackTime < duration; frame++)
                {
                    float time = Mathf.Min(duration, frame / 60f);
                    if (time < runner.Playback.PlaybackTime) continue;
                    runner.Playback.AdvanceTo(time);
                    Assert.AreNotEqual(DamageTextPlaybackState.Failed, runner.Playback.State, runner.Playback.Failure);
                }
                Assert.AreEqual(runner.Playback.Clip.Count, runner.Playback.ConsumedEvents);
                var stats = runner.Playback.Renderer.Statistics;
                Assert.AreEqual(0, stats.Rejected); Assert.AreEqual(0, stats.MissedPresentation);
                Assert.AreEqual(0, stats.LiveEvents); Assert.AreEqual(runner.Playback.Clip.Count, stats.Ended);
                runner.ExportState();
                var report = JsonUtility.FromJson<DamageTextPlaybackRunner.Report>(File.ReadAllText(runner.LastReportPath));
                Assert.AreEqual(runner.Playback.Clip.Hash, report.clipHash);
                Assert.AreEqual("preview-state", report.segment.Reason); Assert.IsTrue(report.traceabilityComplete);
                Assert.AreEqual(runner.Playback.Clip.Count, report.segment.ConsumedEvents);
                Assert.AreEqual(DamageTextPlaybackRunner.CalculateSourceFingerprint(Path.GetFullPath(Path.Combine(Application.dataPath, ".."))), report.sourceFingerprint);
                yield return null;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(runner.gameObject); }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_107.md
    [UnityTest]
    [Category("UT_DAMAGE_107")]
    public IEnumerator FixedAtlas_KeepsDigitOrientationInRenderTextures()
    {
        var fixedAtlas = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageTextGlyphAtlas>("Assets/DamageText/Digits.asset");
        Assert.IsNotNull(fixedAtlas);
        var style = _styles.Normal; style.Color = new Color32(255, 255, 255, 255); style.PopAmplitude = 0; _styles.Normal = style;
        _renderer.Prepare(_configuration, fixedAtlas, _styles, _shader); _renderer.BindCamera(_camera);
        var request = Request(1); request.Amount = 12; _renderer.Spawn(request); _renderer.Tick(0, 0);
        var pixels = Render("fixed-digit-orientation");
        Assert.Less(pixels[72 * 128 + 58].r, 10, "Digit 1's narrow top must not become its wide bottom.");
        Assert.Greater(pixels[55 * 128 + 58].r, 200, "Digit 1's wide bottom must be below its narrow top.");
        yield return null;
    }
#endif
}
