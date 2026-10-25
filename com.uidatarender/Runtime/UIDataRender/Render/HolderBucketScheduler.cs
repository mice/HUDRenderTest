using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Replays a complete value snapshot and generates one holder. Implementations own no scheduler resources.</summary>
public interface IHolderStateApplier<TState> where TState : struct
{
    // Batch hooks can open/close stable UIImage query scopes; EndBatch is called even if BeginBatch fails.
    void BeginBatch();
    Vector3 Apply(IUIPrefabHolder holder, in TState state);
    void EndBatch();
}

public enum HolderBucketStrategy { Rotating, SameFrame, SingleBucket }
public enum HolderBucketStatus { Empty, Ready, Faulted, NeedsResync, Disposed }
public enum HolderFlushReason { Initialization, Membership, Structure, Resources, Recovery, Explicit }

public readonly struct HolderBucketHandle
{
    internal readonly object Owner;
    public readonly long Epoch;
    public readonly int Index;
    internal HolderBucketHandle(object owner, long epoch, int index) { Owner = owner; Epoch = epoch; Index = index; }
}

public readonly struct HolderRequestProgress
{
    public readonly long RequestedVersion, CompletedVersion;
    public readonly int FirstPendingFrame, LatestRequestFrame, CompletedFrame;
    public bool Pending => RequestedVersion != CompletedVersion;
    internal HolderRequestProgress(long requested, long completed, int first, int latest, int frame)
    { RequestedVersion = requested; CompletedVersion = completed; FirstPendingFrame = first; LatestRequestFrame = latest; CompletedFrame = frame; }
}

public struct HolderBucketTickResult
{
    public int Frame, RefreshedBuckets, CompletedHolders, PendingHolders, MeshCount;
    public int MaxFirstPendingAge, MaxLatestValueAge;
    public int MaxCompletedFirstPendingAge, MaxCompletedLatestValueAge;
    public double GenerationMs, MergeMs, UpdateMs;
    public HolderBucketStatus Status;
    public bool IsBarrier;
    public HolderFlushReason BarrierReason;
}

public struct HolderBucketStorage
{
    public int Renderers, Meshes, Materials;
    public long UnityObjectBytes;
}

public interface IHolderDrawSource
{
    bool CanDraw { get; }
    long DrawRevision { get; }
    int RecordDraws(CommandBuffer commands, Matrix4x4 matrix, int shaderPass = 0);
}

/// <summary>
/// Explicit main-thread value queue. Only ordinary Rotating Tick is limited to one 128-holder bucket.
/// TState must contain values or immutable, externally retained assets, never mutable shared template state.
/// Enrolled holders must not be generated or disposed outside the scheduler until replacement/disposal.
/// </summary>
public sealed class HolderBucketScheduler<TState> : IDisposable, IHolderDrawSource where TState : struct
{
    public const int Capacity = 128;
    public const int MaximumHolders = 256;
    private sealed class Entry
    {
        public IUIPrefabHolder Holder;
        public TState State;
        public long Requested, Completed;
        public int FirstPending = -1, LatestRequest = -1, CompletedFrame = -1;
        public bool Dirty => Requested != Completed;
    }

    private sealed class Bucket : IDisposable
    {
        public readonly IUIPrefabHolder[] Holders;
        public readonly Dictionary<IUIPrefabHolder, Vector3> Positions;
        public HolderBatchRenderer Front, Back;
        public readonly int Start;
        public bool Published;
        public Bucket(Entry[] entries, int start, int count, int maxSlots)
        {
            Start = start;
            Holders = new IUIPrefabHolder[count];
            Positions = new Dictionary<IUIPrefabHolder, Vector3>(count);
            for (int i = 0; i < count; i++)
            { Holders[i] = entries[start + i].Holder; Positions.Add(Holders[i], Vector3.zero); }
            Front = new HolderBatchRenderer(maxSlots, preserveOrder: true);
            Back = new HolderBatchRenderer(maxSlots, preserveOrder: true);
        }
        public void Publish() { var old = Front; Front = Back; Back = old; Published = true; }
        public void Dispose() { Front.Dispose(); Back.Dispose(); Positions.Clear(); Array.Clear(Holders, 0, Holders.Length); }
    }

    private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
    private readonly object _identity = new object();
    private readonly int _maxSlots;
    private IHolderStateApplier<TState> _applier;
    private Func<Material> _createMaterial;
    private Action<Material, RenderBatch> _bindTextures;
    private Func<int> _resourceVersion;
    private Entry[] _entries = Array.Empty<Entry>();
    private Bucket[] _buckets = Array.Empty<Bucket>();
    private long _epoch;
    private int _cursor, _lastFrame = -1, _lastTick = -1, _publishedResourceVersion;
    private bool _executing;
    public HolderBucketStrategy Strategy { get; }
    public HolderBucketStatus Status { get; private set; } = HolderBucketStatus.Empty;
    public Exception LastError { get; private set; }
    public HolderBucketTickResult LastResult { get; private set; }
    public long DrawRevision { get; private set; }
    public int HolderCount => _entries.Length;
    public int BucketCount => _buckets.Length;
    public int MeshCount
    {
        get { int count = 0; for (int b = 0; b < _buckets.Length; b++) if (_buckets[b].Published) count += _buckets[b].Front.BatchCount; return count; }
    }

    public HolderBucketScheduler(int maxSlots, IHolderStateApplier<TState> applier,
        Func<Material> createMaterial, Action<Material, RenderBatch> bindTextures,
        Func<int> resourceVersion, HolderBucketStrategy strategy = HolderBucketStrategy.Rotating)
    {
        if (maxSlots < 1) throw new ArgumentOutOfRangeException(nameof(maxSlots));
        if (!Enum.IsDefined(typeof(HolderBucketStrategy), strategy)) throw new ArgumentOutOfRangeException(nameof(strategy));
        _maxSlots = maxSlots;
        _applier = applier ?? throw new ArgumentNullException(nameof(applier));
        _createMaterial = createMaterial ?? throw new ArgumentNullException(nameof(createMaterial));
        _bindTextures = bindTextures ?? throw new ArgumentNullException(nameof(bindTextures));
        _resourceVersion = resourceVersion ?? throw new ArgumentNullException(nameof(resourceVersion));
        Strategy = strategy;
    }

    public HolderBucketHandle GetHandle(int index)
    {
        CheckAccess();
        if ((uint)index >= _entries.Length) throw new ArgumentOutOfRangeException(nameof(index));
        return new HolderBucketHandle(_identity, _epoch, index);
    }

    public HolderRequestProgress GetProgress(HolderBucketHandle handle)
    {
        CheckAccess();
        var e = Resolve(handle);
        return new HolderRequestProgress(e.Requested, e.Completed, e.FirstPending, e.LatestRequest, e.CompletedFrame);
    }

    /// <summary>Stages all new members before replacing the visible set. Failed replacement leaves old handles valid.</summary>
    public HolderBucketTickResult ReplaceMembers(IReadOnlyList<IUIPrefabHolder> holders, IReadOnlyList<TState> initialStates, int frame)
    {
        CheckAccess();
        if (holders == null || initialStates == null) throw new ArgumentNullException();
        if (holders.Count < 1 || holders.Count > MaximumHolders || initialStates.Count != holders.Count)
            throw new ArgumentException("Provide 1..256 ordered holders and one state per holder.");
        for (int i = 0; i < holders.Count; i++)
        {
            if (holders[i] == null) throw new ArgumentException("Null holder.");
            for (int j = 0; j < i; j++) if (ReferenceEquals(holders[i], holders[j])) throw new ArgumentException("Duplicate holder.");
        }
        CheckFrame(frame);
        long nextEpoch = checked(_epoch + 1);
        var entries = new Entry[holders.Count];
        for (int i = 0; i < entries.Length; i++) entries[i] = new Entry { Holder = holders[i], State = initialStates[i] };
        int size = Strategy == HolderBucketStrategy.SingleBucket ? MaximumHolders : Capacity;
        var buckets = new Bucket[(entries.Length + size - 1) / size];
        var result = BeginResult(frame, true, _entries.Length == 0 ? HolderFlushReason.Initialization : HolderFlushReason.Membership);
        long start = Stopwatch.GetTimestamp();
        _executing = true;
        int version = _publishedResourceVersion;
        try
        {
            version = _resourceVersion();
            for (int b = 0; b < buckets.Length; b++)
            {
                buckets[b] = new Bucket(entries, b * size, Math.Min(size, entries.Length - b * size), _maxSlots);
                Stage(buckets[b], entries, true, version, ref result);
                // Warm both allocations now; neither version is visible until all buckets succeed.
                buckets[b].Publish();
                Rebuild(buckets[b], ref result);
            }
            EnsureVersion(version);
            for (int b = 0; b < _buckets.Length; b++) _buckets[b].Dispose();
            _entries = entries;
            _buckets = buckets;
            _epoch = nextEpoch;
            _cursor = 0;
            _lastTick = frame;
            _publishedResourceVersion = version;
            DrawRevision++;
            Status = HolderBucketStatus.Ready;
            LastError = null;
            result.RefreshedBuckets = buckets.Length;
        }
        catch (Exception error)
        {
            for (int b = 0; b < buckets.Length; b++) buckets[b]?.Dispose();
            SetFailure(error, version);
        }
        finally { _executing = false; }
        return Finish(result, start);
    }

    public long Submit(HolderBucketHandle handle, in TState state, int frame)
    {
        CheckAccess();
        var e = Resolve(handle);
        CheckFrame(frame);
        long version = checked(e.Requested + 1);
        if (!e.Dirty) e.FirstPending = frame;
        e.LatestRequest = frame;
        e.State = state;
        e.Requested = version;
        return version;
    }

    public HolderBucketTickResult Tick(int frame)
    {
        CheckAccess();
        CheckFrame(frame);
        var result = BeginResult(frame, false, default);
        long start = Stopwatch.GetTimestamp();
        if (_lastTick == frame) return Finish(result, start);
        _lastTick = frame;
        CheckResourceVersion();
        if (Status != HolderBucketStatus.Ready) return Finish(result, start);
        _executing = true;
        int version = _publishedResourceVersion;
        try
        {
            int first = _cursor;
            for (int offset = 0; offset < _buckets.Length; offset++)
            {
                int b = (first + offset) % _buckets.Length;
                var bucket = _buckets[b];
                if (!IsDirty(bucket)) continue;
                Stage(bucket, _entries, false, version, ref result);
                bucket.Publish();
                DrawRevision++;
                Complete(bucket, frame, ref result);
                result.RefreshedBuckets++;
                _cursor = (b + 1) % _buckets.Length;
                if (Strategy == HolderBucketStrategy.Rotating) break;
            }
        }
        catch (Exception error) { SetFailure(error, version); }
        finally { _executing = false; }
        return Finish(result, start);
    }

    /// <summary>Regenerates every latest snapshot; required after hierarchy/resource changes or failure.</summary>
    public HolderBucketTickResult FlushAll(HolderFlushReason reason, int frame)
    {
        CheckAccess();
        CheckFrame(frame);
        var result = BeginResult(frame, true, reason);
        long start = Stopwatch.GetTimestamp();
        if (_entries.Length == 0) return Finish(result, start);
        _executing = true;
        int version = _publishedResourceVersion;
        try
        {
            version = _resourceVersion();
            for (int b = 0; b < _buckets.Length; b++) Stage(_buckets[b], _entries, true, version, ref result);
            EnsureVersion(version);
            for (int b = 0; b < _buckets.Length; b++)
            {
                _buckets[b].Publish();
                Complete(_buckets[b], frame, ref result);
            }
            _publishedResourceVersion = version;
            DrawRevision++;
            _lastTick = frame;
            Status = HolderBucketStatus.Ready;
            LastError = null;
            result.RefreshedBuckets = _buckets.Length;
        }
        catch (Exception error) { SetFailure(error, version); }
        finally { _executing = false; }
        return Finish(result, start);
    }

    public bool CanDraw
    {
        get
        {
            if (Status == HolderBucketStatus.Disposed) return false;
            CheckAccess();
            CheckResourceVersion();
            return _buckets.Length != 0 && (Status == HolderBucketStatus.Ready || Status == HolderBucketStatus.Faulted);
        }
    }

    public int RecordDraws(CommandBuffer commands, Matrix4x4 matrix, int shaderPass = 0)
    {
        if (commands == null) throw new ArgumentNullException(nameof(commands));
        if (!CanDraw) return 0;
        int count = 0;
        for (int b = 0; b < _buckets.Length; b++)
            if (_buckets[b].Published) count += _buckets[b].Front.RecordDraws(commands, matrix, shaderPass);
        return count;
    }

    /// <summary>Read-only inspection of the current visible mesh. Callers must not mutate or destroy it.</summary>
    public Mesh GetPublishedMesh(int bucket, int mesh)
    {
        CheckAccess();
        if ((uint)bucket >= _buckets.Length) throw new ArgumentOutOfRangeException(nameof(bucket));
        return _buckets[bucket].Published ? _buckets[bucket].Front.GetBatchMesh(mesh) : null;
    }

    /// <summary>Includes front and back. UnityObjectBytes excludes managed queues, scratch buffers and external holders/assets.</summary>
    public HolderBucketStorage GetStorage()
    {
        CheckAccess();
        var storage = new HolderBucketStorage { Renderers = _buckets.Length * 2 };
        for (int b = 0; b < _buckets.Length; b++)
        {
            var bucket = _buckets[b];
            storage.Meshes += bucket.Front.OwnedMeshCount + bucket.Back.OwnedMeshCount;
            storage.Materials += bucket.Front.OwnedMaterialCount + bucket.Back.OwnedMaterialCount;
            storage.UnityObjectBytes += bucket.Front.GetOwnedObjectBytes() + bucket.Back.GetOwnedObjectBytes();
        }
        return storage;
    }

    private void Stage(Bucket bucket, Entry[] entries, bool all, int version, ref HolderBucketTickResult result)
    {
        long start = Stopwatch.GetTimestamp();
        try
        {
            _applier.BeginBatch();
            for (int i = 0; i < bucket.Holders.Length; i++)
            {
                var e = entries[bucket.Start + i];
                if (all || e.Dirty) bucket.Positions[e.Holder] = _applier.Apply(e.Holder, in e.State);
            }
        }
        finally { _applier.EndBatch(); result.GenerationMs += Milliseconds(start); }
        EnsureVersion(version);
        Rebuild(bucket, ref result);
        EnsureVersion(version);
    }

    private void Rebuild(Bucket bucket, ref HolderBucketTickResult result)
    {
        long start = Stopwatch.GetTimestamp();
        try { bucket.Back.Rebuild(bucket.Holders, bucket.Positions, _createMaterial, _bindTextures); }
        finally { result.MergeMs += Milliseconds(start); }
    }

    private void Complete(Bucket bucket, int frame, ref HolderBucketTickResult result)
    {
        for (int i = 0; i < bucket.Holders.Length; i++)
        {
            var e = _entries[bucket.Start + i];
            if (!e.Dirty) continue;
            result.CompletedHolders++;
            result.MaxCompletedFirstPendingAge = Math.Max(result.MaxCompletedFirstPendingAge, frame - e.FirstPending);
            result.MaxCompletedLatestValueAge = Math.Max(result.MaxCompletedLatestValueAge, frame - e.LatestRequest);
            e.Completed = e.Requested;
            e.CompletedFrame = frame;
            e.FirstPending = -1;
        }
    }

    private bool IsDirty(Bucket bucket)
    {
        for (int i = 0; i < bucket.Holders.Length; i++) if (_entries[bucket.Start + i].Dirty) return true;
        return false;
    }

    private void CheckResourceVersion()
    {
        if (_buckets.Length != 0 && _resourceVersion() != _publishedResourceVersion) Status = HolderBucketStatus.NeedsResync;
    }

    private void EnsureVersion(int expected)
    {
        if (_resourceVersion() != expected) throw new InvalidOperationException("Resource mapping changed during generation. FlushAll is required.");
    }

    private void SetFailure(Exception error, int version)
    {
        LastError = error;
        Status = _resourceVersion() != version || (_buckets.Length != 0 && _resourceVersion() != _publishedResourceVersion)
            ? HolderBucketStatus.NeedsResync : HolderBucketStatus.Faulted;
    }

    private Entry Resolve(HolderBucketHandle handle)
    {
        if (!ReferenceEquals(handle.Owner, _identity) || handle.Epoch != _epoch || (uint)handle.Index >= _entries.Length)
            throw new ArgumentException("Stale or foreign holder handle.", nameof(handle));
        return _entries[handle.Index];
    }

    private void CheckAccess()
    {
        if (Thread.CurrentThread.ManagedThreadId != _thread) throw new InvalidOperationException("Use the scheduler on its owning main thread.");
        if (Status == HolderBucketStatus.Disposed) throw new ObjectDisposedException(nameof(HolderBucketScheduler<TState>));
        if (_executing) throw new InvalidOperationException("Scheduler mutation and inspection cannot reenter generation.");
    }

    private void CheckFrame(int frame)
    {
        if (frame < 0 || frame < _lastFrame) throw new ArgumentOutOfRangeException(nameof(frame), "Frames must not move backwards.");
        _lastFrame = frame;
    }

    private static double Milliseconds(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    private static HolderBucketTickResult BeginResult(int frame, bool barrier, HolderFlushReason reason)
        => new HolderBucketTickResult { Frame = frame, IsBarrier = barrier, BarrierReason = reason };

    private HolderBucketTickResult Finish(HolderBucketTickResult result, long start)
    {
        for (int i = 0; i < _entries.Length; i++)
        {
            var e = _entries[i];
            if (!e.Dirty) continue;
            result.PendingHolders++;
            result.MaxFirstPendingAge = Math.Max(result.MaxFirstPendingAge, result.Frame - e.FirstPending);
            result.MaxLatestValueAge = Math.Max(result.MaxLatestValueAge, result.Frame - e.LatestRequest);
        }
        result.MeshCount = MeshCount;
        result.Status = Status;
        result.UpdateMs = Milliseconds(start);
        LastResult = result;
        return result;
    }

    public void Dispose()
    {
        if (Status == HolderBucketStatus.Disposed) return;
        CheckAccess();
        for (int b = 0; b < _buckets.Length; b++) _buckets[b].Dispose();
        _buckets = Array.Empty<Bucket>();
        _entries = Array.Empty<Entry>();
        _applier = null;
        _createMaterial = null;
        _bindTextures = null;
        _resourceVersion = null;
        LastError = null;
        Status = HolderBucketStatus.Disposed;
    }
}
