using System;
using UnityEngine;

public enum DamageTextKind : byte { Normal, Critical, Healing }
public enum DamageTextSpawnResult
{
    Accepted, InvalidAmount, InvalidKind, InvalidPosition, InvalidTime, MissingGlyph,
    DuplicateEventId, LateEvent, CapacityExceeded, NotReady
}
public enum DamageTextRendererStatus { Empty, Ready, Failed, Disposed }
public enum DamageTextGeometry { PersistentMesh, InstancedQuad }
public enum DamageTextPlaybackState { Idle, Preparing, Warming, Sampling, Paused, Completed, Failed }

/// <summary>Copied on acceptance. An EventId identifies one hit within a playback session.</summary>
[Serializable]
public struct DamageTextRequest
{
    public uint EventId;
    public uint Amount;
    public DamageTextKind Kind;
    public Vector3 WorldPosition;
    public float SpawnTime;
    public uint RandomSeed;

    public static DamageTextSpawnResult Validate(in DamageTextRequest request, float maximumTime)
    {
        if (request.Amount < 10 || request.Amount > 99999999) return DamageTextSpawnResult.InvalidAmount;
        if ((byte)request.Kind > (byte)DamageTextKind.Healing) return DamageTextSpawnResult.InvalidKind;
        if (!Finite(request.WorldPosition.x) || !Finite(request.WorldPosition.y) || !Finite(request.WorldPosition.z))
            return DamageTextSpawnResult.InvalidPosition;
        if (!Finite(request.SpawnTime) || request.SpawnTime < 0 || request.SpawnTime > maximumTime)
            return DamageTextSpawnResult.InvalidTime;
        return DamageTextSpawnResult.Accepted;
    }

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static int Compare(in DamageTextRequest a, in DamageTextRequest b)
    {
        int time = a.SpawnTime.CompareTo(b.SpawnTime);
        return time != 0 ? time : a.EventId.CompareTo(b.EventId);
    }
}

[Serializable]
public sealed class DamageTextConfiguration
{
    public int BucketCapacity = 128;
    public int MaximumBuckets = 8;
    public int MaximumPendingEvents = 1024;
    public int MaximumSessionEvents = 16384;
    public float MaximumTime = 1800;

    public DamageTextConfiguration Copy() => (DamageTextConfiguration)MemberwiseClone();
    public void Validate()
    {
        if (BucketCapacity < 1 || BucketCapacity > 2047 || MaximumBuckets < 1 || MaximumBuckets > 256 ||
            MaximumPendingEvents < 1 || MaximumSessionEvents < 1 ||
            !DamageTextRequest.Finite(MaximumTime) || MaximumTime <= 0 || MaximumTime > 1800 ||
            (long)BucketCapacity * MaximumBuckets > 65536 || MaximumPendingEvents > 65536 || MaximumSessionEvents > 1048576)
            throw new ArgumentOutOfRangeException(nameof(DamageTextConfiguration), "Invalid bounded damage-text capacity or time range.");
    }
}

[Serializable]
public struct DamageTextStatistics
{
    public long Accepted, Rejected, Published, Ended, MissedPresentation, RecycledBuckets;
    public long Layouts, Rebuilds, VertexUploads, TheoreticalUploadBytes;
    public int PendingEvents, ResidentEvents, LiveEvents, LiveCharacters, SubmittedCharacters, ExpiredResidentCharacters;
    public int Buckets, Draws, PoolBuckets, OwnedMeshes, OwnedMaterials, VertexStride;
    public int InstanceGroups, InstanceGroupCapacity, InstanceParameterStride, Passes;
    public long InstanceUploads, TheoreticalCommandBytes;
    public long TheoreticalFrameParameterBytes;
    public long LiveEventChecksum;
    public int SourceVertexCapacity, SourceIndexCapacity, SubmittedVertices, SubmittedIndices;
    public int CameraEligibleEvents, NearPlaneClippedEvents;
    public float PlaybackTime, MaximumPendingAge;
}

[Serializable]
public struct DamageTextEventState
{
    public DamageTextRequest Request;
    public float FirstPublishedTime, FirstCameraEligibleTime, EndTime;
    public int Characters;
    public bool Published, Ended;
}
