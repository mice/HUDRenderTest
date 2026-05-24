using System.Collections.Generic;

/// <summary>
/// A single draw-call batch: a subset of holders sharing a texture slot set
/// that fits within MaxImageSlots.
/// </summary>
public class RenderBatch
{
    public List<IUIPrefabHolder> Holders { get; } = new List<IUIPrefabHolder>();

    /// <summary>Global image slot indices (1-based) required by all holders in this batch.</summary>
    public HashSet<int> UsedSlots { get; } = new HashSet<int>();

    internal RenderBatch(IUIPrefabHolder firstHolder, HashSet<int> slots)
    {
        Holders.Add(firstHolder);
        UsedSlots.UnionWith(slots);
    }

    /// <summary>
    /// Tries to add <paramref name="holder"/> to this batch.
    /// Returns true and updates UsedSlots only if the combined slot count stays
    /// within <paramref name="maxSlots"/>.
    /// </summary>
    internal bool TryAdd(IUIPrefabHolder holder, HashSet<int> needed, int maxSlots)
    {
        int combined = UsedSlots.Count;
        foreach (var s in needed)
        {
            if (!UsedSlots.Contains(s)) combined++;
        }
        if (combined > maxSlots) return false;
        Holders.Add(holder);
        UsedSlots.UnionWith(needed);
        return true;
    }
}
