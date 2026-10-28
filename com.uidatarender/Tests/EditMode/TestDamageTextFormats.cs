using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

public sealed class TestDamageTextFormats
{
    private DamageTextGlyphAtlas _atlas;
    private DamageTextStyles _styles;
    private DamageTextConfiguration _configuration;
    private DamageTextRenderer _renderer;
    private sealed class Capabilities : IDamageTextVertexFormatSupport
    {
        public bool SupportsFloat16x2 { get; set; }
        public int HalfChecks;
        public int RejectHalfCheck;
        public bool MatchesLayout(Mesh mesh, DamageTextPositionFormat format)
        {
            if (format == DamageTextPositionFormat.Float16 && ++HalfChecks == RejectHalfCheck) return false;
            return DamageTextVertexFormats.MatchesLayout(mesh, format);
        }
    }
    [SetUp]
    public void SetUp()
    {
        _atlas = ScriptableObject.CreateInstance<DamageTextGlyphAtlas>();
        var glyphs = new DamageTextGlyph[10];
        for (int i = 0; i < glyphs.Length; i++) glyphs[i] = new DamageTextGlyph
        { Available = true, Size = new Vector2(5.0179f, 7.0133f), Advance = 6.1337f, Uv = new Rect(0, 0, 1, 1) };
        _atlas.Initialize(Texture2D.whiteTexture, 24, glyphs, "fractional-format-fixture-v1", "white-fixture");
        _styles = ScriptableObject.CreateInstance<DamageTextStyles>();
        _configuration = new DamageTextConfiguration { MaximumBuckets = 2 };
    }
    [TearDown]
    public void TearDown()
    {
        _renderer?.Dispose(); UnityEngine.Object.DestroyImmediate(_atlas); UnityEngine.Object.DestroyImmediate(_styles);
    }
    private void Prepare() => _renderer.Prepare(_configuration, _atlas, _styles, Shader.Find("UIData/DamageText"));
    private void Spawn()
    {
        Assert.AreEqual(DamageTextSpawnResult.Accepted, _renderer.Spawn(new DamageTextRequest { EventId = 1, Amount = 99999999 }));
        _renderer.Tick(0, 0);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_025.md
    [Test]
    [Category("UT_DAMAGE_025")]
    public void HalfEncoding_ChangesOnlyLocalXYAndUses64ByteStorage()
    {
        var input = new DamageTextVertex
        {
            Position = new Vector2(11.123f, -9.987f), Color = new Color32(5, 17, 128, 253), Uv = new Vector2(.12345f, .98765f),
            AnchorAndSpawn = new Vector4(12345.67f, -76543.21f, .0031f, 1799.123f),
            Motion = new Vector4(1.1234f, 1.4321f, -23.987f, 57.123f), Animation = new Vector4(.47f, .213f, .671f, 2)
        };
        var encoded = DamageTextHalfVertex.Encode(input);
        Assert.AreEqual(68, Marshal.SizeOf<DamageTextVertex>()); Assert.AreEqual(64, Marshal.SizeOf<DamageTextHalfVertex>());
        Assert.AreEqual(4, (int)Marshal.OffsetOf<DamageTextHalfVertex>(nameof(DamageTextHalfVertex.Color)));
        Assert.AreEqual(16, (int)Marshal.OffsetOf<DamageTextHalfVertex>(nameof(DamageTextHalfVertex.AnchorAndSpawn)));
        Assert.AreEqual(input.Color, encoded.Color); Assert.AreEqual(input.Uv, encoded.Uv);
        Assert.AreEqual(input.AnchorAndSpawn, encoded.AnchorAndSpawn); Assert.AreEqual(input.Motion, encoded.Motion);
        Assert.AreEqual(input.Animation, encoded.Animation); Assert.AreNotEqual(input.Position, encoded.DecodePosition());
        Assert.AreEqual(16384, 4096 * (DamageTextVertex.Stride - DamageTextHalfVertex.Stride));
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_026.md
    [Test]
    [Category("UT_DAMAGE_026")]
    public void UnsupportedHalfFormat_FallsBackDuringPreparingAndStaysFloat32UntilNewPrepare()
    {
        var capabilities = new Capabilities();
        _renderer = new DamageTextRenderer(positionFormat: DamageTextPositionFormat.Float16, formatSupport: capabilities);
        Prepare(); Spawn();
        Assert.AreEqual(DamageTextPositionFormat.Float16, _renderer.RequestedPositionFormat);
        Assert.AreEqual(DamageTextPositionFormat.Float32, _renderer.EffectivePositionFormat);
        Assert.AreEqual("float16x2-unsupported", _renderer.FormatReason);
        Assert.AreEqual(68, _renderer.GetPublishedMesh(0).GetVertexBufferStride(0));
        capabilities.SupportsFloat16x2 = true; _renderer.Tick(.1f, 1);
        Assert.AreEqual(DamageTextPositionFormat.Float32, _renderer.EffectivePositionFormat);
        Assert.AreEqual(68, _renderer.Statistics.VertexStride);
        Assert.AreEqual(0, capabilities.HalfChecks, "Never requery/change format inside a segment.");
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_027.md
    [Test]
    [Category("UT_DAMAGE_027")]
    public void UnexpectedProbeLayout_FallsBackBeforePublishingAnyGeometry()
    {
        var capabilities = new Capabilities { SupportsFloat16x2 = true, RejectHalfCheck = 1 };
        _renderer = new DamageTextRenderer(positionFormat: DamageTextPositionFormat.Float16, formatSupport: capabilities);
        Prepare(); Spawn();
        Assert.AreEqual("float16-layout-mismatch", _renderer.FormatReason);
        Assert.AreEqual(DamageTextPositionFormat.Float32, _renderer.EffectivePositionFormat);
        Assert.IsTrue(DamageTextVertexFormats.MatchesLayout(_renderer.GetPublishedMesh(0), DamageTextPositionFormat.Float32));
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_028.md
    [Test]
    [Category("UT_DAMAGE_028")]
    public void UnexpectedSecondBucketLayout_RecreatesEveryBucketWithFloat32()
    {
        var capabilities = new Capabilities { SupportsFloat16x2 = true, RejectHalfCheck = 4 };
        _renderer = new DamageTextRenderer(positionFormat: DamageTextPositionFormat.Float16, formatSupport: capabilities);
        Prepare();
        for (uint i = 1; i <= 129; i++) _renderer.Spawn(new DamageTextRequest { EventId = i, Amount = 10 });
        _renderer.Tick(0, 0);
        Assert.AreEqual("actual-bucket-layout-mismatch", _renderer.FormatReason);
        Assert.AreEqual(2, _renderer.Statistics.Buckets);
        Assert.AreEqual(68, _renderer.GetPublishedMesh(0).GetVertexBufferStride(0));
        Assert.AreEqual(68, _renderer.GetPublishedMesh(1).GetVertexBufferStride(0));
        Assert.AreEqual(129, _renderer.Statistics.Published);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_029.md
    [Test]
    [Category("UT_DAMAGE_029")]
    public void QuantizationAudit_RejectsMaximumScaleThatExceedsQuarterPixel()
    {
        var camera = new GameObject("Quantization reference", typeof(Camera)).GetComponent<Camera>();
        try
        {
            camera.pixelRect = new Rect(0, 0, 1920, 1080); camera.transform.position = new Vector3(1000, 0, -10);
            var quads = new DamageTextGlyphQuad[8];
            DamageTextLayout.Build(99999999, _atlas.Freeze(), 1.1234f, new byte[8], quads);
            var request = new DamageTextRequest { EventId = 1, Amount = 99999999, WorldPosition = new Vector3(1000, 0, 0) };
            var maximum = _styles.Critical; maximum.PixelSize = 512; maximum.PopAmplitude = 4;
            float defaultError = 0, maximumError = 0;
            foreach (var quad in quads)
            {
                defaultError = Mathf.Max(defaultError, DamageTextVertexFormats.ScreenError(quad.Min, request, _styles.Normal,
                    24, _styles.Normal.PopDuration * .5f, camera));
                maximumError = Mathf.Max(maximumError, DamageTextVertexFormats.ScreenError(quad.Min, request, maximum,
                    24, maximum.PopDuration * .5f, camera));
            }
            Assert.LessOrEqual(defaultError, .25f); Assert.Greater(maximumError, .25f);
            Assert.AreEqual(DamageTextPositionFormat.Float32, new DamageTextRenderer().RequestedPositionFormat);
            Assert.Throws<ArgumentException>(() => new DamageTextRenderer(geometry: DamageTextGeometry.InstancedQuad,
                positionFormat: DamageTextPositionFormat.Float16));
        }
        finally { UnityEngine.Object.DestroyImmediate(camera.gameObject); }
    }
}
