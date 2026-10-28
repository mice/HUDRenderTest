using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public sealed class TestDamageTextComparison
{
    private Camera _camera;
    private RenderTexture _target;
    private DamageTextGlyphAtlas _atlas;
    private DamageTextStyles _styles;
    private DamageTextRenderer _renderer;
    private DamageTextConfiguration _configuration;

    [SetUp]
    public void SetUp()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Actual graphics device required.");
        if (!SystemInfo.supportsInstancing) Assert.Ignore("Instancing unsupported; A is unavailable on this device.");
        if (GraphicsSettings.currentRenderPipeline != null) Assert.Ignore("Built-in camera contract.");
        _camera = new GameObject("Damage comparison camera", typeof(Camera)).GetComponent<Camera>();
        _camera.enabled = false; _camera.orthographic = true; _camera.orthographicSize = 5;
        _camera.transform.position = new Vector3(0, 0, -10);
        _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = Color.black; _camera.cullingMask = 0;
        _target = new RenderTexture(512, 256, 24, RenderTextureFormat.ARGB32);
        _target.Create(); _camera.targetTexture = _target;
        _atlas = ScriptableObject.CreateInstance<DamageTextGlyphAtlas>();
        var digits = new DamageTextGlyph[10];
        for (int i = 0; i < digits.Length; i++) digits[i] = new DamageTextGlyph
        { Available = true, Size = new Vector2(5, 7), Advance = 6, Uv = new Rect(0, 0, 1, 1) };
        _atlas.Initialize(Texture2D.whiteTexture, 24, digits, "comparison-fixture-v1", "white-pixel-fixture-v1");
        _styles = ScriptableObject.CreateInstance<DamageTextStyles>();
        _configuration = new DamageTextConfiguration { MaximumBuckets = 4, MaximumPendingEvents = 512 };
    }
    [TearDown]
    public void TearDown()
    {
        _renderer?.Dispose();
        if (_camera != null) _camera.targetTexture = null;
        if (_target != null) { _target.Release(); UnityEngine.Object.DestroyImmediate(_target); }
        if (_camera != null) UnityEngine.Object.DestroyImmediate(_camera.gameObject);
        UnityEngine.Object.DestroyImmediate(_atlas); UnityEngine.Object.DestroyImmediate(_styles);
    }
    private void Prepare(DamageTextGeometry geometry)
    {
        _renderer?.Dispose(); _renderer = new DamageTextRenderer(geometry: geometry);
        var shader = Shader.Find(geometry == DamageTextGeometry.InstancedQuad ? "UIData/DamageTextInstanced" : "UIData/DamageText");
        Assert.IsNotNull(shader); Assert.IsTrue(shader.isSupported);
        _renderer.Prepare(_configuration, _atlas, _styles, shader); _renderer.BindCamera(_camera);
    }
    private static DamageTextRequest Request(uint id, uint amount, Vector3 position, DamageTextKind kind = DamageTextKind.Normal)
        => new DamageTextRequest { EventId = id, Amount = amount, WorldPosition = position, Kind = kind, RandomSeed = id + 19 };
    private void SpawnAll(DamageTextRequest[] requests)
    {
        foreach (var request in requests) Assert.AreEqual(DamageTextSpawnResult.Accepted, _renderer.Spawn(request));
        _renderer.Tick(0, 0);
    }
    private Color32[] Render(string evidence = null)
    {
        _camera.Render(); var old = RenderTexture.active;
        var texture = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = _target; texture.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0); texture.Apply();
            if (evidence != null)
            {
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../TestResults/DamageTextComparison/Images"));
                Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, evidence + ".png"), texture.EncodeToPNG());
            }
            return texture.GetPixels32();
        }
        finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(texture); }
    }
    private static int Lit(Color32[] pixels)
    {
        int count = 0; foreach (var p in pixels) if (p.r > 5 || p.g > 5 || p.b > 5) count++; return count;
    }
    private static void EqualPixels(Color32[] reference, Color32[] actual)
    {
        // Same target/color space, geometry and blend: exact equality catches changed transparent order.
        CollectionAssert.AreEqual(reference, actual);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_109.md
    [UnityTest]
    [Category("UT_DAMAGE_109")]
    public IEnumerator Instancing1024Digits_PreservesLastCharacterAndAllEightGroups()
    {
        var requests = new DamageTextRequest[128];
        for (int i = 0; i < requests.Length; i++) requests[i] = Request((uint)i + 1, 99999999,
            new Vector3((i % 16 - 7.5f) * 1.15f, (i / 16 - 3.5f) * 1.1f, 0));
        Prepare(DamageTextGeometry.PersistentMesh); SpawnAll(requests); var reference = Render("1024-B");
        Assert.Greater(Lit(reference), 10000);
        Prepare(DamageTextGeometry.InstancedQuad); SpawnAll(requests); var actual = Render("1024-A");
        Assert.AreEqual(1024, _renderer.Statistics.SubmittedCharacters);
        Assert.AreEqual(8, _renderer.Statistics.InstanceGroups);
        Assert.AreEqual(8, _renderer.CameraPass.LastDrawCount);
        Assert.AreEqual(8, _renderer.Statistics.Passes);
        Assert.AreEqual(1, _renderer.Statistics.OwnedMeshes);
        EqualPixels(reference, actual);
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_110.md
    [UnityTest]
    [Category("UT_DAMAGE_110")]
    public IEnumerator AllModesAndDigitLengths_MatchAtSharedAnimationTimes()
    {
        var requests = new DamageTextRequest[9]; uint[] amounts = { 10, 12345, 99999999 };
        for (int i = 0; i < requests.Length; i++) requests[i] = Request((uint)i + 1, amounts[i % 3],
            new Vector3((i % 3 - 1) * 5, (i / 3 - 1) * 2.5f - 1, 0), (DamageTextKind)(i / 3));
        foreach (float time in new[] { 0f, .11f, .72f, .85f, 1.1f })
        {
            Prepare(DamageTextGeometry.PersistentMesh); SpawnAll(requests); _renderer.Tick(time, 1);
            var expected = Render(); var statistics = _renderer.Statistics;
            Prepare(DamageTextGeometry.InstancedQuad); SpawnAll(requests); _renderer.Tick(time, 1);
            EqualPixels(expected, Render(time == .11f ? "three-modes-A" : null));
            Assert.AreEqual(statistics.LiveCharacters, _renderer.Statistics.LiveCharacters);
            Assert.AreEqual(statistics.Ended, _renderer.Statistics.Ended);
            Assert.AreEqual(statistics.ExpiredResidentCharacters, _renderer.Statistics.ExpiredResidentCharacters);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_111.md
    [UnityTest]
    [Category("UT_DAMAGE_111")]
    public IEnumerator TransparentGroupAndBucketBoundaries_Preserve128And129Order()
    {
        var normal = _styles.Normal; normal.Color = new Color32(255, 0, 0, 128); normal.PopAmplitude = 0; _styles.Normal = normal;
        var healing = normal; healing.Color = new Color32(0, 0, 255, 128); _styles.Healing = healing;
        foreach (int count in new[] { 17, 128, 129, 256 })
        {
            var requests = new DamageTextRequest[count];
            for (int i = 0; i < count; i++) requests[i] = Request((uint)i + 1, 99999999,
                i < count - 2 ? new Vector3(10000, 0, 0) : Vector3.zero,
                i == count - 1 ? DamageTextKind.Healing : DamageTextKind.Normal);
            Prepare(DamageTextGeometry.PersistentMesh); SpawnAll(requests); var expected = Render();
            Assert.Greater(Lit(expected), 100);
            Prepare(DamageTextGeometry.InstancedQuad); SpawnAll(requests);
            EqualPixels(expected, Render(count == 129 ? "order-129-A" : null));
            Assert.AreEqual((count * 8 + 127) / 128, _renderer.CameraPass.LastDrawCount);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_112.md
    [UnityTest]
    [Category("UT_DAMAGE_112")]
    public IEnumerator MovingPerspectiveOrOrthographicCamera_MatchesViewportAndNearClipping()
    {
        foreach (bool orthographic in new[] { true, false })
        foreach (float x in new[] { 1000f, 990.1f })
        foreach (float z in new[] { -10f, -.1f, 1f })
        {
            _camera.orthographic = orthographic; _camera.transform.position = new Vector3(x, 0, z);
            _camera.rect = new Rect(.1f, .1f, .8f, .8f);
            var requests = new[] { Request(1, 12345, new Vector3(1000, 0, 0), DamageTextKind.Critical) };
            Prepare(DamageTextGeometry.PersistentMesh); SpawnAll(requests); _renderer.Tick(.13f, 1); var expected = Render();
            Prepare(DamageTextGeometry.InstancedQuad); SpawnAll(requests); _renderer.Tick(.13f, 1); EqualPixels(expected, Render());
            if (z >= -.1f) Assert.AreEqual(0, Lit(expected));
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_113.md
    [UnityTest]
    [Category("UT_DAMAGE_113")]
    public IEnumerator FrozenCheckpoint_KeepsExpiredResidentsWithoutGeometryUploads()
    {
        foreach (var geometry in new[] { DamageTextGeometry.PersistentMesh, DamageTextGeometry.InstancedQuad })
        {
            Prepare(geometry);
            SpawnAll(new[] { Request(1, 99999999, Vector3.left), Request(2, 10, Vector3.right, DamageTextKind.Healing) });
            _renderer.Tick(_styles.Normal.Lifetime, 1); var expected = Render();
            var statistics = _renderer.Statistics; long revision = _renderer.DrawRevision;
            Assert.AreEqual(8, statistics.ExpiredResidentCharacters); Assert.AreEqual(10, statistics.SubmittedCharacters);
            for (int i = 2; i < 6; i++) { _renderer.Tick(_styles.Normal.Lifetime, i); EqualPixels(expected, Render()); }
            Assert.AreEqual(statistics.InstanceUploads, _renderer.Statistics.InstanceUploads);
            Assert.AreEqual(statistics.VertexUploads, _renderer.Statistics.VertexUploads);
            Assert.AreEqual(revision, _renderer.DrawRevision);
        }
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_114.md
    [UnityTest]
    [Category("UT_DAMAGE_114")]
    public IEnumerator InstancedPlayback_ConsumesSlowFramesAndReleasesCameraCommands()
    {
        var requests = new[] { Request(1, 10, Vector3.zero), Request(2, 99999999, Vector3.zero, DamageTextKind.Healing) };
        requests[1].SpawnTime = 1;
        var clip = new DamageTextClip(requests, 3, "comparison-lifecycle-v1", 19);
        for (int i = 0; i < 3; i++)
        {
            using (var playback = new DamageTextPlayback(geometry: DamageTextGeometry.InstancedQuad))
            {
                playback.Prepare(clip, _configuration, _atlas, _styles, Shader.Find("UIData/DamageTextInstanced"), _camera);
                playback.AdvanceTo(2.5); Assert.AreEqual(2, playback.ConsumedEvents);
                Assert.AreEqual(1, playback.Renderer.Statistics.MissedPresentation);
                Assert.AreEqual(0, playback.Renderer.Statistics.LiveCharacters);
                Assert.AreEqual(2, _camera.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Length);
            }
            Assert.AreEqual(0, _camera.GetCommandBuffers(CameraEvent.AfterForwardAlpha).Length);
            yield return null;
        }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_115.md
    [UnityTest]
    [Category("UT_DAMAGE_115")]
    public IEnumerator FixedSampleAtlas_InstancingMatchesGlyphUvsAndAnimation()
    {
#if UNITY_EDITOR
        var sourceAtlas = UnityEditor.AssetDatabase.LoadAssetAtPath<DamageTextGlyphAtlas>("Assets/DamageText/Digits.asset");
        Assert.IsNotNull(sourceAtlas);
        UnityEngine.Object.DestroyImmediate(_atlas); _atlas = UnityEngine.Object.Instantiate(sourceAtlas);
        var requests = new[]
        {
            Request(1, 10, new Vector3(-6, -1, 0)), Request(2, 12345, new Vector3(0, -1, 0), DamageTextKind.Critical),
            Request(3, 98765432, new Vector3(6, -1, 0), DamageTextKind.Healing)
        };
        foreach (float time in new[] { 0f, .12f, .7f })
        {
            Prepare(DamageTextGeometry.PersistentMesh); SpawnAll(requests); _renderer.Tick(time, 1);
            var expected = Render(time == .12f ? "fixed-atlas-B" : null);
            Prepare(DamageTextGeometry.InstancedQuad); SpawnAll(requests); _renderer.Tick(time, 1);
            EqualPixels(expected, Render(time == .12f ? "fixed-atlas-A" : null));
            Assert.Greater(Lit(expected), 100);
        }
#endif
        yield return null;
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_116.md
    [UnityTest]
    [Category("UT_DAMAGE_116")]
    public IEnumerator HalfStorage_Uses64ByteStrideAndKeepsIntegerFixturePixelsAndBounds()
    {
        if (!SystemInfo.SupportsVertexAttributeFormat(VertexAttributeFormat.Float16, 2)) Assert.Ignore("Float16x2 unsupported.");
        var requests = new[] { Request(1, 10, new Vector3(-5, -1, 0)), Request(2, 99999999,
            new Vector3(1000, -1, 0), DamageTextKind.Critical) };
        foreach (bool orthographic in new[] { true, false })
        foreach (float cameraX in new[] { 0f, 1000f, 990.1f })
        {
            _camera.orthographic = orthographic; _camera.transform.position = new Vector3(cameraX, 0, -10);
            Prepare(DamageTextGeometry.PersistentMesh); SpawnAll(requests); _renderer.Tick(.12f, 1);
            var expected = Render(); long float32Bytes = _renderer.Statistics.TheoreticalUploadBytes;
            _renderer.Dispose(); _renderer = new DamageTextRenderer(positionFormat: DamageTextPositionFormat.Float16);
            _renderer.Prepare(_configuration, _atlas, _styles, Shader.Find("UIData/DamageText")); _renderer.BindCamera(_camera);
            SpawnAll(requests); _renderer.Tick(.12f, 1);
            Assert.AreEqual(DamageTextPositionFormat.Float16, _renderer.EffectivePositionFormat, _renderer.FormatReason);
            var mesh = _renderer.GetPublishedMesh(0);
            Assert.IsTrue(DamageTextVertexFormats.MatchesLayout(mesh, DamageTextPositionFormat.Float16));
            Assert.AreEqual(64, mesh.GetVertexBufferStride(0));
            StringAssert.Contains("Position:Float16x2@0#0", _renderer.VertexLayout);
            StringAssert.Contains("TexCoord3:Float32x4@48#0", _renderer.VertexLayout);
            Assert.AreEqual(160, float32Bytes - _renderer.Statistics.TheoreticalUploadBytes);
            Assert.Greater(mesh.bounds.size.x, 10000, "Explicit shader-animation bounds remain installed.");
            EqualPixels(expected, Render(cameraX == 1000 && orthographic ? "half-far-anchor" : null));
        }
        yield return null;
    }

    [Serializable] private sealed class PrecisionEvidence
    {
        public string scope = "local D3D11 graphics/quantization diagnostic; device/art/performance acceptance pending";
        public string api, device;
        public PrecisionRow[] rows;
    }
    [Serializable] private struct PrecisionRow
    {
        public uint amount;
        public bool orthographic, maximumStyle, candidatePassed;
        public Vector3 camera;
        public Vector2 originalLocal, quantizedLocal, projectedOriginal, projectedQuantized;
        public float pixelError, maximumPixelError;
        public string effectiveFormat;
    }
    private static RectInt PixelBounds(Color32[] pixels, int width, int height)
    {
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i]; if (p.r < 6 && p.g < 6 && p.b < 6) continue;
            minX = Math.Min(minX, i % width); maxX = Math.Max(maxX, i % width);
            minY = Math.Min(minY, i / width); maxY = Math.Max(maxY, i / width);
        }
        return maxX < 0 ? new RectInt() : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_125.md
    [UnityTest]
    [Category("UT_DAMAGE_125")]
    public IEnumerator FractionalHalfPrecision_RecordsProjectionAndGraphicsAcrossMaximumScaleAndMovingEdges()
    {
        if (!SystemInfo.SupportsVertexAttributeFormat(VertexAttributeFormat.Float16, 2)) Assert.Ignore("Float16x2 unsupported.");
        var glyphs = new DamageTextGlyph[10];
        for (int i = 0; i < glyphs.Length; i++) glyphs[i] = new DamageTextGlyph
        { Available = true, Size = new Vector2(5.0179f, 7.0133f), Advance = 6.1337f, Uv = new Rect(0, 0, 1, 1) };
        _atlas.Initialize(Texture2D.whiteTexture, 24, glyphs, "fractional-graphics-v1", "white-fixture");
        var rows = new List<PrecisionRow>(); bool sawCandidate = false, sawFallback = false;
        foreach (uint amount in new uint[] { 10, 99999999 })
        foreach (bool maximum in new[] { false, true })
        foreach (bool orthographic in new[] { true, false })
        foreach (int cameraPose in new[] { 0, 1, 2 })
        {
            var style = _styles.Critical; style.PixelSize = maximum ? 512 : 33.6f; style.PopAmplitude = maximum ? 4 : .45f;
            style.Tracking = 1.1234f; _styles.Critical = style;
            _camera.orthographic = orthographic;
            _camera.transform.position = new Vector3(cameraPose == 0 ? 0 : cameraPose == 1 ? 1000 : 1000 - (orthographic ? 9.9f : 11.4f), 0, -10);
            var request = Request(1, amount, new Vector3(cameraPose == 0 ? 0 : 1000, 0, 0), DamageTextKind.Critical);
            float time = style.PopDuration * .5f;
            Prepare(DamageTextGeometry.PersistentMesh); SpawnAll(new[] { request }); _renderer.Tick(time, 1);
            var expected = Render(!maximum && amount == 99999999 && cameraPose == 1 && orthographic ? "fractional-float32" : null);
            _renderer.Dispose(); _renderer = null;
            using (var benchmark = new DamageTextBenchmark())
            {
                benchmark.Prepare(new DamageTextClip(new[] { request }, 2, "precision-graphics-v1", 19), _configuration, _atlas, _styles,
                    Shader.Find("UIData/DamageText"), _camera, new DamageTextBenchmarkPlan
                    { Phase = DamageTextBenchmarkPhase.DrawOnly, PositionFormat = DamageTextPositionFormat.Float16, Checkpoint = time, WarmupFrames = 0, SampleFrames = 2 });
                var actual = Render(!maximum && amount == 99999999 && cameraPose == 1 && orthographic ? "fractional-float16" : null);
                var audit = benchmark.Report.precision;
                sawCandidate |= audit.candidatePassed; sawFallback |= !audit.candidatePassed;
                if (!audit.candidatePassed)
                {
                    Assert.AreEqual("Float32", benchmark.Report.effectiveFormat); EqualPixels(expected, actual);
                }
                else
                {
                    Assert.AreEqual("Float16", benchmark.Report.effectiveFormat); Assert.LessOrEqual(audit.maximumPixelError, .25f);
                    var a = PixelBounds(expected, _target.width, _target.height); var b = PixelBounds(actual, _target.width, _target.height);
                    Assert.That(b.xMin, Is.EqualTo(a.xMin).Within(1)); Assert.That(b.xMax, Is.EqualTo(a.xMax).Within(1));
                    Assert.That(b.yMin, Is.EqualTo(a.yMin).Within(1)); Assert.That(b.yMax, Is.EqualTo(a.yMax).Within(1));
                    if (Lit(expected) > 0) Assert.Greater(Lit(actual), 0, "Accepted quantization must not incorrectly cull the string.");
                }
                var layout = new DamageTextGlyphQuad[8];
                int characters = DamageTextLayout.Build(amount, _atlas.Freeze(), style.Tracking, new byte[8], layout);
                for (int i = 0; i < characters; i++) foreach (var local in new[] { layout[i].Min, layout[i].Max,
                    new Vector2(layout[i].Min.x, layout[i].Max.y), new Vector2(layout[i].Max.x, layout[i].Min.y) })
                {
                    var quantized = DamageTextHalfVertex.Encode(new DamageTextVertex { Position = local }).DecodePosition();
                    var projected = DamageTextVertexFormats.Project(local, request, style, 24, time, _camera);
                    var quantizedProjected = DamageTextVertexFormats.Project(quantized, request, style, 24, time, _camera);
                    rows.Add(new PrecisionRow { amount = amount, orthographic = orthographic, maximumStyle = maximum, camera = _camera.transform.position,
                        originalLocal = local, quantizedLocal = quantized, projectedOriginal = projected, projectedQuantized = quantizedProjected,
                        pixelError = Vector2.Distance(projected, quantizedProjected), maximumPixelError = audit.maximumPixelError,
                        candidatePassed = audit.candidatePassed, effectiveFormat = benchmark.Report.effectiveFormat });
                }
            }
        }
        Assert.IsTrue(sawCandidate); Assert.IsTrue(sawFallback);
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../TestResults/DamageTextComparison")); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "precision-projection.json"), JsonUtility.ToJson(new PrecisionEvidence
        { api = SystemInfo.graphicsDeviceType.ToString(), device = SystemInfo.graphicsDeviceName, rows = rows.ToArray() }, true));
        yield return null;
    }
}
