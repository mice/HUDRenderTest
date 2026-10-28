using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct DamageTextCameraKey
{
    public float Time;
    public Vector3 Position;
    public Quaternion Rotation;
    public float FieldOfView, OrthographicSize;
}

/// <summary>Immutable, sorted recording. Accessors return values rather than backing arrays.</summary>
public sealed class DamageTextClip
{
    private sealed class RequestComparer : IComparer<DamageTextRequest>
    {
        public int Compare(DamageTextRequest a, DamageTextRequest b) => DamageTextRequest.Compare(a, b);
    }
    private readonly DamageTextRequest[] _events;
    private readonly DamageTextCameraKey[] _cameraKeys;
    public int Count => _events.Length;
    public float Duration { get; }
    public string Version { get; }
    public uint GenerationSeed { get; }
    public string Hash { get; }
    public int CameraKeyCount => _cameraKeys.Length;
    public DamageTextRequest GetEvent(int index) => _events[index];
    public DamageTextCameraKey GetCameraKey(int index) => _cameraKeys[index];

    public DamageTextClip(DamageTextRequest[] events, float duration, string version, uint generationSeed,
        DamageTextCameraKey[] cameraKeys = null)
    {
        if (events == null || string.IsNullOrEmpty(version) || !DamageTextRequest.Finite(duration) || duration <= 0 || duration > 1800)
            throw new ArgumentException("A finite versioned clip of at most 1,800 seconds is required.");
        _events = (DamageTextRequest[])events.Clone();
        var ids = new HashSet<uint>();
        foreach (var request in _events)
        {
            var result = DamageTextRequest.Validate(request, duration);
            if (result != DamageTextSpawnResult.Accepted || !ids.Add(request.EventId))
                throw new ArgumentException("Invalid or duplicate event in clip: " + request.EventId + " / " + result);
        }
        Array.Sort(_events, new RequestComparer());
        _cameraKeys = cameraKeys == null ? Array.Empty<DamageTextCameraKey>() : (DamageTextCameraKey[])cameraKeys.Clone();
        float last = -1;
        foreach (var key in _cameraKeys)
        {
            float norm = key.Rotation.x * key.Rotation.x + key.Rotation.y * key.Rotation.y +
                key.Rotation.z * key.Rotation.z + key.Rotation.w * key.Rotation.w;
            if (!DamageTextRequest.Finite(key.Time) || key.Time < 0 || key.Time > duration || key.Time <= last ||
                !DamageTextRequest.Finite(key.Position.x) || !DamageTextRequest.Finite(key.Position.y) || !DamageTextRequest.Finite(key.Position.z) ||
                !DamageTextRequest.Finite(norm) || Mathf.Abs(norm - 1) > .001f ||
                !DamageTextRequest.Finite(key.FieldOfView) || key.FieldOfView <= 0 || key.FieldOfView >= 180 ||
                !DamageTextRequest.Finite(key.OrthographicSize) || key.OrthographicSize <= 0)
                throw new ArgumentException("Invalid or unordered camera trajectory.");
            last = key.Time;
        }
        Duration = duration; Version = version; GenerationSeed = generationSeed;
        Hash = DamageTextHash.Compute(writer =>
        {
            writer.Write("damage-clip-v1"); writer.Write(Version); writer.Write(Duration); writer.Write(GenerationSeed);
            writer.Write(_events.Length);
            foreach (var request in _events) DamageTextHash.Write(writer, request);
            writer.Write(_cameraKeys.Length);
            foreach (var key in _cameraKeys)
            {
                writer.Write(key.Time); writer.Write(key.Position.x); writer.Write(key.Position.y); writer.Write(key.Position.z);
                writer.Write(key.Rotation.x); writer.Write(key.Rotation.y); writer.Write(key.Rotation.z); writer.Write(key.Rotation.w);
                writer.Write(key.FieldOfView); writer.Write(key.OrthographicSize);
            }
        });
    }

    public void EvaluateCamera(float time, Camera camera)
    {
        if (camera == null || _cameraKeys.Length == 0) return;
        int upper = 0;
        while (upper < _cameraKeys.Length - 1 && _cameraKeys[upper].Time < time) upper++;
        int lower = Mathf.Max(0, upper - 1);
        var a = _cameraKeys[lower]; var b = _cameraKeys[upper];
        float t = upper == lower ? 0 : Mathf.Clamp01((time - a.Time) / (b.Time - a.Time));
        camera.transform.SetPositionAndRotation(Vector3.LerpUnclamped(a.Position, b.Position, t), Quaternion.SlerpUnclamped(a.Rotation, b.Rotation, t));
        camera.fieldOfView = Mathf.LerpUnclamped(a.FieldOfView, b.FieldOfView, t);
        camera.orthographicSize = Mathf.LerpUnclamped(a.OrthographicSize, b.OrthographicSize, t);
    }
}
