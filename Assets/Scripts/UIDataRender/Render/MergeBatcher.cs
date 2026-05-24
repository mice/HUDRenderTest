using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Partitions a list of holders into <see cref="RenderBatch"/> instances using
/// greedy first-fit on image texture slot indices.
/// </summary>
public class MergeBatcher
{
    private readonly int _maxSlots;

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
        var batches = new List<RenderBatch>();

        foreach (var holder in holders)
        {
            var needed = CollectSlots(holder);
            bool placed = false;

            foreach (var batch in batches)
            {
                if (batch.TryAdd(holder, needed, _maxSlots))
                {
                    placed = true;
                    break;
                }
            }

            if (!placed)
                batches.Add(new RenderBatch(holder, needed));
        }

        if (batches.Count > 1)
            Debug.LogWarning($"[MergeBatcher] split into {batches.Count} batches");

        return batches;
    }

    private static HashSet<int> CollectSlots(IUIPrefabHolder holder)
    {
        var slots = new HashSet<int>();
        foreach (var mesh in holder.UIMeshDatas)
        {
            if (mesh != null && mesh.TextureIndex > 0)
                slots.Add(mesh.TextureIndex);
        }
        return slots;
    }
}
