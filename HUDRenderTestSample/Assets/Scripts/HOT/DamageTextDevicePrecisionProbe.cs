using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Device counterpart of UT_DAMAGE_125, outside all formal timing segments.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class DamageTextDevicePrecisionProbe : MonoBehaviour
{
    public DamageTextBenchmarkRunner runner;
    [Serializable] private sealed class Point
    {
        public int caseIndex;
        public Vector2 originalLocal, quantizedLocal, projectedOriginal, projectedQuantized;
        public float pixelError;
    }
    [Serializable] private sealed class Case
    {
        public uint amount;
        public int cameraPose, stride, differentPixels;
        public bool orthographic, maximumStyle, candidatePassed, passed;
        public Vector3 camera, anchor;
        public float maximumPixelError;
        public string effectiveFormat, formatReason, vertexLayout, referenceImage, effectiveImage, referenceSHA256, effectiveSHA256;
        public RectInt referenceBounds, effectiveBounds;
    }
    [Serializable] private sealed class Report
    {
        public string schema = "damage-device-precision-v1";
        public string sourceFingerprint, commit, buildGuid, api, device, fixtureAtlasHash;
        public string scope = "Fractional white-rectangle geometry fixture; actual Vulkan storage, projection, bounds and fallback. Separate fixed-digit atlas regression. No timing or art/default approval.";
        public int width, height;
        public bool supportsFloat16x2, passed, formal;
        public Case[] cases;
        public Point[] points;
    }
    private Camera _camera;
    private RenderTexture _target;
    private Texture2D _readback;
    private DamageTextGlyphAtlas _atlas;
    private DamageTextStyles _styles;
    private bool _originalCameraEnabled;
    private void Awake() { if (runner != null) runner.enabled = false; }
    private IEnumerator Start()
    {
        _originalCameraEnabled = runner.outputCamera.enabled; runner.outputCamera.enabled = false;
        _camera = new GameObject("Damage precision diagnostic camera", typeof(Camera)).GetComponent<Camera>();
        _camera.CopyFrom(runner.outputCamera); _camera.enabled = false; _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.black; _camera.allowHDR = _camera.allowMSAA = false;
        _target = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32); _target.Create(); _camera.targetTexture = _target;
        _camera.aspect = _target.width / (float)_target.height; _camera.orthographicSize = 6.5f; _camera.fieldOfView = 60;
        _readback = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
        _atlas = ScriptableObject.CreateInstance<DamageTextGlyphAtlas>(); _styles = Instantiate(runner.styles);
        var glyphs = new DamageTextGlyph[10];
        for (int i = 0; i < glyphs.Length; i++) glyphs[i] = new DamageTextGlyph
        { Available = true, Size = new Vector2(5.0179f, 7.0133f), Advance = 6.1337f, Uv = new Rect(0, 0, 1, 1) };
        _atlas.Initialize(Texture2D.whiteTexture, 24, glyphs, "fractional-device-graphics-v1", "white-fixture");
        var configuration = new DamageTextConfiguration { BucketCapacity = 1, MaximumBuckets = 1, MaximumPendingEvents = 1, MaximumSessionEvents = 4 };
        var cases = new List<Case>(); var points = new List<Point>();
        string directory = Path.Combine(Application.persistentDataPath, "DamageTextBenchmark", "Diagnostics", "precision-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (uint amount in new uint[] { 10, 99999999 })
            foreach (bool maximum in new[] { false, true })
            foreach (bool orthographic in new[] { true, false })
            foreach (int pose in new[] { 0, 1, 2, 3 })
            {
                var style = _styles.Critical; style.PixelSize = maximum ? 512 : 33.6f;
                style.PopAmplitude = maximum ? 4 : .45f; style.Tracking = 1.1234f; _styles.Critical = style;
                _camera.orthographic = orthographic;
                float anchorX = pose == 0 ? 0 : 1000;
                float depth = pose == 3 ? _camera.nearClipPlane + .01f : 10;
                float halfWidth = orthographic ? _camera.orthographicSize * _camera.aspect : Mathf.Tan(_camera.fieldOfView * .5f * Mathf.Deg2Rad) * depth * _camera.aspect;
                _camera.transform.SetPositionAndRotation(new Vector3(anchorX - (pose >= 2 ? .94f * halfWidth : 0), 0, -10), Quaternion.identity);
                var request = new DamageTextRequest { EventId = 1, Amount = amount, Kind = DamageTextKind.Critical,
                    RandomSeed = 19, WorldPosition = new Vector3(anchorX, 0, -10 + depth) };
                float time = style.PopDuration * .5f; int index = cases.Count;
                string referenceName = "case-" + index.ToString("D2") + "-float32.png";
                string effectiveName = "case-" + index.ToString("D2") + "-effective.png";
                Color32[] expected;
                using (var renderer = new DamageTextRenderer())
                {
                    renderer.Prepare(configuration, _atlas, _styles, runner.meshShader); renderer.BindCamera(_camera);
                    if (renderer.Spawn(request) != DamageTextSpawnResult.Accepted) throw new InvalidOperationException("Reference request rejected.");
                    renderer.Tick(time, 1); expected = Render(Path.Combine(directory, referenceName));
                }
                using (var benchmark = new DamageTextBenchmark())
                {
                    benchmark.Prepare(new DamageTextClip(new[] { request }, 2, "precision-device-v1", 19), configuration,
                        _atlas, _styles, runner.meshShader, _camera, new DamageTextBenchmarkPlan
                        { Phase = DamageTextBenchmarkPhase.DrawOnly, PositionFormat = DamageTextPositionFormat.Float16, Checkpoint = time, WarmupFrames = 0, SampleFrames = 2 });
                    var actual = Render(Path.Combine(directory, effectiveName));
                    var audit = benchmark.Report.precision;
                    var a = Bounds(expected); var b = Bounds(actual);
                    int different = 0;
                    for (int pixel = 0; pixel < expected.Length; pixel++)
                        if (!expected[pixel].Equals(actual[pixel])) different++;
                    bool half = benchmark.Report.effectiveFormat == "Float16";
                    bool passed = half ? audit.candidatePassed && audit.maximumPixelError <= .25f &&
                        Math.Abs(a.xMin - b.xMin) <= 1 && Math.Abs(a.xMax - b.xMax) <= 1 &&
                        Math.Abs(a.yMin - b.yMin) <= 1 && Math.Abs(a.yMax - b.yMax) <= 1 && (a.width == 0 || b.width > 0) : different == 0;
                    cases.Add(new Case
                    {
                        amount = amount, cameraPose = pose, orthographic = orthographic, maximumStyle = maximum,
                        camera = _camera.transform.position, anchor = request.WorldPosition, candidatePassed = audit.candidatePassed,
                        maximumPixelError = audit.maximumPixelError, effectiveFormat = benchmark.Report.effectiveFormat,
                        formatReason = benchmark.Report.formatReason, vertexLayout = benchmark.Renderer.VertexLayout,
                        stride = benchmark.Renderer.Statistics.VertexStride, passed = passed, differentPixels = different,
                        referenceBounds = a, effectiveBounds = b, referenceImage = referenceName, effectiveImage = effectiveName,
                        referenceSHA256 = DamageTextBenchmarkFiles.HashFile(Path.Combine(directory, referenceName)),
                        effectiveSHA256 = DamageTextBenchmarkFiles.HashFile(Path.Combine(directory, effectiveName))
                    });
                    var layout = new DamageTextGlyphQuad[8];
                    int characters = DamageTextLayout.Build(amount, _atlas.Freeze(), style.Tracking, new byte[8], layout);
                    for (int glyph = 0; glyph < characters; glyph++) foreach (var local in new[] { layout[glyph].Min, layout[glyph].Max,
                        new Vector2(layout[glyph].Min.x, layout[glyph].Max.y), new Vector2(layout[glyph].Max.x, layout[glyph].Min.y) })
                    {
                        var quantized = DamageTextHalfVertex.Encode(new DamageTextVertex { Position = local }).DecodePosition();
                        var originalScreen = DamageTextVertexFormats.Project(local, request, style, 24, time, _camera);
                        var quantizedScreen = DamageTextVertexFormats.Project(quantized, request, style, 24, time, _camera);
                        points.Add(new Point { caseIndex = index, originalLocal = local, quantizedLocal = quantized,
                            projectedOriginal = originalScreen, projectedQuantized = quantizedScreen, pixelError = Vector2.Distance(originalScreen, quantizedScreen) });
                    }
                }
                yield return null;
            }
            bool allPassed = true; foreach (var item in cases) allPassed &= item.passed;
            var report = new Report { sourceFingerprint = runner.sourceFingerprint, commit = runner.sourceCommit,
                buildGuid = Application.buildGUID, api = SystemInfo.graphicsDeviceType.ToString(), device = SystemInfo.graphicsDeviceName,
                fixtureAtlasHash = _atlas.Freeze().Hash, width = _target.width, height = _target.height,
                supportsFloat16x2 = SystemInfo.SupportsVertexAttributeFormat(VertexAttributeFormat.Float16, 2),
                passed = allPassed, cases = cases.ToArray(), points = points.ToArray() };
            File.WriteAllText(Path.Combine(directory, "precision.json"), JsonUtility.ToJson(report, true));
            Debug.Log("DAMAGE_PRECISION_PROBE_COMPLETE=" + directory);
        }
        finally { Cleanup(); }
    }
    private Color32[] Render(string path)
    {
        _camera.Render(); var previous = RenderTexture.active;
        try { RenderTexture.active = _target; _readback.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0); _readback.Apply(); }
        finally { RenderTexture.active = previous; }
        File.WriteAllBytes(path, _readback.EncodeToPNG()); return _readback.GetPixels32();
    }
    private RectInt Bounds(Color32[] pixels)
    {
        int minX = _target.width, minY = _target.height, maxX = -1, maxY = -1;
        for (int i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i]; if (pixel.r < 6 && pixel.g < 6 && pixel.b < 6) continue;
            minX = Math.Min(minX, i % _target.width); maxX = Math.Max(maxX, i % _target.width);
            minY = Math.Min(minY, i / _target.width); maxY = Math.Max(maxY, i / _target.width);
        }
        return maxX < 0 ? new RectInt() : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
    private void OnDestroy() { Cleanup(); }
    private void Cleanup()
    {
        if (_camera != null) { Destroy(_camera.gameObject); _camera = null; }
        if (_target != null) { _target.Release(); Destroy(_target); _target = null; }
        if (_readback != null) { Destroy(_readback); _readback = null; }
        if (_atlas != null) { Destroy(_atlas); _atlas = null; }
        if (_styles != null) { Destroy(_styles); _styles = null; }
        if (runner != null && runner.outputCamera != null) runner.outputCamera.enabled = _originalCameraEnabled;
    }
}
