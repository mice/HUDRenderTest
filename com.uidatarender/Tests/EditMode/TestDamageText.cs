using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

public sealed class TestDamageText
{
    private DamageTextGlyphAtlas _atlas;
    private DamageTextStyles _styles;
    private DamageTextRenderer _renderer;
    private DamageTextConfiguration _configuration;
    private DamageTextGlyph[] _glyphs;
    private Shader _shader;

    [SetUp]
    public void SetUp()
    {
        _shader = Shader.Find("UIData/DamageText");
        Assert.IsNotNull(_shader);
        _atlas = ScriptableObject.CreateInstance<DamageTextGlyphAtlas>();
        _styles = ScriptableObject.CreateInstance<DamageTextStyles>();
        _glyphs = new DamageTextGlyph[10];
        for (int i = 0; i < 10; i++) _glyphs[i] = new DamageTextGlyph
        { Available = true, Size = new Vector2(8, 12), Advance = 10, Uv = new Rect(0, 0, 1, 1) };
        _atlas.Initialize(Texture2D.whiteTexture, 24, _glyphs, "fixture-v1", "white-texture-fixture-v1");
        _configuration = new DamageTextConfiguration { MaximumBuckets = 4, MaximumPendingEvents = 512 };
        _renderer = new DamageTextRenderer(); Prepare();
    }
    [TearDown]
    public void TearDown()
    {
        _renderer?.Dispose(); UnityEngine.Object.DestroyImmediate(_atlas); UnityEngine.Object.DestroyImmediate(_styles);
    }
    private void Prepare() => _renderer.Prepare(_configuration, _atlas, _styles, _shader);
    private static DamageTextRequest Request(uint id, uint amount = 10, float spawn = 0, DamageTextKind kind = DamageTextKind.Normal)
        => new DamageTextRequest { EventId = id, Amount = amount, SpawnTime = spawn, Kind = kind, RandomSeed = id + 17 };

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_001.md
    [Test]
    [Category("UT_DAMAGE_001")]
    public void Spawn_RejectsInvalidInputsWithoutRewritingOrReservingIds()
    {
        Assert.AreEqual(DamageTextSpawnResult.InvalidAmount, _renderer.Spawn(Request(1, 9)));
        Assert.AreEqual(DamageTextSpawnResult.InvalidAmount, _renderer.Spawn(Request(1, 100000000)));
        Assert.AreEqual(DamageTextSpawnResult.InvalidKind, _renderer.Spawn(Request(1, 10, 0, (DamageTextKind)255)));
        Assert.AreEqual(DamageTextSpawnResult.InvalidTime, _renderer.Spawn(Request(1, 10, float.NaN)));
        var request = Request(1); request.WorldPosition.x = float.PositiveInfinity;
        Assert.AreEqual(DamageTextSpawnResult.InvalidPosition, _renderer.Spawn(request));
        Assert.AreEqual(DamageTextSpawnResult.Accepted, _renderer.Spawn(Request(1)));
        Assert.AreEqual(5, _renderer.Statistics.Rejected);
        Assert.AreEqual(2, _renderer.GetRejectionCount(DamageTextSpawnResult.InvalidAmount));
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_002.md
    [Test]
    [Category("UT_DAMAGE_002")]
    public void Layout_CentersCompleteInkAndPreservesVariableAdvances()
    {
        _glyphs[1].Bearing = new Vector2(-2, -3); _glyphs[1].Advance = 6;
        _glyphs[0].Size = new Vector2(11, 16);
        _atlas.Initialize(Texture2D.whiteTexture, 24, _glyphs, "variable-v1", "white-fixture");
        var quads = new DamageTextGlyphQuad[8]; var scratch = new byte[8];
        var snapshot = _atlas.Freeze();
        foreach (uint amount in new uint[] { 10, 99, 100, 99999999 })
        {
            int count = DamageTextLayout.Build(amount, snapshot, 1, scratch, quads);
            Assert.AreEqual(amount == 99999999 ? 8 : amount == 100 ? 3 : 2, count);
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                minX = Mathf.Min(minX, quads[i].Min.x); maxX = Mathf.Max(maxX, quads[i].Max.x);
                minY = Mathf.Min(minY, quads[i].Min.y); maxY = Mathf.Max(maxY, quads[i].Max.y);
            }
            Assert.AreEqual(0, minX + maxX, .00001f); Assert.AreEqual(0, minY + maxY, .00001f);
        }
        DamageTextLayout.Build(100, snapshot, 1, scratch, quads);
        Assert.AreEqual(9, quads[1].Min.x - quads[0].Min.x, .00001f);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_003.md
    [Test]
    [Category("UT_DAMAGE_003")]
    public void Spawn_MissingGlyphIsExplicitAndRejectedIdCanBeRetried()
    {
        _glyphs[9].Available = false;
        _atlas.Initialize(Texture2D.whiteTexture, 24, _glyphs, "missing-nine", "white-fixture"); Prepare();
        Assert.AreEqual(DamageTextSpawnResult.MissingGlyph, _renderer.Spawn(Request(1, 99)));
        Assert.AreEqual(DamageTextSpawnResult.Accepted, _renderer.Spawn(Request(1, 10)));
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_004.md
    [Test]
    [Category("UT_DAMAGE_004")]
    public void Intake_SortsDueEventsAndDefersFutureAndPostCutoffRequests()
    {
        Assert.AreEqual(DamageTextSpawnResult.Accepted, _renderer.Spawn(Request(4, 100, 1)));
        _renderer.Spawn(Request(1)); _renderer.Tick(0, 0);
        _renderer.Spawn(Request(2)); _renderer.Tick(.1f, 0);
        Assert.AreEqual(1, _renderer.Statistics.Published);
        Assert.AreEqual(DamageTextSpawnResult.LateEvent, _renderer.Spawn(Request(0)));
        _renderer.Tick(.1f, 1);
        Assert.AreEqual(2, _renderer.Statistics.Published); Assert.AreEqual(1, _renderer.Statistics.PendingEvents);
        _renderer.Tick(1, 2);
        Assert.AreEqual(3, _renderer.Statistics.Published); Assert.AreEqual(0, _renderer.Statistics.PendingEvents);
        Assert.Throws<ArgumentOutOfRangeException>(() => _renderer.Tick(.9f, 3));
        Assert.AreEqual(DamageTextSpawnResult.LateEvent, _renderer.Spawn(Request(5, 10, .9f)));
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_005.md
    [Test]
    [Category("UT_DAMAGE_005")]
    public void Capacity_ReservesBeforeTickAndDeduplicatesAfterRetirement()
    {
        _configuration.BucketCapacity = 2; _configuration.MaximumBuckets = 1;
        _configuration.MaximumPendingEvents = 2; _configuration.MaximumSessionEvents = 2; Prepare();
        _renderer.Spawn(Request(1)); _renderer.Spawn(Request(2));
        Assert.AreEqual(DamageTextSpawnResult.CapacityExceeded, _renderer.Spawn(Request(3)));
        _renderer.Tick(0, 0); _renderer.Tick(1, 1);
        Assert.AreEqual(0, _renderer.Statistics.ResidentEvents);
        Assert.AreEqual(DamageTextSpawnResult.DuplicateEventId, _renderer.Spawn(Request(1, 10, 1)));
        Assert.AreEqual(DamageTextSpawnResult.CapacityExceeded, _renderer.Spawn(Request(3, 10, 1)));
        Prepare(); Assert.AreEqual(DamageTextSpawnResult.Accepted, _renderer.Spawn(Request(1)));
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_006.md
    [Test]
    [Category("UT_DAMAGE_006")]
    public void BirthBurst_UsesOneUploadPerBucketAndOnlyEffectiveIndices()
    {
        foreach (int count in new[] { 127, 128, 129, 256 })
        {
            Prepare();
            for (uint id = 1; id <= count; id++) Assert.AreEqual(DamageTextSpawnResult.Accepted, _renderer.Spawn(Request(id, 99999999)));
            var stats = _renderer.Tick(0, 0);
            int buckets = (count + 127) / 128;
            Assert.AreEqual(buckets, stats.Buckets); Assert.AreEqual(buckets, stats.VertexUploads);
            Assert.AreEqual(count, stats.Layouts); Assert.AreEqual(count * 8, stats.SubmittedCharacters);
            Assert.AreEqual((Math.Min(128, count) * 8 * 6), _renderer.GetPublishedMesh(0).GetIndexCount(0));
            Assert.AreEqual(68, _renderer.GetPublishedMesh(0).GetVertexBufferStride(0));
            Assert.AreEqual(68, Marshal.SizeOf<DamageTextVertex>());
        }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_007.md
    [Test]
    [Category("UT_DAMAGE_007")]
    public void StableAnimation_ReusesGeometryAndDoesNotRelayoutExistingEvents()
    {
        _renderer.Spawn(Request(1)); _renderer.Tick(0, 0);
        var mesh = _renderer.GetPublishedMesh(0); long revision = _renderer.DrawRevision;
        _renderer.Tick(.1f, 1); _renderer.Tick(.2f, 2);
        Assert.AreSame(mesh, _renderer.GetPublishedMesh(0)); Assert.AreEqual(revision, _renderer.DrawRevision);
        Assert.AreEqual(1, _renderer.Statistics.VertexUploads); Assert.AreEqual(1, _renderer.Statistics.Layouts);
        _renderer.Spawn(Request(2, 10, .2f)); _renderer.Tick(.2f, 3);
        Assert.AreEqual(2, _renderer.Statistics.VertexUploads); Assert.AreEqual(2, _renderer.Statistics.Layouts);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_008.md
    [Test]
    [Category("UT_DAMAGE_008")]
    public void MixedLifetimes_RetainExpiredDigitsAndRecycleWholeBuckets()
    {
        _configuration.BucketCapacity = 2; _configuration.MaximumBuckets = 2; Prepare();
        _renderer.Spawn(Request(1)); _renderer.Spawn(Request(2, 100, 0, DamageTextKind.Healing));
        _renderer.Spawn(Request(3, 99, .4f)); _renderer.Spawn(Request(4, 99, .4f));
        _renderer.Tick(0, 0); _renderer.Tick(.4f, 1);
        _renderer.Tick(.8f, 2);
        Assert.AreEqual(3, _renderer.Statistics.LiveEvents); Assert.AreEqual(2, _renderer.Statistics.ExpiredResidentCharacters);
        long uploads = _renderer.Statistics.VertexUploads;
        _renderer.Tick(1.1f, 3);
        Assert.AreEqual(1, _renderer.Statistics.RecycledBuckets); Assert.AreEqual(1, _renderer.Statistics.Buckets);
        _renderer.Spawn(Request(5, 100, 1.1f)); _renderer.Spawn(Request(6, 100, 1.1f)); _renderer.Tick(1.1f, 4);
        Assert.AreEqual(uploads + 1, _renderer.Statistics.VertexUploads);
        Assert.AreEqual(24, _renderer.GetPublishedMesh(0).GetIndexCount(0)); // Older surviving events remain first.
        _renderer.Tick(3, 5);
        Assert.AreEqual(6, _renderer.Statistics.Ended); Assert.AreEqual(0, _renderer.Statistics.ResidentEvents);
    }

    private sealed class FailingUploader : IDamageTextMeshUploader
    {
        private readonly DamageTextMeshUploader _inner = new DamageTextMeshUploader();
        public int Calls;
        public DamageTextRenderer Owner;
        public bool TestReentrancy;
        public void Upload(Mesh mesh, DamageTextVertex[] vertices, int vertexCount, int indexCount)
        {
            _inner.Upload(mesh, vertices, vertexCount, indexCount); Calls++;
            if (TestReentrancy) Owner.Spawn(Request(999));
            if (Calls == 2) throw new InvalidOperationException("Injected second-bucket upload failure.");
        }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_009.md
    [Test]
    [Category("UT_DAMAGE_009")]
    public void FailedSecondBucket_PreservesFrontAndAcceptedEventsUntilAtomicRecovery()
    {
        _renderer.Dispose(); var uploader = new FailingUploader(); _renderer = new DamageTextRenderer(uploader); Prepare();
        for (uint id = 1; id <= 129; id++) _renderer.Spawn(Request(id));
        _renderer.Tick(0, 0);
        Assert.AreEqual(DamageTextRendererStatus.Failed, _renderer.Status);
        Assert.AreEqual(0, _renderer.Statistics.Published); Assert.AreEqual(129, _renderer.Statistics.PendingEvents);
        Assert.AreEqual(0, _renderer.Statistics.Draws);
        Assert.IsTrue(_renderer.Recover());
        Assert.AreEqual(129, _renderer.Statistics.Published); Assert.AreEqual(129, _renderer.Statistics.Layouts);
        Assert.AreEqual(0, _renderer.Statistics.PendingEvents); Assert.AreEqual(2, _renderer.Statistics.Draws);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_010.md
    [Test]
    [Category("UT_DAMAGE_010")]
    public void ReentrantMutation_FaultsWithoutAcknowledgingPendingEvents()
    {
        _renderer.Dispose(); var uploader = new FailingUploader { TestReentrancy = true };
        _renderer = new DamageTextRenderer(uploader); uploader.Owner = _renderer; Prepare();
        _renderer.Spawn(Request(1)); _renderer.Tick(0, 0);
        Assert.AreEqual(DamageTextRendererStatus.Failed, _renderer.Status);
        Assert.AreEqual(1, _renderer.Statistics.PendingEvents); Assert.AreEqual(0, _renderer.Statistics.Published);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_011.md
    [Test]
    [Category("UT_DAMAGE_011")]
    public void AtlasInvalidation_BlocksDrawingAndRequiresExplicitPreparation()
    {
        _renderer.Spawn(Request(1)); _renderer.Tick(0, 0);
        _atlas.Initialize(Texture2D.whiteTexture, 24, _glyphs, "changed-v2", "changed-fixture");
        Assert.IsFalse(_renderer.CanDraw); _renderer.Tick(.1f, 1);
        Assert.AreEqual(DamageTextRendererStatus.Failed, _renderer.Status); Assert.IsFalse(_renderer.Recover());
        Prepare(); Assert.IsTrue(_renderer.CanDraw);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_012.md
    [Test]
    [Category("UT_DAMAGE_012")]
    public void SlowTick_ConsumesExpiredEventsWithoutUploadingThem()
    {
        _renderer.Spawn(Request(1)); _renderer.Tick(1, 0);
        Assert.AreEqual(1, _renderer.Statistics.MissedPresentation); Assert.AreEqual(1, _renderer.Statistics.Ended);
        Assert.AreEqual(0, _renderer.Statistics.VertexUploads); Assert.AreEqual(0, _renderer.Statistics.ResidentEvents);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_013.md
    [Test]
    [Category("UT_DAMAGE_013")]
    public void Clear_IsRepeatableAndPreservesExternalAssets()
    {
        _renderer.Spawn(Request(1)); _renderer.Tick(0, 0); var mesh = _renderer.GetPublishedMesh(0);
        _renderer.Clear(); _renderer.Clear();
        Assert.IsTrue(mesh == null); Assert.IsNotNull(_atlas.Texture); Assert.IsNotNull(_styles);
        Assert.AreEqual(0, _renderer.Statistics.OwnedMeshes); Prepare();
        Assert.AreEqual(DamageTextSpawnResult.Accepted, _renderer.Spawn(Request(1)));
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_014.md
    [Test]
    [Category("UT_DAMAGE_014")]
    public void Clip_IsSortedImmutableAndHasCanonicalContentHash()
    {
        var input = new[] { Request(3, 99, .2f), Request(2), Request(1) };
        var clip = new DamageTextClip(input, 3, "record-v1", 7);
        var reordered = new DamageTextClip(new[] { input[2], input[0], input[1] }, 3, "record-v1", 7);
        Assert.AreEqual(clip.Hash, reordered.Hash); Assert.AreEqual(1, clip.GetEvent(0).EventId);
        input[2].Amount = 1234; Assert.AreEqual(10, clip.GetEvent(0).Amount);
        Assert.Throws<ArgumentException>(() => new DamageTextClip(new[] { Request(1), Request(1) }, 3, "v1", 0));
        Assert.Throws<ArgumentException>(() => new DamageTextClip(new[] { Request(1, 9) }, 3, "v1", 0));
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_015.md
    [Test]
    [Category("UT_DAMAGE_015")]
    public void Playback_HandlesStartAndSlowIntervalsWithoutLosingEvents()
    {
        using (var playback = new DamageTextPlayback())
        {
            var clip = new DamageTextClip(new[] { Request(1), Request(2, 99, .1f), Request(3, 100, .2f) }, 3, "v1", 0);
            playback.Prepare(clip, _configuration, _atlas, _styles, _shader);
            Assert.AreEqual(1, playback.ConsumedEvents);
            playback.Play(); playback.AdvanceTo(1.3);
            Assert.AreEqual(3, playback.ConsumedEvents); Assert.AreEqual(2, playback.Renderer.Statistics.MissedPresentation);
            Assert.AreEqual(3, playback.Renderer.Statistics.Ended);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_016.md
    [Test]
    [Category("UT_DAMAGE_016")]
    public void Seek_RestoresSameActiveStateAndContinuesAtNextEvent()
    {
        using (var playback = new DamageTextPlayback())
        {
            var clip = new DamageTextClip(new[] { Request(1), Request(2, 99, .1f, DamageTextKind.Healing), Request(3, 100, .2f) }, 3, "v1", 0);
            playback.Prepare(clip, _configuration, _atlas, _styles, _shader);
            playback.AdvanceTo(.15); var expected = playback.Renderer.Statistics;
            playback.AdvanceTo(2); playback.Seek(.15f);
            Assert.AreEqual(expected.LiveEvents, playback.Renderer.Statistics.LiveEvents);
            Assert.AreEqual(expected.LiveCharacters, playback.Renderer.Statistics.LiveCharacters);
            Assert.AreEqual(2, playback.RestoredEvents); Assert.IsTrue(playback.Recovered);
            Assert.IsTrue(playback.Renderer.TryGetEvent(2, out var restored)); Assert.AreEqual(99, restored.Request.Amount);
            playback.AdvanceTo(.2); Assert.AreEqual(1, playback.ConsumedEvents);
            Assert.AreEqual(3, playback.Renderer.Statistics.LiveEvents);
            playback.Seek(.8f); Assert.IsFalse(playback.Renderer.TryGetEvent(1, out _));
        }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_017.md
    [Test]
    [Category("UT_DAMAGE_017")]
    public void PauseStepAndSamplingInterventions_KeepClockAndSegmentBoundariesExplicit()
    {
        using (var playback = new DamageTextPlayback())
        {
            playback.Prepare(new DamageTextClip(new[] { Request(1) }, 3, "v1", 0), _configuration, _atlas, _styles, _shader);
            playback.Advance(.5); Assert.AreEqual(0, playback.PlaybackTime);
            playback.Step(.125f); Assert.AreEqual(.125f, playback.PlaybackTime);
            playback.BeginSampling(0); playback.Advance(.1); playback.SetSpeed(2);
            Assert.IsTrue(playback.LastSegment.Aborted); Assert.AreEqual("speed", playback.LastSegment.Reason);
            Assert.IsFalse(playback.IsFormalSampling);
            playback.Pause(); float frozen = playback.PlaybackTime; playback.Advance(1);
            Assert.AreEqual(frozen, playback.PlaybackTime);
            playback.Restart(); Assert.AreEqual(0, playback.PlaybackTime); Assert.AreEqual(1, playback.ConsumedEvents);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_018.md
    [Test]
    [Category("UT_DAMAGE_018")]
    public void Animation_UsesSeedAndRoundedSharedLifetimeBoundary()
    {
        var normal = _styles.Normal;
        Assert.AreEqual(DamageTextAnimation.Evaluate(.2f, 0, normal, 7), DamageTextAnimation.Evaluate(.2f, 0, normal, 7));
        float end = 1700f + normal.Lifetime;
        Assert.IsFalse(DamageTextAnimation.IsAlive(end, 1700, normal.Lifetime));
        Assert.AreEqual(0, DamageTextAnimation.Evaluate(end, 1700, normal, 7).w);
        Assert.AreEqual(1, DamageTextAnimation.Evaluate(0, 0, normal, 7).z);
        Assert.AreNotEqual(DamageTextAnimation.Spread(7), DamageTextAnimation.Spread(8));
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_019.md
    [Test]
    [Category("UT_DAMAGE_019")]
    public void WarmupInterruption_ExportsAbortedSegmentAndDoesNotResumeSampling()
    {
        using (var playback = new DamageTextPlayback())
        {
            playback.Prepare(new DamageTextClip(new[] { Request(1) }, 3, "v1", 0), _configuration, _atlas, _styles, _shader);
            playback.BeginSampling(5); playback.Play();
            Assert.AreEqual(DamageTextPlaybackState.Warming, playback.State);
            playback.Advance(.01); playback.Pause();
            Assert.IsTrue(playback.LastSegment.Aborted); Assert.AreEqual("warmup: pause", playback.LastSegment.Reason);
            playback.Play(); playback.Advance(.2); Assert.IsFalse(playback.IsFormalSampling);
            playback.BeginSampling(5); playback.Advance(.01); playback.SetSpeed(2);
            Assert.AreEqual("warmup: speed", playback.LastSegment.Reason);
            for (int i = 0; i < 6; i++) playback.Advance(.01);
            Assert.IsFalse(playback.IsFormalSampling, "Changing speed during warmup must not silently restart formal sampling.");
        }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_020.md
    [Test]
    [Category("UT_DAMAGE_020")]
    public void Dispose_ReleasesMeshesEvenIfSegmentObserverFails()
    {
        var playback = new DamageTextPlayback();
        try
        {
            playback.Prepare(new DamageTextClip(new[] { Request(1) }, 3, "v1", 0), _configuration, _atlas, _styles, _shader);
            playback.BeginSampling(0); var mesh = playback.Renderer.GetPublishedMesh(0);
            playback.SegmentEnded += segment => throw new InvalidOperationException("Injected exporter failure.");
            Assert.Throws<InvalidOperationException>(() => playback.Dispose());
            Assert.AreEqual(DamageTextRendererStatus.Disposed, playback.Renderer.Status); Assert.IsTrue(mesh == null);
        }
        finally { playback.Dispose(); }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_021.md
    [Test]
    [Category("UT_DAMAGE_021")]
    public void FailedAppend_KeepsExistingFrontAndPublishesEachHitOnlyOnceAfterRecovery()
    {
        _renderer.Dispose(); var uploader = new FailingUploader(); _renderer = new DamageTextRenderer(uploader); Prepare();
        _renderer.Spawn(Request(1)); _renderer.Tick(0, 0); var front = _renderer.GetPublishedMesh(0);
        _renderer.Spawn(Request(2, 99, .1f)); _renderer.Tick(.1f, 1);
        Assert.AreEqual(DamageTextRendererStatus.Failed, _renderer.Status); Assert.IsTrue(_renderer.CanDraw);
        Assert.AreSame(front, _renderer.GetPublishedMesh(0)); Assert.AreEqual(12, front.GetIndexCount(0));
        Assert.AreEqual(1, _renderer.Statistics.Published);
        Assert.IsTrue(_renderer.TryGetEvent(2, out var pending)); Assert.IsFalse(pending.Published);
        Assert.IsTrue(_renderer.Recover()); Assert.AreEqual(2, _renderer.Statistics.Published);
        Assert.AreEqual(24, _renderer.GetPublishedMesh(0).GetIndexCount(0));
        Assert.IsTrue(_renderer.TryGetEvent(2, out var published)); Assert.AreEqual(.1f, published.FirstPublishedTime);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_022.md
    [Test]
    [Category("UT_DAMAGE_022")]
    public void CameraTrajectory_IsVersionedAndEvaluatedFromTheSameClock()
    {
        var keys = new[]
        {
            new DamageTextCameraKey { Time = 0, Position = new Vector3(0, 0, -10), Rotation = Quaternion.identity, FieldOfView = 60, OrthographicSize = 5 },
            new DamageTextCameraKey { Time = 3, Position = new Vector3(3, 0, -10), Rotation = Quaternion.identity, FieldOfView = 90, OrthographicSize = 6 }
        };
        var clip = new DamageTextClip(new[] { Request(1) }, 3, "camera-v1", 0, keys);
        var cameraObject = new GameObject("Damage camera trajectory fixture", typeof(Camera));
        try
        {
            var camera = cameraObject.GetComponent<Camera>(); clip.EvaluateCamera(1.5f, camera);
            Assert.AreEqual(new Vector3(1.5f, 0, -10), camera.transform.position);
            Assert.AreEqual(75, camera.fieldOfView); Assert.AreEqual(5.5f, camera.orthographicSize);
            keys[1].Position.x = 100; clip.EvaluateCamera(1.5f, camera);
            Assert.AreEqual(1.5f, camera.transform.position.x);
        }
        finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_023.md
    [Test]
    [Category("UT_DAMAGE_023")]
    public void Preparation_FreezesStylesAndConfigurationUntilAnExplicitRestart()
    {
        Color32 original = _styles.Normal.Color;
        var changed = _styles.Normal; changed.Color = new Color32(1, 2, 3, 4); _styles.Normal = changed;
        _configuration.MaximumTime = .1f;
        Assert.AreEqual(DamageTextSpawnResult.Accepted, _renderer.Spawn(Request(1)));
        _renderer.Tick(0, 0); Assert.AreEqual(original, _renderer.GetPublishedMesh(0).colors32[0]);
        _configuration.MaximumTime = 1800; Prepare(); _renderer.Spawn(Request(1)); _renderer.Tick(0, 0);
        Assert.AreEqual(changed.Color, _renderer.GetPublishedMesh(0).colors32[0]);
    }

    // TestRecord: Documentation~/Testing/Unit/DamageText/UT_DAMAGE_024.md
    [Test]
    [Category("UT_DAMAGE_024")]
    public void PlaybackStyleChanges_RequireRestartAndRevalidateTheClipLifetime()
    {
        using (var playback = new DamageTextPlayback())
        {
            playback.Prepare(new DamageTextClip(new[] { Request(1) }, 3, "v1", 0), _configuration, _atlas, _styles, _shader);
            playback.AdvanceTo(.1);
            var original = _styles.Normal; var changed = original; changed.Color = new Color32(3, 4, 5, 255); _styles.Normal = changed;
            Assert.Throws<InvalidOperationException>(() => playback.Seek(.1f));
            Assert.AreEqual(DamageTextPlaybackState.Failed, playback.State);
            Assert.Throws<InvalidOperationException>(() => playback.Step());
            playback.Restart(); Assert.AreEqual(DamageTextPlaybackState.Paused, playback.State);
            Assert.AreEqual(changed.Color, playback.Renderer.GetPublishedMesh(0).colors32[0]);
            changed.Lifetime = 4; _styles.Normal = changed;
            Assert.Throws<ArgumentException>(() => playback.Restart());
            Assert.AreEqual(DamageTextPlaybackState.Failed, playback.State);
            _styles.Normal = original; playback.Restart(); playback.AdvanceTo(3); playback.Pause(); playback.Play();
            Assert.AreEqual(0, playback.PlaybackTime, "Play from Completed must restart even after a redundant Pause.");
        }
    }
}
