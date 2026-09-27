using System;
using System.Collections.Generic;
using System.Diagnostics;
using UIData;
using UnityEngine;

/// <summary>
/// Builds and draws one or more meshes from prefab holders through MergeBatcher.
/// This is the shared runtime path for holder rendering regardless of holder data type.
/// </summary>
public sealed class HolderBatchRenderer : IDisposable
{
    private readonly MergeBatcher batcher;
    private readonly PerfProbe probe;
    private Mesh[] meshes = Array.Empty<Mesh>();
    private Material[] materials = Array.Empty<Material>();
    // Reused merge scratch buffers. Rebuilding a batch every frame must not
    // allocate four new backing arrays through List growth.
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector4> uvs = new List<Vector4>();
    private readonly List<Color32> colors = new List<Color32>();
    private readonly List<int> triangles = new List<int>();
    private int batchCount;
    public double LastPlanMs { get; private set; }
    public double LastFillMs { get; private set; }
    public double LastMeshUploadMs { get; private set; }

    public HolderBatchRenderer(int maxSlots, PerfProbe probe = null)
    {
        batcher = new MergeBatcher(maxSlots);
        this.probe = probe ?? new PerfProbe();
    }

    public int BatchCount => batchCount;
    public PerfProbe Probe => probe;
    public Mesh GetBatchMesh(int index) => index >= 0 && index < batchCount ? meshes[index] : null;

    public void Rebuild(
        IReadOnlyList<IUIPrefabHolder> holders,
        IReadOnlyDictionary<IUIPrefabHolder, Vector3> positions,
        Func<Material> createMaterial,
        Action<Material, RenderBatch> bindTextures)
    {
        if (holders == null) throw new ArgumentNullException(nameof(holders));
        if (createMaterial == null) throw new ArgumentNullException(nameof(createMaterial));
        if (bindTextures == null) throw new ArgumentNullException(nameof(bindTextures));

        List<RenderBatch> batches;
        long planStart = Stopwatch.GetTimestamp();
        using (PerfProbe.MergeJobMarker.Auto())
        {
            batches = batcher.Plan(holders);
        }
        LastPlanMs = (Stopwatch.GetTimestamp() - planStart) * 1000.0 / Stopwatch.Frequency;
        batchCount = batches.Count;
        EnsureBatchArrays(batchCount, createMaterial);

        float fillMs = 0f;
        double meshUploadMs = 0.0;
        for (int b = 0; b < batches.Count; b++)
        {
            var batch = batches[b];
            var localSlotMap = batch.BuildLocalSlotMap();

            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();

            // Reserve the common quad workload up front. Lists retain any
            // larger capacity observed in later rebuilds, so subsequent
            // Fill/Add calls do not create replacement arrays.
            int holderCount = batch.Holders.Count;
            EnsureCapacity(vertices, holderCount * 12);
            EnsureCapacity(uvs, holderCount * 12);
            EnsureCapacity(colors, holderCount * 12);
            EnsureCapacity(triangles, holderCount * 18);

            long fillStartTimestamp = Stopwatch.GetTimestamp();
            using (PerfProbe.FillMarker.Auto())
            {
                foreach (var holder in batch.Holders)
                {
                    var position = positions != null && positions.TryGetValue(holder, out var p)
                        ? p
                        : holder.Position;
                    holder.Fill(vertices, uvs, colors, triangles, position);
                }
            }
            long fillElapsedTicks = Stopwatch.GetTimestamp() - fillStartTimestamp;
            fillMs += (float)(fillElapsedTicks * 1000.0 / Stopwatch.Frequency);

            ApplySlotRemap(uvs, localSlotMap);

            long meshStart = Stopwatch.GetTimestamp();
            meshes[b].Clear();
            meshes[b].SetVertices(vertices);
            meshes[b].SetUVs(0, uvs);
            meshes[b].SetColors(colors);
            meshes[b].SetTriangles(triangles, 0);
            meshes[b].RecalculateBounds();
            meshUploadMs += (Stopwatch.GetTimestamp() - meshStart) * 1000.0 / Stopwatch.Frequency;

            bindTextures(materials[b], batch);
        }

        probe.Record(fillMs, batchCount);
        LastFillMs = fillMs;
        LastMeshUploadMs = meshUploadMs;
    }

    public int Draw(Matrix4x4 matrix, int layer, Camera camera)
    {
        using (PerfProbe.DrawMarker.Auto())
        {
            for (int i = 0; i < batchCount; i++)
                Graphics.DrawMesh(meshes[i], matrix, materials[i], layer, camera);
        }
        return batchCount;
    }

    public void Dispose()
    {
        DestroyObjects(meshes);
        DestroyObjects(materials);
        meshes = Array.Empty<Mesh>();
        materials = Array.Empty<Material>();
        batchCount = 0;
        vertices.Clear();
        uvs.Clear();
        colors.Clear();
        triangles.Clear();
    }

    private void EnsureBatchArrays(int needed, Func<Material> createMaterial)
    {
        if (meshes.Length >= needed && materials.Length >= needed) return;

        var newMeshes = new Mesh[needed];
        var newMaterials = new Material[needed];
        for (int i = 0; i < needed; i++)
        {
            newMeshes[i] = i < meshes.Length ? meshes[i] : new Mesh();
            newMaterials[i] = i < materials.Length ? materials[i] : createMaterial();
        }
        meshes = newMeshes;
        materials = newMaterials;
    }

    private static void ApplySlotRemap(List<Vector4> uvs, IReadOnlyDictionary<int, int> localSlotMap)
    {
        for (int i = 0; i < uvs.Count; i++)
        {
            var uv = uvs[i];
            int globalSlot = (int)uv.z;
            if (globalSlot > 0 && localSlotMap.TryGetValue(globalSlot, out int localSlot))
                uvs[i] = new Vector4(uv.x, uv.y, localSlot, uv.w);
        }
    }

    private static void EnsureCapacity<T>(List<T> list, int required)
    {
        if (required > list.Capacity)
            list.Capacity = required;
    }

    private static void DestroyObjects<T>(IEnumerable<T> objects) where T : UnityEngine.Object
    {
        foreach (var obj in objects)
        {
            if (obj == null) continue;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(obj);
            else
                UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
