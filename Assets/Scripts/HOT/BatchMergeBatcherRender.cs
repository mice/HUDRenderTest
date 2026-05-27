using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manual scene controller for MergeBatcher validation.
/// Keeps the BatchTextRenderer-style "press button to rebuild" workflow,
/// but renders through UIPrefabManager + MergeBatcher + HolderBatchRenderer.
/// </summary>
public class BatchMergeBatcherRender : MonoBehaviour, IPerfProbeSource
{
    public enum DisplayMode
    {
        UIAndDataRender = 0,
        UIOnly = 1,
        DataRenderOnly = 2
    }

    private static readonly int MainTex0 = Shader.PropertyToID("_MainTex0");

    public Font font;
    public Camera uiCamera;
    public Transform uiRoot;
    public Text statusText;
    public List<UIPrefabOwner> owners = new List<UIPrefabOwner>();
    public List<UIPrefabOwner> ownerPrefabs = new List<UIPrefabOwner>();
    public bool useSlim;
    public bool enable8TexSlots;
    public bool autoRecreateOnStart = true;
    public bool rebuildEveryFrame;
    public DisplayMode displayMode = DisplayMode.DataRenderOnly;
    public bool instantiateOwnersAtRuntime;
    public Transform runtimeSourceRoot;
    public Vector3 runtimeStartPosition;
    public Vector3 runtimeStep = new Vector3(0f, -60f, 0f);
    public string csvTag = "merge_batcher_scene";

    [Header("Manual edit")]
    public int targetOwnerIndex;
    public int targetDrawIndex = 2;
    public string textOverride = "HUD-Edited";
    public Sprite spriteOverride;

    [Header("Manual sprite cycle")]
    public int cycleOwnerIndex;
    public int cycleDrawIndex = 2;
    public Sprite[] spriteCycle = System.Array.Empty<Sprite>();

    [Button(nameof(ReCreate))]
    public string _recreate;
    [Button(nameof(ApplyTextOverride))]
    public string _applyText;
    [Button(nameof(ApplySpriteOverride))]
    public string _applySprite;
    [Button(nameof(CycleSprite))]
    public string _cycleSprite;
    [Button(nameof(FlushProbe))]
    public string _flushProbe;
    [Button(nameof(OpenCsvFolder))]
    public string _openCsvFolder;

    private readonly List<IUIPrefabHolder> runtimeHolders = new List<IUIPrefabHolder>();
    private readonly Dictionary<IUIPrefabHolder, UIPrefabOwner> ownerMap = new Dictionary<IUIPrefabHolder, UIPrefabOwner>();
    private readonly Dictionary<IUIPrefabHolder, Vector3> positionMap = new Dictionary<IUIPrefabHolder, Vector3>();
    private readonly List<UIPrefabOwner> activeOwners = new List<UIPrefabOwner>();
    private readonly List<GameObject> instantiatedOwnerObjects = new List<GameObject>();
    private readonly Dictionary<UIPrefabOwner, SourceCanvasGroupState> sourceCanvasGroups = new Dictionary<UIPrefabOwner, SourceCanvasGroupState>();
    private readonly UIPrefabManager uiPrefabManager = UIPrefabManager.Instance;

    private HolderBatchRenderer batchRenderer;
    private int cycleSpriteIndex;
    private bool pendingFontRebuildRefresh;
    private bool suppressFontTextureRebuildCallback;
    private DisplayMode appliedDisplayMode;
    private bool displayModeInitialized;

    public UIData.PerfProbe Probe => batchRenderer != null ? batchRenderer.Probe : null;
    public int BatchCount => batchRenderer != null ? batchRenderer.BatchCount : 0;
    public string LastCsvPath { get; private set; }

    private void OnEnable()
    {
        Font.textureRebuilt += OnFontTextureRebuilt;
    }

    private void OnDisable()
    {
        Font.textureRebuilt -= OnFontTextureRebuilt;
    }

    private void Start()
    {
        if (autoRecreateOnStart)
            ReCreate();
    }

    public void ReCreate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Only Run In PlayMode");
            return;
        }

        ClearRuntimeHolders();
        batchRenderer?.Dispose();
        batchRenderer = new HolderBatchRenderer(enable8TexSlots ? 7 : 3);
        uiPrefabManager.SetBatchTextureLimit(enable8TexSlots ? 7 : 3);

        CreateActiveOwners();
        PrepareOwnerFonts();

        foreach (var owner in activeOwners)
        {
            if (owner == null)
                continue;

            IUIPrefabHolder holder = useSlim
                ? new DataPrefabHolder<UIMeshDataX>()
                : new DataPrefabHolder<UIMeshData>();

            holder.SetTarget(owner);
            uiPrefabManager.Register(holder);
            uiPrefabManager.Generate(holder);

            runtimeHolders.Add(holder);
            ownerMap[holder] = owner;
            positionMap[holder] = owner.transform.localPosition;
        }

        ApplyDisplayMode(true);
        RebuildMesh();
    }

    public void ApplyTextOverride()
    {
        if (!TryGetHolder(targetOwnerIndex, out var holder))
            return;

        holder.SetText(targetDrawIndex, textOverride);
        RegenerateAllHolders();
    }

    public void ApplySpriteOverride()
    {
        if (!TryGetHolder(targetOwnerIndex, out var holder))
            return;

        holder.SetSprite(targetDrawIndex, spriteOverride);
        RebuildMesh();
    }

    public void CycleSprite()
    {
        if (spriteCycle == null || spriteCycle.Length == 0)
            return;
        if (!TryGetHolder(cycleOwnerIndex, out var holder))
            return;

        var sprite = spriteCycle[cycleSpriteIndex % spriteCycle.Length];
        cycleSpriteIndex++;
        holder.SetSprite(cycleDrawIndex, sprite);
        RebuildMesh();
    }

    public void FlushProbe()
    {
        if (Probe == null)
            return;

        LastCsvPath = Probe.Flush(csvTag);
        UpdateStatusText();
    }

    public void OpenCsvFolder()
    {
        string directory = string.IsNullOrEmpty(LastCsvPath)
            ? Application.persistentDataPath
            : Path.GetDirectoryName(LastCsvPath);

        if (string.IsNullOrEmpty(directory))
            directory = Application.persistentDataPath;

        Application.OpenURL(new System.Uri(directory).AbsoluteUri);
    }

    private void Update()
    {
        if (pendingFontRebuildRefresh)
        {
            pendingFontRebuildRefresh = false;
            RegenerateAllHolders();
        }

        if (displayModeInitialized && appliedDisplayMode != displayMode)
            ApplyDisplayMode(false);

        if (rebuildEveryFrame && runtimeHolders.Count > 0)
            RebuildMesh();
    }

    private void LateUpdate()
    {
        if (batchRenderer == null || uiRoot == null || displayMode == DisplayMode.UIOnly)
            return;

        batchRenderer.Draw(uiRoot.localToWorldMatrix, 5, uiCamera);
    }

    private void OnDestroy()
    {
        ClearRuntimeHolders();
        batchRenderer?.Dispose();
        batchRenderer = null;
    }

    private void RebuildMesh()
    {
        if (batchRenderer == null)
            return;

        UpdatePositions();
        batchRenderer.Rebuild(runtimeHolders, positionMap, CreateMaterial, BindBatchTextures);
        UpdateStatusText();
    }

    private void BindBatchTextures(Material material, RenderBatch batch)
    {
        material.SetTexture(MainTex0, font != null ? font.material.mainTexture : null);
        uiPrefabManager.UpdateTexture(material, batch);
    }

    private void RegenerateAllHolders()
    {
        if (runtimeHolders.Count == 0)
            return;

        suppressFontTextureRebuildCallback = true;
        try
        {
            PrepareOwnerFonts();
            for (int i = 0; i < runtimeHolders.Count; i++)
                uiPrefabManager.Generate(runtimeHolders[i]);
        }
        finally
        {
            suppressFontTextureRebuildCallback = false;
        }

        pendingFontRebuildRefresh = false;
        RebuildMesh();
    }

    private void PrepareOwnerFonts()
    {
        foreach (var owner in activeOwners)
        {
            if (owner == null)
                continue;

            var texts = owner.GetComponentsInChildren<UIText>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (text == null || text.font == null || string.IsNullOrEmpty(text.text))
                    continue;

                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            }
        }
    }

    private void OnFontTextureRebuilt(Font rebuiltFont)
    {
        if (rebuiltFont == null || suppressFontTextureRebuildCallback || runtimeHolders.Count == 0)
            return;

        if (UsesFont(rebuiltFont))
            pendingFontRebuildRefresh = true;
    }

    private bool UsesFont(Font rebuiltFont)
    {
        if (font == rebuiltFont)
            return true;
        if (statusText != null && statusText.font == rebuiltFont)
            return true;

        foreach (var owner in activeOwners)
        {
            if (owner == null)
                continue;

            var texts = owner.GetComponentsInChildren<UIText>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].font == rebuiltFont)
                    return true;
            }
        }
        return false;
    }

    private void UpdatePositions()
    {
        foreach (var pair in ownerMap)
            positionMap[pair.Key] = pair.Value != null ? pair.Value.transform.localPosition : Vector3.zero;
    }

    private Material CreateMaterial()
    {
        var material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        material.SetTexture(MainTex0, font != null ? font.material.mainTexture : null);
        material.renderQueue = 3000;
        if (enable8TexSlots)
            uiPrefabManager.Enable8TexSlots(material);
        return material;
    }

    private bool TryGetHolder(int index, out IUIPrefabHolder holder)
    {
        if (index < 0 || index >= runtimeHolders.Count)
        {
            holder = null;
            return false;
        }

        holder = runtimeHolders[index];
        return true;
    }

    private void ClearRuntimeHolders()
    {
        SetSourceGraphicsVisible(true);
        appliedDisplayMode = DisplayMode.DataRenderOnly;
        displayModeInitialized = false;

        foreach (var holder in runtimeHolders)
        {
            uiPrefabManager.RemoveHolder(holder);
            var meshes = holder.UIMeshDatas;
            if (meshes == null)
                continue;

            for (int i = 0; i < meshes.Count; i++)
                meshes[i]?.Dispose();
        }

        runtimeHolders.Clear();
        ownerMap.Clear();
        positionMap.Clear();
        activeOwners.Clear();

        for (int i = 0; i < instantiatedOwnerObjects.Count; i++)
        {
            var obj = instantiatedOwnerObjects[i];
            if (obj == null)
                continue;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }
        instantiatedOwnerObjects.Clear();
    }

    private void CreateActiveOwners()
    {
        activeOwners.Clear();

        if (!instantiateOwnersAtRuntime)
        {
            activeOwners.AddRange(owners);
            return;
        }

        var parent = runtimeSourceRoot != null ? runtimeSourceRoot : transform;
        Vector3 position = runtimeStartPosition;
        int j = 0;
        int count = 3;
        foreach (var ownerPrefab in ownerPrefabs)
        {
            position = runtimeStartPosition;
            if (ownerPrefab == null)
                continue;
            for (int i = 0; i < count; i++)
            {
                j++;
                var instance = Instantiate(ownerPrefab, parent);
                instance.gameObject.name = $"{ownerPrefab.name}_RuntimeSource_{i}";
                instance.transform.localPosition = position + new Vector3((j % 3) * runtimeStep.x, (j / 3) * runtimeStep.y, 0);
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                instantiatedOwnerObjects.Add(instance.gameObject);
                activeOwners.Add(instance);
            }
        }
    }

    private void ApplyDisplayMode(bool force)
    {
        if (!force && displayModeInitialized && appliedDisplayMode == displayMode)
            return;

        SetSourceGraphicsVisible(displayMode != DisplayMode.DataRenderOnly);
        appliedDisplayMode = displayMode;
        displayModeInitialized = true;
        UpdateStatusText();
    }

    private void SetSourceGraphicsVisible(bool visible)
    {
        if (visible)
        {
            foreach (var pair in sourceCanvasGroups)
            {
                var state = pair.Value;
                if (state.CanvasGroup == null)
                    continue;

                state.CanvasGroup.alpha = state.OriginalAlpha;
                state.CanvasGroup.interactable = state.OriginalInteractable;
                state.CanvasGroup.blocksRaycasts = state.OriginalBlocksRaycasts;
                if (state.CreatedByRunner)
                {
                    if (Application.isPlaying)
                        Destroy(state.CanvasGroup);
                    else
                        DestroyImmediate(state.CanvasGroup);
                }
            }
            sourceCanvasGroups.Clear();
            return;
        }

        sourceCanvasGroups.Clear();
        foreach (var owner in activeOwners)
        {
            if (owner == null)
                continue;

            bool created = false;
            if (!owner.TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                canvasGroup = owner.gameObject.AddComponent<CanvasGroup>();
                created = true;
            }
            sourceCanvasGroups[owner] = new SourceCanvasGroupState(
                canvasGroup,
                canvasGroup.alpha,
                canvasGroup.interactable,
                canvasGroup.blocksRaycasts,
                created);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private readonly struct SourceCanvasGroupState
    {
        public SourceCanvasGroupState(
            CanvasGroup canvasGroup,
            float originalAlpha,
            bool originalInteractable,
            bool originalBlocksRaycasts,
            bool createdByRunner)
        {
            CanvasGroup = canvasGroup;
            OriginalAlpha = originalAlpha;
            OriginalInteractable = originalInteractable;
            OriginalBlocksRaycasts = originalBlocksRaycasts;
            CreatedByRunner = createdByRunner;
        }

        public CanvasGroup CanvasGroup { get; }
        public float OriginalAlpha { get; }
        public bool OriginalInteractable { get; }
        public bool OriginalBlocksRaycasts { get; }
        public bool CreatedByRunner { get; }
    }

    private void UpdateStatusText()
    {
        if (statusText == null)
            return;

        string next = $"owners:{runtimeHolders.Count} batches:{BatchCount} probe:{(Probe != null ? Probe.Count : 0)} mode:{displayMode}\n{LastCsvPath}";
        if (statusText.text != next)
            statusText.text = next;
    }
}
