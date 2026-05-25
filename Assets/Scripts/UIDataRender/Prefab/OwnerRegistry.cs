using System.Collections.Generic;

/// <summary>
/// Maps each <see cref="UIPrefabOwner"/> to its shared <see cref="UIPrefabRegistration"/>,
/// ensuring one registration is created per owner regardless of how many holders attach.
/// Extracted from <see cref="UIPrefabManager"/> as part of the Facade split (TASK-20).
/// </summary>
public sealed class OwnerRegistry
{
    private readonly Dictionary<UIPrefabOwner, UIPrefabRegistration> owners =
        new Dictionary<UIPrefabOwner, UIPrefabRegistration>();

    /// <summary>Read-only view of the owner→registration map; exposed for API compatibility.</summary>
    public IReadOnlyDictionary<UIPrefabOwner, UIPrefabRegistration> Owners => owners;

    public bool TryGet(UIPrefabOwner owner, out UIPrefabRegistration reg) =>
        owners.TryGetValue(owner, out reg);

    /// <summary>
    /// Returns the existing registration for <paramref name="owner"/>, or creates a new one
    /// and registers its textures via <paramref name="recorder"/>.
    /// </summary>
    public UIPrefabRegistration GetOrCreate(UIPrefabOwner owner, ITextureRecorder recorder)
    {
        if (!owners.TryGetValue(owner, out var reg))
        {
            reg = new UIPrefabRegistration(owner, recorder);
            owners.Add(owner, reg);
        }
        return reg;
    }
}
