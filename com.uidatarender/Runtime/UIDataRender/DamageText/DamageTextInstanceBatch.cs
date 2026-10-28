using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Ordered, fixed-capacity instance groups. Arrays/MPBs are allocated only in Preparing.</summary>
internal sealed class DamageTextInstanceBatch
{
    // Kept below the per-draw constant-buffer budget, including Unity's object matrices.
    // Must match forcemaxcount in UIE-DamageTextInstanced.shader.
    public const int GroupCapacity = 128;
    public const int ParameterStride = 6 * 16;
    private static readonly int[] PropertyIds =
    {
        Shader.PropertyToID("_DamageLocalRect"), Shader.PropertyToID("_DamageUvRect"),
        Shader.PropertyToID("_DamageColor"), Shader.PropertyToID("_DamageAnchor"),
        Shader.PropertyToID("_DamageMotion"), Shader.PropertyToID("_DamageAnimation")
    };
    private sealed class Group
    {
        public readonly Vector4[][] Values = new Vector4[6][];
        public readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
        public Group()
        {
            for (int i = 0; i < Values.Length; i++) Values[i] = new Vector4[GroupCapacity];
        }
        public void Upload()
        {
            // Always install the full array: MPB array lengths cannot grow after first assignment.
            for (int i = 0; i < Values.Length; i++) Properties.SetVectorArray(PropertyIds[i], Values[i]);
        }
    }
    private readonly Group[] _groups;
    private readonly Matrix4x4[] _matrices;
    public int Count { get; private set; }
    public int GroupCount => (Count + GroupCapacity - 1) / GroupCapacity;
    public long UploadBytes => (long)GroupCount * GroupCapacity * ParameterStride;

    public DamageTextInstanceBatch(int maximumCharacters, Matrix4x4[] matrices)
    {
        _matrices = matrices;
        _groups = new Group[(maximumCharacters + GroupCapacity - 1) / GroupCapacity];
        for (int i = 0; i < _groups.Length; i++) _groups[i] = new Group();
    }
    public void Reset() { Count = 0; }
    public void Add(in DamageTextGlyphQuad quad, in DamageTextVertex parameters)
    {
        if (Count == _groups.Length * GroupCapacity) throw new InvalidOperationException("Instance capacity exceeded.");
        var values = _groups[Count / GroupCapacity].Values;
        int index = Count++ % GroupCapacity;
        values[0][index] = new Vector4(quad.Min.x, quad.Min.y, quad.Max.x, quad.Max.y);
        values[1][index] = new Vector4(quad.Uv.xMin, quad.Uv.yMin, quad.Uv.xMax, quad.Uv.yMax);
        values[2][index] = (Color)parameters.Color;
        values[3][index] = parameters.AnchorAndSpawn;
        values[4][index] = parameters.Motion;
        values[5][index] = parameters.Animation;
    }
    public void Upload()
    {
        for (int i = 0; i < GroupCount; i++) _groups[i].Upload();
    }
    public int RecordDraws(CommandBuffer commands, Mesh quad, Material material)
    {
        int remaining = Count;
        for (int i = 0; i < GroupCount; i++)
        {
            int count = Math.Min(GroupCapacity, remaining);
            commands.DrawMeshInstanced(quad, 0, material, 0, _matrices, count, _groups[i].Properties);
            remaining -= count;
        }
        return GroupCount;
    }
    public static Mesh CreateQuad()
    {
        var mesh = new Mesh { name = "Damage text shared instance quad" };
        try
        {
            mesh.vertices = new[] { Vector3.zero, Vector3.up, Vector3.one - Vector3.forward, Vector3.right };
            mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000000);
            return mesh;
        }
        catch
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh);
            throw;
        }
    }
}
