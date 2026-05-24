using System.Collections.Generic;

/// <summary>
/// Maintains a reverse index (slot → IUIData list) and propagates
/// TextureSlotTable slot-change events only to the affected meshes.
/// Reduces notification complexity from O(holders × meshdata) to O(affected meshes).
/// </summary>
public sealed class HolderNotifier
{
    private readonly Dictionary<int, List<IUIData>> _slotToMeshes
        = new Dictionary<int, List<IUIData>>();

    public HolderNotifier(TextureSlotTable slotTable)
    {
        slotTable.SlotReplaced += OnSlotReplaced;
        slotTable.SlotRemoved += OnSlotRemoved;
    }

    /// <summary>Adds a single mesh to the reverse index using its current TextureIndex.</summary>
    public void Track(IUIData mesh)
    {
        if (mesh == null) return;
        GetOrCreate(mesh.TextureIndex).Add(mesh);
    }

    /// <summary>Removes a single mesh from the reverse index.</summary>
    public void Untrack(IUIData mesh)
    {
        if (mesh == null) return;
        if (_slotToMeshes.TryGetValue(mesh.TextureIndex, out var list))
        {
            list.Remove(mesh);
            if (list.Count == 0)
                _slotToMeshes.Remove(mesh.TextureIndex);
        }
    }

    /// <summary>Registers all IUIData belonging to a holder into the reverse index.</summary>
    public void AddHolder(IUIPrefabHolder holder)
    {
        if (holder == null) return;
        foreach (var mesh in holder.UIMeshDatas)
            Track(mesh);
    }

    /// <summary>Unregisters all IUIData belonging to a holder from the reverse index.</summary>
    public void RemoveHolder(IUIPrefabHolder holder)
    {
        if (holder == null) return;
        foreach (var mesh in holder.UIMeshDatas)
            Untrack(mesh);
    }

    /// <summary>
    /// Updates all meshes mapped to slot <paramref name="from"/> to reference slot
    /// <paramref name="to"/>, then moves them to the to-bucket in the reverse index.
    /// </summary>
    public void ReplaceTextureID(int from, int to)
    {
        if (!_slotToMeshes.TryGetValue(from, out var list)) return;
        _slotToMeshes.Remove(from);
        var toList = GetOrCreate(to);
        foreach (var mesh in list)
        {
            mesh.UpdateTextureIndex(to);
            toList.Add(mesh);
        }
    }

    /// <summary>
    /// Sets TextureIndex to -1 for all meshes mapped to the given slot
    /// and removes them from the reverse index.
    /// </summary>
    public void RemoveTextureID(int index)
    {
        if (!_slotToMeshes.TryGetValue(index, out var list)) return;
        _slotToMeshes.Remove(index);
        foreach (var mesh in list)
            mesh.UpdateTextureIndex(-1);
    }

    private void OnSlotReplaced(int from, int to) => ReplaceTextureID(from, to);
    private void OnSlotRemoved(int index) => RemoveTextureID(index);

    private List<IUIData> GetOrCreate(int slot)
    {
        if (!_slotToMeshes.TryGetValue(slot, out var list))
        {
            list = new List<IUIData>();
            _slotToMeshes[slot] = list;
        }
        return list;
    }
}
