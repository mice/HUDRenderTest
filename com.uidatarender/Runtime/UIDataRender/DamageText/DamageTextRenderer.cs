using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Upload seam for platform adapters. A failure must never publish any staged bucket.</summary>
public interface IDamageTextMeshUploader
{
    void Upload(Mesh mesh, DamageTextVertex[] vertices, int vertexCount, int indexCount);
}

public interface IDamageTextHalfMeshUploader
{
    void Upload(Mesh mesh, DamageTextHalfVertex[] vertices, int vertexCount, int indexCount);
}

public sealed class DamageTextMeshUploader : IDamageTextMeshUploader, IDamageTextHalfMeshUploader
{
    public void Upload(Mesh mesh, DamageTextVertex[] vertices, int vertexCount, int indexCount)
    {
        mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds);
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
        { vertexCount = vertexCount }, MeshUpdateFlags.DontRecalculateBounds);
    }
    public void Upload(Mesh mesh, DamageTextHalfVertex[] vertices, int vertexCount, int indexCount)
    {
        mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds);
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
        { vertexCount = vertexCount }, MeshUpdateFlags.DontRecalculateBounds);
    }
}

/// <summary>Main-thread event renderer. Spawn reserves; Tick atomically publishes the cutoff.</summary>
public sealed class DamageTextRenderer : IDisposable, IHolderDrawSource
{
    private sealed class VertexLayoutException : InvalidOperationException
    { public VertexLayoutException() : base("Damage-text vertex layout does not match the Shader protocol.") { } }
    private sealed class Entry
    {
        public DamageTextEventState State;
        public DamageTextStyle Style;
        public readonly DamageTextGlyphQuad[] Layout = new DamageTextGlyphQuad[8];
        public int Bucket = -1;
    }
    private sealed class Bucket
    {
        public Mesh Front, Back;
        public DamageTextInstanceBatch InstanceFront, InstanceBack;
        public int[] Entries;
        public int Count, PublishedCount, Characters;
        public bool Dirty;
    }
    private sealed class PendingComparer : IComparer<int>
    {
        private readonly Entry[] _entries;
        public PendingComparer(Entry[] entries) { _entries = entries; }
        public int Compare(int a, int b) => DamageTextRequest.Compare(_entries[a].State.Request, _entries[b].State.Request);
    }

    private readonly IDamageTextMeshUploader _uploader;
    private readonly IDamageTextVertexFormatSupport _formatSupport;
    private readonly int _thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
    private DamageTextConfiguration _configuration;
    private DamageTextGlyphAtlas.Snapshot _atlas;
    private DamageTextStyle[] _styles;
    private Material _material;
    private Mesh _instanceQuad;
    private Matrix4x4[] _instanceMatrices;
    private Entry[] _entries;
    private Bucket[] _pool;
    private int[] _freeEntries;
    private int _freeEntryCount;
    private List<int> _pending, _order;
    private HashSet<uint> _seen;
    private PendingComparer _comparer;
    private DamageTextVertex[] _vertices;
    private DamageTextHalfVertex[] _halfVertices;
    private readonly byte[] _digits = new byte[8];
    private readonly long[] _rejections = new long[Enum.GetValues(typeof(DamageTextSpawnResult)).Length];
    private int _lastFrame = -1;
    private float _time;
    private bool _executing, _hasCommittedKey;
    private DamageTextRequest _committedKey;
    private DamageTextStatistics _statistics;
    private DamageTextCameraPass _cameraPass;

    public DamageTextRendererStatus Status { get; private set; }
    public Exception LastError { get; private set; }
    public long DrawRevision { get; private set; }
    public string AtlasHash => _atlas?.Hash;
    public string StyleHash { get; private set; }
    public string ConfigurationHash { get; private set; }
    public float PlaybackTime => _time;
    public bool CanDraw => _material != null && _atlas != null && _atlas.IsValid && Status != DamageTextRendererStatus.Disposed;
    public DamageTextStatistics Statistics => _statistics;
    public DamageTextCameraPass CameraPass => _cameraPass;
    public DamageTextGeometry Geometry { get; }
    public DamageTextPositionFormat RequestedPositionFormat { get; }
    public DamageTextPositionFormat EffectivePositionFormat { get; private set; }
    public string FormatReason { get; private set; }
    public string VertexLayout { get; private set; }
    public int ShaderPassCount => _material != null ? _material.passCount : 0;
    public string Scheme => Geometry == DamageTextGeometry.InstancedQuad ? "A-SharedQuad-Float32" : "B-PersistentMesh-" + EffectivePositionFormat;

    public DamageTextRenderer(IDamageTextMeshUploader uploader = null, DamageTextGeometry geometry = DamageTextGeometry.PersistentMesh,
        DamageTextPositionFormat positionFormat = DamageTextPositionFormat.Float32, IDamageTextVertexFormatSupport formatSupport = null)
    {
        if (!Enum.IsDefined(typeof(DamageTextGeometry), geometry)) throw new ArgumentOutOfRangeException(nameof(geometry));
        if (!Enum.IsDefined(typeof(DamageTextPositionFormat), positionFormat)) throw new ArgumentOutOfRangeException(nameof(positionFormat));
        if (geometry == DamageTextGeometry.InstancedQuad && positionFormat != DamageTextPositionFormat.Float32)
            throw new ArgumentException("Local FP16 is a B-only experiment.");
        _uploader = uploader ?? new DamageTextMeshUploader(); Geometry = geometry;
        RequestedPositionFormat = positionFormat; _formatSupport = formatSupport ?? new DamageTextVertexFormatSupport();
    }

    public void Prepare(DamageTextConfiguration configuration, DamageTextGlyphAtlas atlas,
        DamageTextStyles styles, Shader shader)
    {
        CheckAccess();
        if (configuration == null || atlas == null || styles == null || shader == null) throw new ArgumentNullException();
        configuration = configuration.Copy(); configuration.Validate();
        var snapshot = atlas.Freeze();
        var frozenStyles = styles.Freeze(out var styleHash);
        Clear();
        _configuration = configuration; _atlas = snapshot; _styles = frozenStyles; StyleHash = styleHash;
        ConfigurationHash = DamageTextHash.Compute(writer =>
        {
            writer.Write("damage-config-v1"); writer.Write(configuration.BucketCapacity); writer.Write(configuration.MaximumBuckets);
            writer.Write(configuration.MaximumPendingEvents); writer.Write(configuration.MaximumSessionEvents); writer.Write(configuration.MaximumTime);
        });
        try
        {
            bool instanced = Geometry == DamageTextGeometry.InstancedQuad;
            if (instanced && (!SystemInfo.supportsInstancing || !shader.isSupported || shader.name != "UIData/DamageTextInstanced"))
                throw new NotSupportedException("Shared Quad requires instancing support and the supported damage-text instancing Shader.");
            SelectPositionFormat();
            int capacity = configuration.BucketCapacity * configuration.MaximumBuckets;
            _entries = new Entry[capacity]; _freeEntries = new int[capacity]; _freeEntryCount = capacity;
            for (int i = 0; i < capacity; i++) { _entries[i] = new Entry(); _freeEntries[i] = capacity - 1 - i; }
            _pending = new List<int>(configuration.MaximumPendingEvents);
            _order = new List<int>(configuration.MaximumBuckets);
            _seen = new HashSet<uint>(configuration.MaximumSessionEvents);
            _comparer = new PendingComparer(_entries);
            _vertices = instanced ? null : new DamageTextVertex[configuration.BucketCapacity * 32];
            _halfVertices = EffectivePositionFormat == DamageTextPositionFormat.Float16 ? new DamageTextHalfVertex[_vertices.Length] : null;
            var indices = instanced ? null : new ushort[configuration.BucketCapacity * 48];
            for (int quad = 0; !instanced && quad < configuration.BucketCapacity * 8; quad++)
            {
                int v = quad * 4, t = quad * 6;
                indices[t] = (ushort)v; indices[t + 1] = (ushort)(v + 1); indices[t + 2] = (ushort)(v + 2);
                indices[t + 3] = (ushort)(v + 2); indices[t + 4] = (ushort)(v + 3); indices[t + 5] = (ushort)v;
            }
            _pool = new Bucket[configuration.MaximumBuckets];
            _material = new Material(shader) { name = "Damage text owned material", renderQueue = 3000 };
            _material.enableInstancing = instanced;
            if (instanced)
            {
                if (!_material.enableInstancing || _material.passCount != 1)
                    throw new NotSupportedException("Shared Quad requires one instanced pass.");
                _instanceQuad = DamageTextInstanceBatch.CreateQuad();
                _instanceMatrices = new Matrix4x4[DamageTextInstanceBatch.GroupCapacity];
                for (int i = 0; i < _instanceMatrices.Length; i++) _instanceMatrices[i] = Matrix4x4.identity;
            }
            _material.SetTexture("_DamageTextAtlas", snapshot.Texture);
            try { CreateBuckets(indices, instanced); }
            catch (VertexLayoutException) when (EffectivePositionFormat == DamageTextPositionFormat.Float16)
            {
                foreach (var bucket in _pool)
                    if (bucket != null) { DestroyOwned(bucket.Front); DestroyOwned(bucket.Back); }
                EffectivePositionFormat = DamageTextPositionFormat.Float32;
                FormatReason = "actual-bucket-layout-mismatch"; _halfVertices = null;
                CreateBuckets(indices, false);
            }
            _statistics = new DamageTextStatistics
            {
                PoolBuckets = _pool.Length, OwnedMeshes = instanced ? 1 : _pool.Length * 2, OwnedMaterials = 1,
                VertexStride = instanced ? _instanceQuad.GetVertexBufferStride(0) :
                    EffectivePositionFormat == DamageTextPositionFormat.Float16 ? DamageTextHalfVertex.Stride : DamageTextVertex.Stride,
                InstanceGroupCapacity = instanced ? DamageTextInstanceBatch.GroupCapacity : 0,
                InstanceParameterStride = instanced ? DamageTextInstanceBatch.ParameterStride : 0
            };
            VertexLayout = DescribeVertexLayout(instanced ? _instanceQuad : _pool[0].Front);
            Status = DamageTextRendererStatus.Ready;
        }
        catch (Exception error)
        {
            Clear(); Status = DamageTextRendererStatus.Failed; LastError = error; throw;
        }
    }

    private static string DescribeVertexLayout(Mesh mesh)
    {
        var description = new System.Text.StringBuilder();
        description.Append("stride=").Append(mesh.GetVertexBufferStride(0));
        foreach (var attribute in mesh.GetVertexAttributes())
        {
            description.Append('|').Append(attribute.attribute).Append(':').Append(attribute.format).Append('x').Append(attribute.dimension)
                .Append('@').Append(mesh.GetVertexAttributeOffset(attribute.attribute)).Append('#').Append(attribute.stream);
        }
        return description.ToString();
    }

    private void CreateBuckets(ushort[] indices, bool instanced)
    {
        for (int i = 0; i < _pool.Length; i++)
        {
            var bucket = new Bucket { Entries = new int[_configuration.BucketCapacity] };
            _pool[i] = bucket; // Install before allocations so partial Prepare is disposable.
            if (instanced)
            {
                bucket.InstanceFront = new DamageTextInstanceBatch(_configuration.BucketCapacity * 8, _instanceMatrices);
                bucket.InstanceBack = new DamageTextInstanceBatch(_configuration.BucketCapacity * 8, _instanceMatrices);
            }
            else { bucket.Front = CreateMesh(indices); bucket.Back = CreateMesh(indices); }
        }
    }

    private void SelectPositionFormat()
    {
        EffectivePositionFormat = DamageTextPositionFormat.Float32; FormatReason = "requested-float32";
        if (RequestedPositionFormat != DamageTextPositionFormat.Float16) return;
        if (!(_uploader is IDamageTextHalfMeshUploader)) { FormatReason = "uploader-does-not-support-float16"; return; }
        if (!_formatSupport.SupportsFloat16x2) { FormatReason = "float16x2-unsupported"; return; }
        Mesh probe = null;
        try
        {
            probe = new Mesh(); probe.SetVertexBufferParams(4, DamageTextHalfVertex.Attributes);
            if (!DamageTextVertexFormats.MatchesLayout(probe, DamageTextPositionFormat.Float16) ||
                !_formatSupport.MatchesLayout(probe, DamageTextPositionFormat.Float16))
            { FormatReason = "float16-layout-mismatch"; return; }
            EffectivePositionFormat = DamageTextPositionFormat.Float16; FormatReason = "supported-and-layout-verified";
        }
        catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is NotSupportedException)
        { FormatReason = "float16-layout-unavailable: " + error.GetType().Name; }
        finally { DestroyOwned(probe); }
    }

    private Mesh CreateMesh(ushort[] indices)
    {
        var mesh = new Mesh { name = "Damage text persistent bucket" };
        try
        {
            mesh.MarkDynamic();
            bool half = EffectivePositionFormat == DamageTextPositionFormat.Float16;
            mesh.SetVertexBufferParams(_vertices.Length, half ? DamageTextHalfVertex.Attributes : DamageTextVertex.Attributes);
            if (half) mesh.SetVertexBufferData(_halfVertices, 0, 0, _halfVertices.Length);
            else mesh.SetVertexBufferData(_vertices, 0, 0, _vertices.Length);
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, 0), MeshUpdateFlags.DontRecalculateBounds);
            // Direct camera commands do not use local glyph bounds for renderer/frustum culling.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000);
            if (!DamageTextVertexFormats.MatchesLayout(mesh, EffectivePositionFormat) ||
                !_formatSupport.MatchesLayout(mesh, EffectivePositionFormat)) throw new VertexLayoutException();
            return mesh;
        }
        catch (Exception error)
        {
            DestroyOwned(mesh);
            if (EffectivePositionFormat == DamageTextPositionFormat.Float16 &&
                (error is ArgumentException || error is InvalidOperationException || error is NotSupportedException)) throw new VertexLayoutException();
            throw;
        }
    }

    public DamageTextSpawnResult Spawn(in DamageTextRequest request)
    {
        CheckAccess();
        if (Status != DamageTextRendererStatus.Ready || !_atlas.IsValid) return Reject(DamageTextSpawnResult.NotReady);
        var result = DamageTextRequest.Validate(request, _configuration.MaximumTime);
        if (result != DamageTextSpawnResult.Accepted) return Reject(result);
        var style = _styles[(int)request.Kind];
        if (request.SpawnTime + style.Lifetime > _configuration.MaximumTime) return Reject(DamageTextSpawnResult.InvalidTime);
        if (!_atlas.Contains(request.Amount)) return Reject(DamageTextSpawnResult.MissingGlyph);
        if (_seen.Contains(request.EventId)) return Reject(DamageTextSpawnResult.DuplicateEventId);
        if (request.SpawnTime < _time || (_hasCommittedKey && DamageTextRequest.Compare(request, _committedKey) <= 0))
            return Reject(DamageTextSpawnResult.LateEvent);
        int appendCapacity = (_pool.Length - _order.Count) * _configuration.BucketCapacity;
        if (_order.Count != 0) appendCapacity += _configuration.BucketCapacity - _pool[_order[_order.Count - 1]].Count;
        if (_pending.Count >= _configuration.MaximumPendingEvents || _seen.Count >= _configuration.MaximumSessionEvents ||
            _freeEntryCount == 0 || _pending.Count >= appendCapacity)
            return Reject(DamageTextSpawnResult.CapacityExceeded);
        int index = _freeEntries[--_freeEntryCount];
        var entry = _entries[index];
        entry.State = new DamageTextEventState
        {
            Request = request, FirstPublishedTime = -1, FirstCameraEligibleTime = -1,
            EndTime = request.SpawnTime + style.Lifetime
        };
        entry.Style = style; entry.Bucket = -1;
        _pending.Add(index); _seen.Add(request.EventId);
        _statistics.Accepted++; _statistics.PendingEvents = _pending.Count;
        return DamageTextSpawnResult.Accepted;
    }

    private DamageTextSpawnResult Reject(DamageTextSpawnResult result)
    {
        _rejections[(int)result]++; _statistics.Rejected++; return result;
    }
    public long GetRejectionCount(DamageTextSpawnResult result) => _rejections[(int)result];

    public DamageTextStatistics Tick(float playbackTime) => Tick(playbackTime, Time.frameCount);
    public DamageTextStatistics Tick(float playbackTime, int frame)
    {
        CheckAccess();
        if (_configuration == null) throw new InvalidOperationException("Prepare before Tick.");
        if (!DamageTextRequest.Finite(playbackTime) || playbackTime < _time || playbackTime > _configuration.MaximumTime || frame < _lastFrame)
            throw new ArgumentOutOfRangeException(nameof(playbackTime));
        if (frame == _lastFrame) return _statistics;
        if (Status != DamageTextRendererStatus.Ready) throw new InvalidOperationException("Prepare or explicitly recover the renderer before Tick.");
        _lastFrame = frame; _time = playbackTime;
        ExecuteCutoff(); return _statistics;
    }

    public bool Recover()
    {
        CheckAccess();
        if (Status != DamageTextRendererStatus.Failed || _atlas == null || !_atlas.IsValid) return false;
        Status = DamageTextRendererStatus.Ready; ExecuteCutoff();
        return Status == DamageTextRendererStatus.Ready;
    }

    private void ExecuteCutoff()
    {
        _executing = true;
        try
        {
            if (!_atlas.IsValid) throw new InvalidOperationException("Digit atlas changed or became unavailable. Prepare a new resource version.");
            RecycleExpiredBuckets();
            _pending.Sort(_comparer);
            for (int i = 0; i < _pending.Count; i++)
            {
                var entry = _entries[_pending[i]];
                if (entry.State.Request.SpawnTime > _time) break;
                if (entry.Bucket >= 0) continue; // Retained staging after an upload failure.
                if (entry.State.EndTime <= _time) continue; // Counted on commit, without pointless geometry.
                entry.State.Characters = DamageTextLayout.Build(entry.State.Request.Amount, _atlas, entry.Style.Tracking, _digits, entry.Layout);
                _statistics.Layouts++;
                int bucketIndex = GetAppendBucket();
                var bucket = _pool[bucketIndex];
                bucket.Entries[bucket.Count++] = _pending[i]; bucket.Characters += entry.State.Characters;
                bucket.Dirty = true; entry.Bucket = bucketIndex;
            }
            for (int b = 0; b < _order.Count; b++)
            {
                var bucket = _pool[_order[b]];
                if (!bucket.Dirty) continue;
                int count = FillGeometry(bucket);
                if (Geometry == DamageTextGeometry.InstancedQuad)
                {
                    bucket.InstanceBack.Upload(); _statistics.InstanceUploads += bucket.InstanceBack.GroupCount;
                    _statistics.TheoreticalUploadBytes += bucket.InstanceBack.UploadBytes;
                }
                else
                {
                    if (_halfVertices != null)
                    {
                        for (int i = 0; i < count; i++) _halfVertices[i] = DamageTextHalfVertex.Encode(_vertices[i]);
                        ((IDamageTextHalfMeshUploader)_uploader).Upload(bucket.Back, _halfVertices, count, bucket.Characters * 6);
                    }
                    else _uploader.Upload(bucket.Back, _vertices, count, bucket.Characters * 6);
                    _statistics.VertexUploads++;
                    _statistics.TheoreticalUploadBytes += (long)count * _statistics.VertexStride;
                }
                _statistics.Rebuilds++;
            }
            bool changed = false;
            for (int b = 0; b < _order.Count; b++)
            {
                var bucket = _pool[_order[b]];
                if (!bucket.Dirty) continue;
                var old = bucket.Front; bucket.Front = bucket.Back; bucket.Back = old;
                var oldInstances = bucket.InstanceFront; bucket.InstanceFront = bucket.InstanceBack; bucket.InstanceBack = oldInstances;
                bucket.PublishedCount = bucket.Count; bucket.Dirty = false; changed = true;
            }
            int remaining = 0;
            for (int i = 0; i < _pending.Count; i++)
            {
                int index = _pending[i]; var entry = _entries[index];
                if (entry.State.Request.SpawnTime > _time) { _pending[remaining++] = index; continue; }
                if (entry.Bucket < 0)
                {
                    _statistics.Ended++; _statistics.MissedPresentation++;
                    _freeEntries[_freeEntryCount++] = index;
                }
                else
                {
                    entry.State.Published = true; entry.State.FirstPublishedTime = _time; _statistics.Published++;
                }
                _committedKey = entry.State.Request; _hasCommittedKey = true;
            }
            _pending.RemoveRange(remaining, _pending.Count - remaining);
            if (changed) DrawRevision++;
            LastError = null;
        }
        catch (Exception error) { LastError = error; Status = DamageTextRendererStatus.Failed; }
        finally { _executing = false; UpdateStatistics(); }
    }

    private int GetAppendBucket()
    {
        if (_order.Count != 0)
        {
            int last = _order[_order.Count - 1];
            if (_pool[last].Count < _configuration.BucketCapacity) return last;
        }
        for (int i = 0; i < _pool.Length; i++)
        {
            if (_pool[i].Count != 0) continue;
            _order.Add(i); return i;
        }
        throw new InvalidOperationException("Reserved append capacity was exceeded.");
    }

    private int FillGeometry(Bucket bucket)
    {
        int vertexCount = 0;
        bucket.InstanceBack?.Reset();
        for (int e = 0; e < bucket.Count; e++)
        {
            var entry = _entries[bucket.Entries[e]]; var request = entry.State.Request; var style = entry.Style;
            var vertex = new DamageTextVertex
            {
                Color = style.Color,
                AnchorAndSpawn = new Vector4(request.WorldPosition.x, request.WorldPosition.y, request.WorldPosition.z, request.SpawnTime),
                Motion = new Vector4(style.Lifetime, style.PixelSize / _atlas.ReferencePixelSize,
                    DamageTextAnimation.Spread(request.RandomSeed) * style.HorizontalSpread, style.RisePixels),
                Animation = new Vector4(style.PopAmplitude, style.PopDuration, style.FadeStart, (int)request.Kind)
            };
            for (int q = 0; q < entry.State.Characters; q++)
            {
                var quad = entry.Layout[q];
                if (bucket.InstanceBack != null) { bucket.InstanceBack.Add(quad, vertex); continue; }
                vertex.Position = quad.Min; vertex.Uv = new Vector2(quad.Uv.xMin, quad.Uv.yMin); _vertices[vertexCount++] = vertex;
                vertex.Position = new Vector2(quad.Min.x, quad.Max.y); vertex.Uv.y = quad.Uv.yMax; _vertices[vertexCount++] = vertex;
                vertex.Position = quad.Max; vertex.Uv.x = quad.Uv.xMax; _vertices[vertexCount++] = vertex;
                vertex.Position = new Vector2(quad.Max.x, quad.Min.y); vertex.Uv.y = quad.Uv.yMin; _vertices[vertexCount++] = vertex;
            }
        }
        return vertexCount;
    }

    private void RecycleExpiredBuckets()
    {
        for (int b = _order.Count - 1; b >= 0; b--)
        {
            var bucket = _pool[_order[b]];
            bool expired = !bucket.Dirty;
            for (int e = 0; e < bucket.Count; e++)
                if (_entries[bucket.Entries[e]].State.EndTime > _time) { expired = false; break; }
            if (!expired) continue;
            for (int e = 0; e < bucket.Count; e++)
            {
                int index = bucket.Entries[e];
                if (!_entries[index].State.Ended) _statistics.Ended++;
                _entries[index].Bucket = -1; _freeEntries[_freeEntryCount++] = index;
            }
            bucket.Count = bucket.PublishedCount = bucket.Characters = 0;
            _order.RemoveAt(b); _statistics.RecycledBuckets++; DrawRevision++;
        }
    }

    private void UpdateStatistics()
    {
        _statistics.PlaybackTime = _time; _statistics.PendingEvents = _pending?.Count ?? 0;
        _statistics.MaximumPendingAge = 0;
        if (_pending != null) foreach (int index in _pending)
            _statistics.MaximumPendingAge = Mathf.Max(_statistics.MaximumPendingAge, _time - _entries[index].State.Request.SpawnTime);
        _statistics.ResidentEvents = _entries == null ? 0 : _entries.Length - _freeEntryCount;
        _statistics.LiveEvents = _statistics.LiveCharacters = _statistics.SubmittedCharacters = 0;
        _statistics.SourceVertexCapacity = _statistics.SourceIndexCapacity = _statistics.SubmittedVertices = _statistics.SubmittedIndices = 0;
        _statistics.LiveEventChecksum = 0;
        _statistics.ExpiredResidentCharacters = _statistics.Buckets = _statistics.Draws = _statistics.InstanceGroups = _statistics.Passes = 0;
        if (_order == null) return;
        for (int b = 0; b < _order.Count; b++)
        {
            var bucket = _pool[_order[b]];
            if (bucket.PublishedCount == 0) continue;
            _statistics.Buckets++;
            int draws = Geometry == DamageTextGeometry.InstancedQuad ? bucket.InstanceFront.GroupCount : 1;
            _statistics.Draws += draws; _statistics.Passes += draws;
            if (Geometry == DamageTextGeometry.InstancedQuad) _statistics.InstanceGroups += draws;
            for (int e = 0; e < bucket.PublishedCount; e++)
            {
                var entry = _entries[bucket.Entries[e]];
                _statistics.SubmittedCharacters += entry.State.Characters;
                if (entry.State.EndTime > _time)
                {
                    _statistics.LiveEvents++; _statistics.LiveCharacters += entry.State.Characters;
                    _statistics.LiveEventChecksum ^= DamageTextEventChecksum.Compute(entry.State.Request);
                }
                else
                {
                    _statistics.ExpiredResidentCharacters += entry.State.Characters;
                    if (!entry.State.Ended) { entry.State.Ended = true; _statistics.Ended++; }
                }
            }
        }
        _statistics.SubmittedVertices = _statistics.SubmittedCharacters * 4;
        _statistics.SubmittedIndices = _statistics.SubmittedCharacters * 6;
        _statistics.SourceVertexCapacity = Geometry == DamageTextGeometry.InstancedQuad ? (_statistics.Draws > 0 ? 4 : 0) :
            _statistics.Buckets * _configuration.BucketCapacity * 32;
        _statistics.SourceIndexCapacity = Geometry == DamageTextGeometry.InstancedQuad ? (_statistics.Draws > 0 ? 6 : 0) :
            _statistics.Buckets * _configuration.BucketCapacity * 48;
    }

    public bool TryGetEvent(uint eventId, out DamageTextEventState state)
    {
        CheckAccess();
        if (_entries != null)
        {
            foreach (int bucketIndex in _order)
            {
                var bucket = _pool[bucketIndex];
                for (int i = 0; i < bucket.Count; i++)
                {
                    var entry = _entries[bucket.Entries[i]];
                    if (entry.State.Request.EventId == eventId) { state = entry.State; return true; }
                }
            }
            foreach (int index in _pending)
                if (_entries[index].State.Request.EventId == eventId) { state = _entries[index].State; return true; }
        }
        state = default; return false;
    }

    public Mesh GetPublishedMesh(int logicalBucket)
    {
        if (Geometry != DamageTextGeometry.PersistentMesh) throw new InvalidOperationException("Shared Quad has no per-bucket mesh.");
        CheckAccess(); return _pool[_order[logicalBucket]].Front;
    }
    /// <summary>Preparation/diagnostics only; includes owned Unity objects, not measured GPU memory.</summary>
    public long GetOwnedObjectBytes()
    {
        CheckAccess(); long bytes = 0;
        if (_material != null) bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(_material);
        if (_instanceQuad != null) bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(_instanceQuad);
        if (_pool != null) foreach (var bucket in _pool)
        {
            if (bucket?.Front != null) bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(bucket.Front);
            if (bucket?.Back != null) bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(bucket.Back);
        }
        return bytes;
    }
    /// <summary>Near-plane eligibility only; this does not claim pixel visibility or absence of occlusion.</summary>
    internal void ObserveCamera(Matrix4x4 view, float nearPlane)
    {
        _statistics.TheoreticalFrameParameterBytes += 4 + 64 + 64 + 16;
        _statistics.CameraEligibleEvents = _statistics.NearPlaneClippedEvents = 0;
        if (!CanDraw || _order == null) return;
        foreach (int index in _order)
        {
            var bucket = _pool[index];
            for (int i = 0; i < bucket.PublishedCount; i++)
            {
                var entry = _entries[bucket.Entries[i]];
                if (entry.State.EndTime <= _time) continue;
                if (-view.MultiplyPoint3x4(entry.State.Request.WorldPosition).z <= nearPlane)
                    _statistics.NearPlaneClippedEvents++;
                else
                {
                    _statistics.CameraEligibleEvents++;
                    if (entry.State.FirstCameraEligibleTime < 0) entry.State.FirstCameraEligibleTime = _time;
                }
            }
        }
    }
    public int RecordDraws(CommandBuffer commands, Matrix4x4 matrix, int shaderPass = 0)
    {
        CheckAccess();
        if (commands == null) throw new ArgumentNullException(nameof(commands));
        if (shaderPass != 0) throw new ArgumentOutOfRangeException(nameof(shaderPass));
        if (!CanDraw) return 0;
        int draws = 0;
        foreach (int index in _order)
        {
            var bucket = _pool[index];
            if (bucket.PublishedCount == 0) continue;
            if (Geometry == DamageTextGeometry.InstancedQuad)
            {
                if (matrix != Matrix4x4.identity) throw new ArgumentException("Damage-text anchors require an identity draw matrix.");
                draws += bucket.InstanceFront.RecordDraws(commands, _instanceQuad, _material);
                _statistics.TheoreticalCommandBytes += (long)bucket.InstanceFront.Count * 64;
            }
            else
            {
                commands.DrawMesh(bucket.Front, matrix, _material, 0, 0); draws++;
                _statistics.TheoreticalCommandBytes += 64;
            }
        }
        return draws;
    }

    public DamageTextCameraPass BindCamera(Camera camera)
    {
        CheckAccess();
        if (Status != DamageTextRendererStatus.Ready) throw new InvalidOperationException("Prepare before binding a camera.");
        _cameraPass?.Dispose(); _cameraPass = new DamageTextCameraPass(camera, this); return _cameraPass;
    }
    public void Clear()
    {
        CheckAccess();
        _cameraPass?.Dispose(); _cameraPass = null;
        if (_pool != null) foreach (var bucket in _pool)
            if (bucket != null) { DestroyOwned(bucket.Front); DestroyOwned(bucket.Back); }
        DestroyOwned(_material); _material = null; _pool = null; _entries = null; _freeEntries = null;
        DestroyOwned(_instanceQuad); _instanceQuad = null; _instanceMatrices = null;
        _pending = null; _order = null; _seen = null; _vertices = null; _comparer = null;
        _halfVertices = null; EffectivePositionFormat = DamageTextPositionFormat.Float32; FormatReason = VertexLayout = null;
        _atlas = null; _styles = null; _configuration = null;
        Array.Clear(_rejections, 0, _rejections.Length);
        _freeEntryCount = 0; _statistics = default; _lastFrame = -1; _time = 0;
        _hasCommittedKey = false; LastError = null; StyleHash = ConfigurationHash = null;
        DrawRevision++; Status = DamageTextRendererStatus.Empty;
    }
    public void Dispose()
    {
        if (Status == DamageTextRendererStatus.Disposed) return;
        Clear(); Status = DamageTextRendererStatus.Disposed;
    }
    private void CheckAccess()
    {
        if (System.Threading.Thread.CurrentThread.ManagedThreadId != _thread) throw new InvalidOperationException("Main-thread access required.");
        if (_executing) throw new InvalidOperationException("Damage-text mutation is not reentrant.");
        if (Status == DamageTextRendererStatus.Disposed) throw new ObjectDisposedException(nameof(DamageTextRenderer));
    }
    private static void DestroyOwned(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
    }
}
