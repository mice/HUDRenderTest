using System.Collections.Generic;

/// <summary>
/// A single draw-call batch: a subset of holders sharing a texture slot set
/// that fits within MaxImageSlots.
/// </summary>
public class RenderBatch
{
    public List<IUIPrefabHolder> Holders { get; } = new List<IUIPrefabHolder>();

    /// <summary>Global image slot indices (1-based) required by all holders in this batch.</summary>
    private HashSet<int> usedSlots = new HashSet<int>();
    public HashSet<int> UsedSlots { get { return usedSlots; } }
    private readonly List<int> sortedSlots = new List<int>();
    private readonly Dictionary<int, int> localSlotMap = new Dictionary<int, int>();

    internal RenderBatch(IUIPrefabHolder firstHolder, HashSet<int> slots, int holderCapacity, int slotCapacity)
    {
        if (slotCapacity > 0)
            usedSlots = new HashSet<int>(slotCapacity);
        Reset(firstHolder, slots, holderCapacity);
    }

    internal void Reset(IUIPrefabHolder firstHolder, HashSet<int> slots, int holderCapacity)
    {
        if (holderCapacity > Holders.Capacity)
            Holders.Capacity = holderCapacity;
        Holders.Clear();
        UsedSlots.Clear();
        Holders.Add(firstHolder);
        AddSlots(slots);
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
        AddSlots(needed);
        return true;
    }

    private void AddSlots(HashSet<int> slots)
    {
        foreach (var slot in slots)
            UsedSlots.Add(slot);
    }

    /// <summary>
    /// Builds a mapping from each global slot index in this batch to a local
    /// 1-based slot index (1 = _MainTex1, 2 = _MainTex2, …).
    /// </summary>
    public IReadOnlyDictionary<int, int> BuildLocalSlotMap()
    {
        sortedSlots.Clear();
        foreach (var slot in UsedSlots)
            sortedSlots.Add(slot);
        sortedSlots.Sort();
        localSlotMap.Clear();
        for (int i = 0; i < sortedSlots.Count; i++)
            localSlotMap[sortedSlots[i]] = i + 1;
        return localSlotMap;
    }

    /// <summary>
    /// Returns the textures required by this batch, ordered by ascending global slot,
    /// suitable for passing to <see cref="MaterialBinder.Bind"/>.
    /// </summary>
    public List<UnityEngine.Texture> GetBatchTextures(IReadOnlyList<UnityEngine.Texture> globalTextures)
    {
        var sorted = new List<int>(UsedSlots);
        sorted.Sort();
        var list = new List<UnityEngine.Texture>(sorted.Count);
        foreach (var globalSlot in sorted)
        {
            int idx = globalSlot - 1;
            list.Add(idx >= 0 && idx < globalTextures.Count ? globalTextures[idx] : null);
        }
        return list;
    }
}
