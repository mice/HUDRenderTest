using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TextureSlotTable
{
    private const int DefaultMaxImageSlots = 3;
    private const int MaxSupportedImageSlots = 7;

    private readonly List<Texture> textures = new List<Texture>();
    private readonly Dictionary<int, Texture> ownerToTexture = new Dictionary<int, Texture>();
    private readonly Dictionary<int, HashSet<int>> slotToOwners = new Dictionary<int, HashSet<int>>();

    public int MaxImageSlots { get; private set; }
    public IReadOnlyList<Texture> Textures => textures;

    public event Action<int, int> SlotReplaced;
    public event Action<int> SlotRemoved;

    public TextureSlotTable(int maxImageSlots = DefaultMaxImageSlots)
    {
        if (maxImageSlots < 1)
        {
            maxImageSlots = 1;
        }
        else if (maxImageSlots > MaxSupportedImageSlots)
        {
            maxImageSlots = MaxSupportedImageSlots;
        }

        MaxImageSlots = maxImageSlots;
    }

    /// <summary>
    /// Expand the slot capacity up to <see cref="MaxSupportedImageSlots"/>.
    /// Has no effect if <paramref name="newMax"/> is not greater than the current limit.
    /// Safe to call at any time; existing registrations are unaffected.
    /// </summary>
    public bool ExpandTo(int newMax)
    {
        if (newMax <= MaxImageSlots) return false;
        MaxImageSlots = Mathf.Min(newMax, MaxSupportedImageSlots);
        return true;
    }

    public int GetSlot(Texture texture)
    {
        if (texture == null)
        {
            return -1;
        }

        var index = textures.IndexOf(texture);
        return index >= 0 ? index + 1 : -1;
    }

    public int Register(int ownerKey, Texture texture)
    {
        if (ownerToTexture.TryGetValue(ownerKey, out var oldTexture))
        {
            if (oldTexture == texture)
            {
                var existingSlot = GetSlot(texture);
                if (existingSlot != -1)
                {
                    AddOwnerToSlot(existingSlot, ownerKey);
                    return existingSlot;
                }

                // Stale mapping (e.g. previous over-capacity failure): allow re-register.
                ownerToTexture.Remove(ownerKey);
            }
            else
            {
                Unregister(ownerKey);
            }
        }

        if (texture == null)
        {
            return -1;
        }

        var slot = GetSlot(texture);
        if (slot != -1)
        {
            ownerToTexture[ownerKey] = texture;
            AddOwnerToSlot(slot, ownerKey);
            return slot;
        }

        if (textures.Count >= MaxImageSlots)
        {
            UnityEngine.Debug.LogWarning($"[TextureSlotTable] texture count exceeds per-batch limit ({textures.Count + 1}/{MaxImageSlots}); draw calls will be split by MergeBatcher.");
        }

        textures.Add(texture);
        slot = textures.Count;
        ownerToTexture[ownerKey] = texture;
        AddOwnerToSlot(slot, ownerKey);
        return slot;
    }

    public void Unregister(int ownerKey)
    {
        if (!ownerToTexture.TryGetValue(ownerKey, out var texture))
        {
            return;
        }

        ownerToTexture.Remove(ownerKey);

        if (texture == null)
        {
            return;
        }

        var slot = GetSlot(texture);
        if (slot == -1)
        {
            return;
        }

        if (!slotToOwners.TryGetValue(slot, out var owners))
        {
            return;
        }

        owners.Remove(ownerKey);
        if (owners.Count > 0)
        {
            return;
        }

        slotToOwners.Remove(slot);

        var removedIndex = slot - 1;
        var lastIndex = textures.Count - 1;
        if (removedIndex == lastIndex)
        {
            textures.RemoveAt(lastIndex);
            SlotRemoved?.Invoke(slot);
            return;
        }

        textures[removedIndex] = textures[lastIndex];
        textures.RemoveAt(lastIndex);

        var lastSlot = lastIndex + 1;
        if (slotToOwners.TryGetValue(lastSlot, out var movedOwners))
        {
            slotToOwners.Remove(lastSlot);
            slotToOwners[slot] = movedOwners;
        }
        else
        {
            slotToOwners[slot] = new HashSet<int>();
        }

        SlotReplaced?.Invoke(lastSlot, slot);
    }

    private void AddOwnerToSlot(int slot, int ownerKey)
    {
        if (!slotToOwners.TryGetValue(slot, out var owners))
        {
            owners = new HashSet<int>();
            slotToOwners.Add(slot, owners);
        }

        owners.Add(ownerKey);
    }
}
