using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Partitions a list of holders into <see cref="RenderBatch"/> instances using
/// greedy first-fit on image texture slot indices.
/// </summary>
public class MergeBatcher
{
    private readonly int _maxSlots;
    private readonly List<RenderBatch> batches = new List<RenderBatch>();
    private readonly List<RenderBatch> batchPool = new List<RenderBatch>();
    private readonly HashSet<int> slotScratch = new HashSet<int>();

    public MergeBatcher(int maxSlots)
    {
        _maxSlots = maxSlots;
    }

    /// <summary>
    /// Splits <paramref name="holders"/> into batches so that each batch uses at most
    /// <c>maxSlots</c> distinct image texture slot indices (TextureIndex &gt; 0).
    /// Logs a warning when more than one batch is required.
    /// </summary>
    public List<RenderBatch> Plan(IReadOnlyList<IUIPrefabHolder> holders)
    {
        batches.Clear();

        for (int h = 0; h < holders.Count; h++)
        {
            var holder = holders[h];
            slotScratch.Clear();
            CollectSlots(holder, slotScratch);
            bool placed = false;

            for (int b = 0; b < batches.Count; b++)
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

        return batches;
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
