using System;
using UnityEngine;

[Serializable]
public struct DamageTextPlaybackSegment
{
    public bool Formal, Aborted, Recovered;
    public string Reason, ClipHash, AtlasHash, StyleHash, ConfigurationHash;
    public float StartTime, EndTime;
    public long ConsumedEvents;
    public DamageTextStatistics Statistics;
}

/// <summary>Owns the only clock and event cursor. Unity Update is an adapter, never a second clock.</summary>
public sealed class DamageTextPlayback : IDisposable
{
    private DamageTextClip _clip;
    private DamageTextConfiguration _configuration;
    private DamageTextGlyphAtlas _atlas;
    private DamageTextStyles _styles;
    private DamageTextStyle[] _frozenStyles;
    private Shader _shader;
    private Camera _camera;
    private int _cursor, _frame, _warmupRemaining;
    private double _preciseTime;
    private bool _formal, _samplingRequested, _recovered, _disposed;
    private DamageTextPlaybackSegment _segment;
    public DamageTextRenderer Renderer { get; }
    public DamageTextPlaybackState State { get; private set; }
    public float PlaybackTime => (float)_preciseTime;
    public float Speed { get; private set; } = 1;
    public long ConsumedEvents { get; private set; }
    public int RestoredEvents { get; private set; }
    public string Failure { get; private set; }
    public bool IsFormalSampling => _formal;
    public bool Recovered => _recovered;
    public DamageTextClip Clip => _clip;
    public DamageTextPlaybackSegment LastSegment { get; private set; }
    public event Action<DamageTextPlaybackSegment> SegmentEnded;

    public DamageTextPlayback(IDamageTextMeshUploader uploader = null, DamageTextGeometry geometry = DamageTextGeometry.PersistentMesh,
        DamageTextPositionFormat positionFormat = DamageTextPositionFormat.Float32, IDamageTextVertexFormatSupport formatSupport = null)
    { Renderer = new DamageTextRenderer(uploader, geometry, positionFormat, formatSupport); }

    public void Prepare(DamageTextClip clip, DamageTextConfiguration configuration, DamageTextGlyphAtlas atlas,
        DamageTextStyles styles, Shader shader, Camera camera = null)
    {
        CheckNotDisposed(); AbortSampling("prepare"); State = DamageTextPlaybackState.Preparing;
        try
        {
            if (clip == null || configuration == null || atlas == null || styles == null || shader == null) throw new ArgumentNullException();
            configuration = configuration.Copy(); configuration.Validate();
            if (clip.Duration > configuration.MaximumTime || clip.Count > configuration.MaximumSessionEvents)
                throw new ArgumentException("Clip exceeds configured session capacity or duration.");
            var atlasSnapshot = atlas.Freeze(); var frozenStyles = styles.Freeze(out _);
            int simultaneous = 0; float lastSpawn = -1;
            for (int i = 0; i < clip.Count; i++)
            {
                var request = clip.GetEvent(i);
                if (!atlasSnapshot.Contains(request.Amount) || request.SpawnTime + frozenStyles[(int)request.Kind].Lifetime > clip.Duration)
                    throw new ArgumentException("Clip has missing glyphs or an event ending after its duration.");
                simultaneous = request.SpawnTime == lastSpawn ? simultaneous + 1 : 1; lastSpawn = request.SpawnTime;
                if (simultaneous > configuration.MaximumPendingEvents || simultaneous > configuration.BucketCapacity * configuration.MaximumBuckets)
                    throw new ArgumentException("A simultaneous clip burst exceeds configured intake capacity.");
            }
            _clip = clip; _configuration = configuration; _atlas = atlas; _styles = styles; _shader = shader; _camera = camera;
            _frozenStyles = frozenStyles;
            ResetRenderer(); _recovered = false; RestoredEvents = 0;
            if (!DispatchAndTick(0)) return;
            State = DamageTextPlaybackState.Paused;
        }
        catch (Exception error) { Fail(error); throw; }
    }

    private void ResetRenderer()
    {
        Renderer.Prepare(_configuration, _atlas, _styles, _shader);
        if (_camera != null) Renderer.BindCamera(_camera);
        _cursor = _frame = 0; _preciseTime = 0; ConsumedEvents = 0; Failure = null;
    }

    public void Play()
    {
        EnsurePrepared();
        if (State == DamageTextPlaybackState.Warming || State == DamageTextPlaybackState.Sampling) return;
        if (State == DamageTextPlaybackState.Completed) Restart();
        if (State == DamageTextPlaybackState.Failed) throw new InvalidOperationException(Failure);
        State = DamageTextPlaybackState.Sampling;
    }
    public void Pause()
    {
        EnsurePrepared(); AbortSampling("pause");
        if (State != DamageTextPlaybackState.Failed && State != DamageTextPlaybackState.Completed) State = DamageTextPlaybackState.Paused;
    }
    public void BeginSampling(int warmupFrames = 60)
    {
        EnsurePrepared();
        if (warmupFrames < 0) throw new ArgumentOutOfRangeException(nameof(warmupFrames));
        Restart(); _warmupRemaining = warmupFrames; _samplingRequested = true;
        State = warmupFrames == 0 ? DamageTextPlaybackState.Sampling : DamageTextPlaybackState.Warming;
        if (warmupFrames == 0) StartSegment();
    }
    private void StartSegment()
    {
        _formal = true; _samplingRequested = false;
        _segment = new DamageTextPlaybackSegment
        {
            Formal = true, Recovered = _recovered, StartTime = PlaybackTime,
            ClipHash = _clip.Hash, AtlasHash = Renderer.AtlasHash, StyleHash = Renderer.StyleHash,
            ConfigurationHash = Renderer.ConfigurationHash
        };
    }
    public void Advance(double elapsedSeconds)
    {
        EnsurePrepared();
        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (State != DamageTextPlaybackState.Sampling && State != DamageTextPlaybackState.Warming) return;
        AdvanceTo(Math.Min(_clip.Duration, _preciseTime + elapsedSeconds * Speed));
    }
    public void AdvanceTo(double time)
    {
        EnsurePrepared();
        if (double.IsNaN(time) || double.IsInfinity(time) || time < _preciseTime || time > _clip.Duration)
            throw new ArgumentOutOfRangeException(nameof(time), "Use Seek for backwards playback.");
        if (State == DamageTextPlaybackState.Failed) throw new InvalidOperationException(Failure);
        _preciseTime = time;
        if (!DispatchAndTick((float)time)) return;
        if (State == DamageTextPlaybackState.Warming && --_warmupRemaining <= 0)
        { State = DamageTextPlaybackState.Sampling; StartSegment(); }
        if (PlaybackTime >= _clip.Duration)
        {
            if (_samplingRequested) AbortSampling("clip completed before warmup");
            else FinishSegment(false, "completed");
            State = DamageTextPlaybackState.Completed;
        }
    }
    private bool DispatchAndTick(float time)
    {
        try
        {
            while (_cursor < _clip.Count && _clip.GetEvent(_cursor).SpawnTime <= time)
            {
                var request = _clip.GetEvent(_cursor);
                var result = Renderer.Spawn(request);
                ConsumedEvents++; _cursor++;
                if (result != DamageTextSpawnResult.Accepted)
                    throw new InvalidOperationException("Event " + request.EventId + " rejected: " + result);
            }
            Renderer.Tick(time, _frame++);
            if (Renderer.Status != DamageTextRendererStatus.Ready) throw Renderer.LastError;
            _clip.EvaluateCamera(time, _camera);
            return true;
        }
        catch (Exception error) { Fail(error); return false; }
    }

    public void Step(float seconds = 1f / 60f)
    {
        EnsurePrepared();
        if (State == DamageTextPlaybackState.Failed) throw new InvalidOperationException(Failure);
        if (!DamageTextRequest.Finite(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        AbortSampling("step"); State = DamageTextPlaybackState.Paused;
        AdvanceTo(Math.Min(_clip.Duration, _preciseTime + seconds));
    }
    public void SetSpeed(float speed)
    {
        EnsurePrepared();
        if (!DamageTextRequest.Finite(speed) || speed <= 0 || speed > 8) throw new ArgumentOutOfRangeException(nameof(speed));
        if (speed == Speed) return;
        AbortSampling("speed"); Speed = speed;
    }
    public void Seek(float time)
    {
        EnsurePrepared();
        if (!DamageTextRequest.Finite(time) || time < 0 || time > _clip.Duration) throw new ArgumentOutOfRangeException(nameof(time));
        AbortSampling("seek"); State = DamageTextPlaybackState.Preparing;
        try
        {
            _styles.Freeze(out string currentStyleHash);
            if (currentStyleHash != Renderer.StyleHash || _atlas.Freeze().Hash != Renderer.AtlasHash)
                throw new InvalidOperationException("Playback resources changed. Restart the preview before restoring history.");
            ResetRenderer(); RestoredEvents = 0; _recovered = true;
            while (_cursor < _clip.Count && _clip.GetEvent(_cursor).SpawnTime <= time)
            {
                var request = _clip.GetEvent(_cursor++);
                if (!DamageTextAnimation.IsAlive(time, request.SpawnTime, _frozenStyles[(int)request.Kind].Lifetime)) continue;
                var result = Renderer.Spawn(request);
                if (result != DamageTextSpawnResult.Accepted) throw new InvalidOperationException("Restore rejected: " + result);
                RestoredEvents++;
            }
            Renderer.Tick(time, _frame++);
            if (Renderer.Status != DamageTextRendererStatus.Ready) throw Renderer.LastError;
            _preciseTime = time; _clip.EvaluateCamera(time, _camera);
            State = time == _clip.Duration ? DamageTextPlaybackState.Completed : DamageTextPlaybackState.Paused;
        }
        catch (Exception error) { Fail(error); throw; }
    }
    public void Restart()
    {
        EnsurePrepared(); AbortSampling("restart");
        Prepare(_clip, _configuration, _atlas, _styles, _shader, _camera);
    }
    public void AbortSampling(string reason)
    {
        bool wasWarming = State == DamageTextPlaybackState.Warming;
        if (_samplingRequested)
        {
            StartSegment();
            reason = "warmup: " + reason;
        }
        FinishSegment(true, reason);
        if (wasWarming) State = DamageTextPlaybackState.Sampling; // Continue preview; only BeginSampling may start another formal segment.
    }
    private void FinishSegment(bool aborted, string reason)
    {
        if (!_formal) return;
        _formal = false; _segment.Aborted = aborted; _segment.Reason = reason;
        _segment.EndTime = PlaybackTime; _segment.ConsumedEvents = ConsumedEvents; _segment.Statistics = Renderer.Statistics;
        LastSegment = _segment; SegmentEnded?.Invoke(_segment);
    }
    private void Fail(Exception error)
    {
        Failure = error.Message; AbortSampling("failure: " + Failure); State = DamageTextPlaybackState.Failed;
    }
    public void Stop()
    {
        CheckNotDisposed();
        try { AbortSampling("stop"); }
        finally
        {
            Renderer.Clear(); State = DamageTextPlaybackState.Idle;
            _clip = null; _camera = null; _preciseTime = 0; _cursor = 0; ConsumedEvents = 0;
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        try { Stop(); }
        finally { Renderer.Dispose(); _disposed = true; }
    }
    private void EnsurePrepared()
    {
        CheckNotDisposed(); if (_clip == null) throw new InvalidOperationException("Prepare a playback clip first.");
    }
    private void CheckNotDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(DamageTextPlayback)); }
}
