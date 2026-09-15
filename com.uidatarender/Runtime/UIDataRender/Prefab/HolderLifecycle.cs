using System.Collections.Generic;

/// <summary>
/// Manages the lifecycle set of active <see cref="IUIPrefabHolder"/> instances and keeps
/// the <see cref="HolderNotifier"/> reverse index in sync.
/// Extracted from <see cref="UIPrefabManager"/> as part of the Facade split (TASK-20).
/// </summary>
public sealed class HolderLifecycle
{
    private readonly HashSet<IUIPrefabHolder> holders = new HashSet<IUIPrefabHolder>();
    private readonly HolderNotifier notifier;

    public HolderLifecycle(HolderNotifier notifier) => this.notifier = notifier;

    /// <summary>Adds a holder to the lifecycle set (notifier tracking deferred to <see cref="Track"/>).</summary>
    public void Add(IUIPrefabHolder holder) => holders.Add(holder);

    /// <summary>Removes a holder from the lifecycle set and the notifier reverse index.</summary>
    public void Remove(IUIPrefabHolder holder)
    {
        if (!holders.Remove(holder)) return;
        notifier.RemoveHolder(holder);
    }

    /// <summary>Re-syncs the notifier for a holder whose UIMeshDatas have been rebuilt.</summary>
    public void Refresh(IUIPrefabHolder holder)
    {
        if (!holders.Contains(holder)) return;
        notifier.RemoveHolder(holder);
        notifier.AddHolder(holder);
    }

    /// <summary>
    /// Adds the holder to both the lifecycle set and notifier atomically.
    /// Call only after <see cref="IUIPrefabHolder.UIMeshDatas"/> has been populated.
    /// </summary>
    public void Track(IUIPrefabHolder holder)
    {
        holders.Add(holder);
        notifier.AddHolder(holder);
    }

    public bool Contains(IUIPrefabHolder holder) => holders.Contains(holder);
}
