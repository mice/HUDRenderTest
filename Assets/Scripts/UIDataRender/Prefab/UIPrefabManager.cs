using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public interface IUIPrefabDataOwner
{
    IUIPrefabHolder DataHolder { get; }
}

/// <summary>
/// </summary>
public interface IUIPrefabHolder
{
    UIPrefabOwner Target { get; }
    Vector3 Position { get; }
    UIPrefabRegistration wrapper { get; }

    void SetTarget(UIPrefabOwner target);

    IList<IUIData> UIMeshDatas { get; }
    void SetWrapper(UIPrefabRegistration wrapper);
    void BuildMesh(IUIDrawTarget[] draws);

    void Fill(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Vector3 localPosition);

    void Fill(List<int> triangles_, Vector3 localPosition);
}

public interface ITextureRecorder
{
    void OnTextureRegister(int guid,Texture obj);
    void OnTextureUnRegister(int guid);
}

public interface ITextureNotify
{
    void ReplaceTextureID(int lastIndex, int tIndex);
    void RemoveTextureID(int lastIndex);
}


public class UIPrefabWrapper : IDisposable
{
    public UIPrefabOwner target;
    /// <summary>
    /// UI element
    /// </summary>
    public List<IUIDrawTarget> draws = new List<IUIDrawTarget>();
    /// <summary>
    /// </summary>
    public List<UIMeshData> uIMeshDatas = new List<UIMeshData>();

    public void Dispose()
    {
        draws.Clear();
        uIMeshDatas.Clear();
    }
}

/// <summary>
/// </summary>
public partial class UIPrefabRegistration
{
    /// <summary>
    /// </summary>
    public ITextureRecorder TextureCall;
    public UIPrefabOwner Owner { get; private set; }
    /// <summary>
    /// UI element
    /// </summary>
    public IUIDrawTarget[] draws;

    public UIPrefabRegistration(UIPrefabOwner owner, ITextureRecorder textureCall)
    {
        this.Owner = owner;
        TextureCall = textureCall;
        RecordDraws();
    }

    private void RecordDraws()
    {
        draws = new IUIDrawTarget[Owner.targets.Count];
        for (int i = 0; i < Owner.targets.Count; i++)
        {
            draws[i] = Owner.targets[i].GetComponent<IUIDrawTarget>();
            if (draws[i] is UIImage uiImg)
            {
                TextureCall.OnTextureRegister(uiImg.GetInstanceID(), uiImg.sprite? uiImg.sprite.texture:null);
            }
        }
    }

    internal void Generate(IUIPrefabHolder holder)
    {
        holder.BuildMesh(draws);
    }
}

/// <summary>
/// Facade over four internal components: <see cref="TextureSlotTable"/>,
/// <see cref="HolderNotifier"/>, <see cref="OwnerRegistry"/>, and <see cref="HolderLifecycle"/>.
/// All state and logic are delegated to these classes; UIPrefabManager exposes the public API only.
/// </summary>
public class UIPrefabManager : ITextureRecorder, ITextureNotify
{
    public static UIPrefabManager Instance { get; } = new UIPrefabManager();

    private readonly TextureSlotTable textureSlots;
    private readonly HolderNotifier holderNotifier;
    private readonly OwnerRegistry ownerRegistry;
    private readonly HolderLifecycle holderLifecycle;

    /// <summary>
    /// Read-only view of the owner→registration map. Preserved from the pre-facade API for
    /// compatibility; prefer <see cref="Register"/> and <see cref="Generate"/> for mutations.
    /// </summary>
    public IReadOnlyDictionary<UIPrefabOwner, UIPrefabRegistration> owners => ownerRegistry.Owners;

    private UIPrefabManager()
    {
        textureSlots    = new TextureSlotTable();
        holderNotifier  = new HolderNotifier(textureSlots);
        ownerRegistry   = new OwnerRegistry();
        holderLifecycle = new HolderLifecycle(holderNotifier);
    }

    void ITextureNotify.ReplaceTextureID(int lastIndex, int tIndex) =>
        holderNotifier.ReplaceTextureID(lastIndex, tIndex);

    void ITextureNotify.RemoveTextureID(int lastIndex) =>
        holderNotifier.RemoveTextureID(lastIndex);

    /// <summary>Registers a holder in the lifecycle set; notifier tracking deferred to Generate/RefreshHolder.</summary>
    public void AddHolder(IUIPrefabHolder holder) => holderLifecycle.Add(holder);

    public void RemoveHolder(IUIPrefabHolder holder) => holderLifecycle.Remove(holder);

    /// <summary>
    /// Re-syncs the notifier reverse index after a holder's UIMeshDatas have been
    /// rebuilt outside of Generate (e.g. the UIPrefabHolder.BuildMesh direct path).
    /// </summary>
    public void RefreshHolder(IUIPrefabHolder holder) => holderLifecycle.Refresh(holder);

    public int GetTextureIndex(Texture texture)
    {
        var slot = textureSlots.GetSlot(texture);
        LogDebug($"GetTextureIndex:{slot}");
        return slot;
    }

    public void Register(IUIPrefabHolder holder)
    {
        var prefabOwner = holder?.Target;
        if (prefabOwner == null)
        {
            LogDebug("Should Not Be Null::");
            return;
        }
        var reg = ownerRegistry.GetOrCreate(prefabOwner, this);
        holder.SetWrapper(reg);  // always set wrapper, even for subsequent holders with the same owner
    }

    void ITextureRecorder.OnTextureRegister(int guid, Texture obj)
    {
        var slot = textureSlots.Register(guid, obj);
        LogDebug($"RegisterTexture guid:{guid}, slot:{slot}, tex:{obj}");
    }

    void ITextureRecorder.OnTextureUnRegister(int guid)
    {
        textureSlots.Unregister(guid);
        LogDebug($"UnregisterTexture guid:{guid}");
    }

    public UIPrefabRegistration Generate(IUIPrefabHolder holder)
    {
        if (ownerRegistry.TryGet(holder.Target, out var reg))
        {
            holderNotifier.RemoveHolder(holder);  // untrack stale mesh objects
            reg.Generate(holder);                  // populate UIMeshDatas
            holderLifecycle.Track(holder);         // add to set + notifier atomically
            return reg;
        }
        return null;
    }

    /// <summary>
    /// Moves a single mesh from its current slot bucket to the new bucket after
    /// its TextureIndex has been updated externally (e.g. SetSprite, SetTextureIndex).
    /// Call Untrack BEFORE the TextureIndex changes and Track AFTER.
    /// </summary>
    public void UntrackMesh(IUIData mesh) => holderNotifier.Untrack(mesh);

    public void TrackMesh(IUIData mesh) => holderNotifier.Track(mesh);

    public void UpdateTexture(Material comb_Material) =>
        MaterialBinder.Bind(comb_Material, textureSlots.Textures);

    /// <summary>
    /// Returns the global texture list managed by this manager's <see cref="TextureSlotTable"/>.
    /// Index 0 = slot 1 (font), index 1 = slot 2 (_MainTex1), …
    /// </summary>
    public IReadOnlyList<UnityEngine.Texture> GetTextures() => textureSlots.Textures;

    /// <summary>
    /// Binds the subset of textures required by <paramref name="batch"/> to <paramref name="material"/>,
    /// remapped to local slots 1..N.  Use this when rendering with <see cref="MergeBatcher"/>.
    /// </summary>
    public void UpdateTexture(Material material, RenderBatch batch) =>
        MaterialBinder.Bind(material, batch.GetBatchTextures(textureSlots.Textures));

    private const string Keyword8Tex = "HUD_8_TEX_SLOTS";

    /// <summary>
    /// Activates 8-texture-slot mode: enables the <c>HUD_8_TEX_SLOTS</c> shader keyword on
    /// <paramref name="material"/> and expands <see cref="TextureSlotTable"/> capacity to 7 image
    /// slots so that <see cref="Register"/> can accept up to 7 distinct image textures.
    /// Call once after material creation. Safe to call repeatedly (idempotent).
    /// </summary>
    public void Enable8TexSlots(Material material)
    {
        material?.EnableKeyword(Keyword8Tex);
        textureSlots.ExpandTo(7);
    }

    /// <summary>
    /// Deactivates 8-texture-slot mode on <paramref name="material"/>.
    /// The <see cref="TextureSlotTable"/> capacity is not reduced; existing registrations above
    /// slot 3 will still be tracked but will render incorrectly until the keyword is re-enabled.
    /// </summary>
    public void Disable8TexSlots(Material material) => material?.DisableKeyword(Keyword8Tex);

    [Conditional("UI_VERBOSE")]
    private static void LogDebug(string message)
    {
        UnityEngine.Debug.Log(message);
    }
}
