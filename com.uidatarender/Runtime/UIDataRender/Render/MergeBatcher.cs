using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Partitions a list of holders into <see cref="RenderBatch"/> instances using
/// greedy first-fit on image texture slot indices.
/// </summary>
public class MergeBatcher
{
    private readonly int _maxSlots;
    private readonly bool _preserveOrder;
    private readonly List<RenderBatch> batches = new List<RenderBatch>();
    private readonly List<RenderBatch> batchPool = new List<RenderBatch>();
    private readonly HashSet<int> slotScratch = new HashSet<int>();
    private readonly List<IUIPrefabHolder> cachedHolders = new List<IUIPrefabHolder>();
    private readonly List<int> cachedMeshCounts = new List<int>();
    private readonly List<int> cachedTextureIndices = new List<int>();
    private bool hasPlanCache;

    public bool LastPlanCacheHit { get; private set; }
    public int PlanCacheHits { get; private set; }
    public int PlanCacheMisses { get; private set; }

    public MergeBatcher(int maxSlots, bool preserveOrder = false)
    {
        _maxSlots = maxSlots;
        _preserveOrder = preserveOrder;
        if (preserveOrder && maxSlots < 1) throw new System.ArgumentOutOfRangeException(nameof(maxSlots));
    }

    /// <summary>
    /// Splits <paramref name="holders"/> into batches so that each batch uses at most
    /// <c>maxSlots</c> distinct image texture slot indices (TextureIndex &gt; 0).
    /// Logs a warning when more than one batch is required.
    /// </summary>
    public List<RenderBatch> Plan(IReadOnlyList<IUIPrefabHolder> holders)
    {
        if (holders == null) throw new System.ArgumentNullException(nameof(holders));

        if (hasPlanCache && MatchesCachedStructure(holders))
        {
            LastPlanCacheHit = true;
            PlanCacheHits++;
            return batches;
        }

        LastPlanCacheHit = false;
        PlanCacheMisses++;
        // A failed new plan must never make the previous cache match partially reset batches.
        hasPlanCache = false;
        batches.Clear();

        for (int h = 0; h < holders.Count; h++)
        {
            var holder = holders[h];
            slotScratch.Clear();
            CollectSlots(holder, slotScratch);
            if (_preserveOrder && slotScratch.Count > _maxSlots)
                throw new System.InvalidOperationException("A holder exceeds the ordered batch texture capacity.");
            bool placed = false;

            for (int b = _preserveOrder ? System.Math.Max(0, batches.Count - 1) : 0; b < batches.Count; b++)
            {
                var batch = batches[b];
                if (batch.TryAdd(holder, slotScratch, _maxSlots))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                RenderBatch batch;
                if (batches.Count < batchPool.Count)
                {
                    batch = batchPool[batches.Count];
                    batch.Reset(holder, slotScratch, holders.Count);
                }
                else
                {
                    batch = new RenderBatch(holder, slotScratch, holders.Count, _maxSlots);
                    batchPool.Add(batch);
                }
                batches.Add(batch);
            }
        }

        if (batches.Count > 1)
            Debug.LogWarning($"[MergeBatcher] split into {batches.Count} batches");

        CaptureStructure(holders);

        return batches;
    }

    private bool MatchesCachedStructure(IReadOnlyList<IUIPrefabHolder> holders)
    {
        if (holders.Count != cachedHolders.Count)
            return false;

        int slotOffset = 0;
        for (int h = 0; h < holders.Count; h++)
        {
            var holder = holders[h];
            if (!ReferenceEquals(holder, cachedHolders[h]))
                return false;

            var meshes = holder.UIMeshDatas;
            if (meshes == null || meshes.Count != cachedMeshCounts[h])
                return false;

            for (int i = 0; i < meshes.Count; i++)
            {
                if (meshes[i]?.TextureIndex != cachedTextureIndices[slotOffset++])
                    return false;
            }
        }
        return slotOffset == cachedTextureIndices.Count;
    }

    private void CaptureStructure(IReadOnlyList<IUIPrefabHolder> holders)
    {
        cachedHolders.Clear();
        cachedMeshCounts.Clear();
        cachedTextureIndices.Clear();
        for (int h = 0; h < holders.Count; h++)
        {
            var holder = holders[h];
            cachedHolders.Add(holder);
            var meshes = holder.UIMeshDatas;
            int count = meshes?.Count ?? 0;
            cachedMeshCounts.Add(count);
            for (int i = 0; i < count; i++)
                cachedTextureIndices.Add(meshes[i]?.TextureIndex ?? 0);
        }
        hasPlanCache = true;
    }

    private static void CollectSlots(IUIPrefabHolder holder, HashSet<int> slots)
    {
        var meshes = holder.UIMeshDatas;
        for (int i = 0; i < meshes.Count; i++)
        {
            var mesh = meshes[i];
            if (mesh != null && mesh.TextureIndex > 0)
                slots.Add(mesh.TextureIndex);
        }
    }
}
