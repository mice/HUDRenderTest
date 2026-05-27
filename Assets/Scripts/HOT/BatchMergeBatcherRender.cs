using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manual scene controller for MergeBatcher validation.
/// Keeps the BatchTextRenderer-style "press button to rebuild" workflow,
/// but renders through UIPrefabManager + MergeBatcher + HolderBatchRenderer.
/// </summary>
public class BatchMergeBatcherRender : MonoBehaviour, IPerfProbeSource, IEightSlotPerfTarget, IDynamicSwapTarget
{
    public enum DisplayMode
    {
        UIAndDataRender = 0,
        UIOnly = 1,
        DataRenderOnly = 2
    }

    public enum CycleOwnerSelectionMode
    {
        RuntimeIndex = 0,
        PrefabAndInstance = 1
    }

    public enum CycleDrawSelectionMode
    {
        DrawIndex = 0,
        TargetName = 1
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
    public int runtimeInstancesPerPrefab = 3;
    public Transform runtimeSourceRoot;
    public Vector3 runtimeStartPosition;
    public Vector3 runtimeStep = new Vector3(0f, -60f, 0f);
    public string csvTag = "merge_batcher_scene";
    public EightSlotPerfRunner comparisonRunner;
    public DynamicSwapRunner dynamicSwapRunner;

    [Header("Manual edit")]
    public int targetOwnerIndex;
    public int targetDrawIndex = 2;
    public string textOverride = "HUD-Edited";
    public Sprite spriteOverride;

    [Header("Manual sprite cycle")]
    public CycleOwnerSelectionMode cycleOwnerSelectionMode = CycleOwnerSelectionMode.PrefabAndInstance;
    public int cycleOwnerIndex;
    public int cycleOwnerPrefabIndex;
    public int cycleOwnerInstanceIndex;
    public CycleDrawSelectionMode cycleDrawSelectionMode = CycleDrawSelectionMode.TargetName;
    public int cycleDrawIndex = 2;
    public string cycleDrawTargetName = string.Empty;
    public Sprite[] spriteCycle = System.Array.Empty<Sprite>();

    [Button(nameof(ReCreate))]
    public string _recreate;
    [Button(nameof(ApplyTextOverride))]
    public string _applyText;
    [Button(nameof(ApplySpriteOverride))]
    public string _applySprite;
    [Button(nameof(CycleSprite))]
    public string _cycleSprite;
    [Button(nameof(StartDynamicSwapSequence))]
    public string _startDynamicSwapSequence;
    [Button(nameof(FlushProbe))]
    public string _flushProbe;
    [Button(nameof(StartEightSlotComparison))]
    public string _startEightSlotComparison;
    [Button(nameof(ReportBatchLayout))]
    public string _reportBatchLayout;
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
    private string actionMessage;

    public UIData.PerfProbe Probe => batchRenderer != null ? batchRenderer.Probe : null;
    public int BatchCount => batchRenderer != null ? batchRenderer.BatchCount : 0;
    public int BatchTextureLimit => enable8TexSlots ? 7 : 3;
    public int UniqueImageTextureCount => CountUniqueImageTextures();
    public bool IsOverCapacityActive => UniqueImageTextureCount > BatchTextureLimit;
    public string LastCsvPath { get; private set; }
    public bool Enable8TexSlots { get => enable8TexSlots; set => enable8TexSlots = value; }
    public bool RebuildEveryFrame { get => rebuildEveryFrame; set => rebuildEveryFrame = value; }
    public string CsvTag { get => csvTag; set => csvTag = value; }

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
        {
            ReportAction("CycleSprite skipped: spriteCycle is empty.", logWarning: true);
            return;
        }

        if (!TryResolveCycleHolderIndex(runtimeHolders.Count, out int resolvedIndex, out string reason))
        {
            ReportAction(reason, logWarning: true);
            return;
        }

        if (!TryGetHolder(resolvedIndex, out var holder))
        {
            ReportAction($"CycleSprite skipped: resolved holder index {resolvedIndex} is out of range.", logWarning: true);
            return;
        }

        if (!ownerMap.TryGetValue(holder, out var owner) || owner == null)
        {
            ReportAction($"CycleSprite skipped: owner for holder index {resolvedIndex} is unavailable.", logWarning: true);
            return;
        }

        if (!TryResolveCycleDrawIndex(owner, holder.UIMeshDatas != null ? holder.UIMeshDatas.Count : 0, out int resolvedDrawIndex, out reason))
        {
            ReportAction(reason, logWarning: true);
            return;
        }

        var sprite = spriteCycle[cycleSpriteIndex % spriteCycle.Length];
        cycleSpriteIndex++;
        holder.SetSprite(resolvedDrawIndex, sprite);
        RebuildMesh();
        string drawName = TryGetTargetName(owner, resolvedDrawIndex);
        ReportAction($"CycleSprite applied: holder={resolvedIndex}, draw={resolvedDrawIndex}({drawName}), sprite={(sprite != null ? sprite.name : "<null>")}, step={cycleSpriteIndex}/{spriteCycle.Length}");
    }

    public void FlushProbe()
    {
        if (Probe == null)
            return;

        LastCsvPath = Probe.Flush(BuildProbeTag(), BuildProbeContext());
        UpdateStatusText();
    }

    public void StartEightSlotComparison()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Only Run In PlayMode");
            return;
        }

        var runner = comparisonRunner != null ? comparisonRunner : GetComponent<EightSlotPerfRunner>();
        if (runner == null)
        {
            runner = gameObject.AddComponent<EightSlotPerfRunner>();
            runner.autoStart = false;
            if (!string.IsNullOrWhiteSpace(csvTag))
                runner.baseTag = $"{csvTag}_compare";
            comparisonRunner = runner;
        }

        if (runner.target == null && runner.targetBehaviour == null)
            runner.target = this;

        runner.StartComparison();
    }

    public void StartDynamicSwapSequence()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Only Run In PlayMode");
            return;
        }

        var runner = dynamicSwapRunner != null ? dynamicSwapRunner : GetComponent<DynamicSwapRunner>();
        if (runner == null)
        {
            runner = gameObject.AddComponent<DynamicSwapRunner>();
            runner.autoStart = false;
            if (!string.IsNullOrWhiteSpace(csvTag))
                runner.baseTag = $"{csvTag}_dynamic_swap";
            dynamicSwapRunner = runner;
        }

        if (runner.outputText == null && statusText != null)
            runner.outputText = statusText;

        if (runner.target == null && runner.targetBehaviour == null)
            runner.target = this;

        ReportAction("DynamicSwap sequence started.");
        runner.StartSequence();
    }

    public void OpenCsvFolder()
    {
        string directory = string.IsNullOrEmpty(LastCsvPath)
            ? UIData.PerfProbe.GetOutputDirectory()
            : Path.GetDirectoryName(LastCsvPath);

        if (string.IsNullOrEmpty(directory))
            directory = UIData.PerfProbe.GetOutputDirectory();

        Application.OpenURL(new System.Uri(directory).AbsoluteUri);
    }

    public void ReportBatchLayout()
    {
        ReportAction(BuildBatchLayoutSummary());
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
        int count = Mathf.Max(runtimeInstancesPerPrefab, 1);
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

        string next = $"owners:{runtimeHolders.Count} batches:{BatchCount} textures:{UniqueImageTextureCount}/{BatchTextureLimit} over:{(IsOverCapacityActive ? "yes" : "no")} probe:{(Probe != null ? Probe.Count : 0)} mode:{displayMode}";
        if (!string.IsNullOrWhiteSpace(actionMessage))
            next += $"\n{actionMessage}";
        next += $"\n{LastCsvPath}";
        if (statusText.text != next)
            statusText.text = next;
    }

    private void ReportAction(string message, bool logWarning = false)
    {
        actionMessage = message;
        UpdateStatusText();

        if (logWarning)
            Debug.LogWarning(message);
    }

    private string BuildProbeTag()
    {
        var parts = new List<string>(6);
        AddTagPart(parts, csvTag);

        string slotTag = enable8TexSlots ? "8slot" : "3slot";
        if (!ContainsTag(csvTag, slotTag))
            AddTagPart(parts, slotTag);

        AddTagPart(parts, useSlim ? "slim" : "managed");
        AddTagPart(parts, rebuildEveryFrame ? "rebuild-every-frame" : "rebuild-on-demand");
        AddTagPart(parts, $"owners{runtimeHolders.Count}");
        AddTagPart(parts, $"batches{BatchCount}");
        AddTagPart(parts, displayMode.ToString());

        return string.Join("_", parts);
    }

    private Dictionary<string, string> BuildProbeContext()
    {
        return new Dictionary<string, string>
        {
            { "useSlim", useSlim.ToString() },
            { "enable8TexSlots", enable8TexSlots.ToString() },
            { "rebuildEveryFrame", rebuildEveryFrame.ToString() },
            { "owners", runtimeHolders.Count.ToString() },
            { "batches", BatchCount.ToString() },
            { "displayMode", displayMode.ToString() }
        };
    }

    private static void AddTagPart(List<string> parts, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parts.Add(UIData.PerfProbe.SanitizeTag(value));
    }

    private static bool ContainsTag(string source, string tag)
    {
        return !string.IsNullOrEmpty(source) &&
               source.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string BuildBatchLayoutSummary()
    {
        return $"Batch layout: owners={runtimeHolders.Count}, textures={UniqueImageTextureCount}/{BatchTextureLimit}, overCapacity={(IsOverCapacityActive ? "yes" : "no")}, batches={BatchCount}, mode={displayMode}";
    }

    private int CountUniqueImageTextures()
    {
        if (activeOwners.Count == 0)
            return 0;

        var textureIds = new HashSet<int>();
        foreach (var owner in activeOwners)
        {
            if (owner == null)
                continue;

            var images = owner.GetComponentsInChildren<UIImage>(true);
            for (int i = 0; i < images.Length; i++)
            {
                var sprite = images[i] != null ? images[i].sprite : null;
                var texture = sprite != null ? sprite.texture : null;
                if (texture != null)
                    textureIds.Add(texture.GetInstanceID());
            }
        }

        return textureIds.Count;
    }

    private bool TryResolveCycleHolderIndex(int holderCount, out int resolvedIndex, out string reason)
    {
        if (instantiateOwnersAtRuntime && cycleOwnerSelectionMode == CycleOwnerSelectionMode.PrefabAndInstance)
        {
            if (runtimeInstancesPerPrefab <= 0)
            {
                resolvedIndex = -1;
                reason = "CycleSprite skipped: runtimeInstancesPerPrefab must be > 0.";
                return false;
            }

            if (cycleOwnerPrefabIndex < 0 || cycleOwnerPrefabIndex >= ownerPrefabs.Count)
            {
                resolvedIndex = -1;
                reason = $"CycleSprite skipped: cycleOwnerPrefabIndex {cycleOwnerPrefabIndex} is out of range for ownerPrefabs.";
                return false;
            }

            if (cycleOwnerInstanceIndex < 0 || cycleOwnerInstanceIndex >= runtimeInstancesPerPrefab)
            {
                resolvedIndex = -1;
                reason = $"CycleSprite skipped: cycleOwnerInstanceIndex {cycleOwnerInstanceIndex} must be between 0 and {runtimeInstancesPerPrefab - 1}.";
                return false;
            }

            resolvedIndex = cycleOwnerPrefabIndex * runtimeInstancesPerPrefab + cycleOwnerInstanceIndex;
            if (resolvedIndex < 0 || resolvedIndex >= holderCount)
            {
                reason = $"CycleSprite skipped: resolved holder index {resolvedIndex} is out of range.";
                return false;
            }

            reason = null;
            return true;
        }

        resolvedIndex = cycleOwnerIndex;
        if (resolvedIndex < 0 || resolvedIndex >= holderCount)
        {
            reason = $"CycleSprite skipped: cycleOwnerIndex {cycleOwnerIndex} is out of range.";
            return false;
        }

        reason = null;
        return true;
    }

    private bool TryResolveCycleDrawIndex(UIPrefabOwner owner, int drawCount, out int resolvedIndex, out string reason)
    {
        if (cycleDrawSelectionMode == CycleDrawSelectionMode.TargetName)
        {
            if (owner == null)
            {
                resolvedIndex = -1;
                reason = "CycleSprite skipped: owner is null, cannot resolve cycle draw target.";
                return false;
            }

            string query = cycleDrawTargetName != null ? cycleDrawTargetName.Trim() : string.Empty;
            if (string.IsNullOrEmpty(query))
            {
                resolvedIndex = -1;
                reason = "CycleSprite skipped: cycleDrawTargetName is empty.";
                return false;
            }

            if (owner.targets == null || owner.targets.Count == 0)
            {
                resolvedIndex = -1;
                reason = $"CycleSprite skipped: owner '{owner.name}' has no registered targets.";
                return false;
            }

            int matchIndex = -1;
            for (int i = 0; i < owner.targets.Count; i++)
            {
                var target = owner.targets[i];
                if (!IsCycleDrawTargetMatch(owner.transform, target, query))
                    continue;

                if (matchIndex >= 0)
                {
                    resolvedIndex = -1;
                    reason = $"CycleSprite skipped: cycleDrawTargetName '{query}' is ambiguous on owner '{owner.name}'.";
                    return false;
                }

                matchIndex = i;
            }

            if (matchIndex < 0)
            {
                resolvedIndex = -1;
                reason = $"CycleSprite skipped: cycleDrawTargetName '{query}' was not found on owner '{owner.name}'. Available targets: {DescribeOwnerTargets(owner)}.";
                return false;
            }

            if (matchIndex >= drawCount)
            {
                resolvedIndex = -1;
                reason = $"CycleSprite skipped: resolved draw index {matchIndex} is out of range.";
                return false;
            }

            resolvedIndex = matchIndex;
            reason = null;
            return true;
        }

        resolvedIndex = cycleDrawIndex;
        if (resolvedIndex < 0 || resolvedIndex >= drawCount)
        {
            reason = $"CycleSprite skipped: cycleDrawIndex {cycleDrawIndex} is out of range.";
            return false;
        }

        reason = null;
        return true;
    }

    private static bool IsCycleDrawTargetMatch(Transform ownerRoot, Transform target, string query)
    {
        if (target == null || string.IsNullOrEmpty(query))
            return false;

        if (string.Equals(target.name, query, StringComparison.OrdinalIgnoreCase))
            return true;

        string fullPath = BuildTargetPath(ownerRoot, target, includeOwnerRoot: true);
        if (string.Equals(fullPath, query, StringComparison.OrdinalIgnoreCase))
            return true;

        string relativePath = BuildTargetPath(ownerRoot, target, includeOwnerRoot: false);
        return string.Equals(relativePath, query, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTargetPath(Transform ownerRoot, Transform target, bool includeOwnerRoot)
    {
        if (target == null)
            return string.Empty;

        var parts = new List<string>();
        Transform current = target;
        while (current != null)
        {
            if (current == ownerRoot)
            {
                if (includeOwnerRoot)
                    parts.Add(current.name);
                break;
            }

            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string DescribeOwnerTargets(UIPrefabOwner owner)
    {
        if (owner == null || owner.targets == null || owner.targets.Count == 0)
            return "<none>";

        var entries = new List<string>(owner.targets.Count);
        for (int i = 0; i < owner.targets.Count; i++)
        {
            entries.Add($"{i}:{TryGetTargetName(owner, i)}");
        }
        return string.Join(", ", entries);
    }

    private static string TryGetTargetName(UIPrefabOwner owner, int index)
    {
        if (owner == null || owner.targets == null || index < 0 || index >= owner.targets.Count)
            return "<unknown>";

        var target = owner.targets[index];
        if (target == null)
            return "<null>";

        string relativePath = BuildTargetPath(owner.transform, target, includeOwnerRoot: false);
        return string.IsNullOrEmpty(relativePath) ? target.name : relativePath;
    }
}
