using System;
using System.Collections.Generic;
using System.IO;
using UIData;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable scene harness for the pending texture-slot and performance-baseline scenes.
/// It builds holders through the production MergeBatcher + HolderBatchRenderer path.
/// </summary>
public class PrefabBatchSceneRunner : MonoBehaviour, IPerfProbeSource
{
    public Font font;
    public Camera uiCamera;
    public Transform uiRoot;
    public UIPrefabOwner[] ownerTemplates;
    public Text statusText;
    public int count = 10;
    public bool useSlim;
    public bool changePos;
    public bool randomizeOwners;
    public bool continuousRebuild;
    public bool enable8TexSlots;
    public bool autoRecreateOnStart = true;
    public Vector2 positionRangeX = new Vector2(-400f, 400f);
    public Vector2 positionRangeY = new Vector2(-300f, 300f);
    public string csvTag = "prefab_batch";

    [Button(nameof(ReCreate))]
    public string _recreate;
    [Button(nameof(FlushProbe))]
    public string _flushProbe;
    [Button(nameof(OpenCsvFolder))]
    public string _openCsvFolder;

    private readonly List<IUIPrefabHolder> holders = new List<IUIPrefabHolder>();
    private readonly Dictionary<IUIPrefabHolder, Vector3> positions = new Dictionary<IUIPrefabHolder, Vector3>();
    private readonly UIPrefabManager uiPrefabManager = UIPrefabManager.Instance;

    private HolderBatchRenderer batchRenderer;

    public PerfProbe Probe => batchRenderer != null ? batchRenderer.Probe : null;
    public int BatchCount => batchRenderer != null ? batchRenderer.BatchCount : 0;
    public string LastCsvPath { get; private set; }

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

        ClearSceneState();
        CreateHolders();
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

        Application.OpenURL(new Uri(directory).AbsoluteUri);
    }

    private void Update()
    {
        if (holders.Count == 0)
            return;

        if (changePos)
        {
            foreach (var holder in holders)
                positions[holder] = CreateRandomPosition();
        }

        if (changePos || continuousRebuild)
            RebuildMesh();
    }

    private void LateUpdate()
    {
        if (batchRenderer == null || uiRoot == null)
            return;

        batchRenderer.Draw(uiRoot.localToWorldMatrix, 5, uiCamera);
    }

    private void OnDestroy()
    {
        ClearSceneState();
    }

    private void CreateHolders()
    {
        if (ownerTemplates == null || ownerTemplates.Length == 0)
            throw new InvalidOperationException("ownerTemplates must contain at least one UIPrefabOwner.");

        for (int i = 0; i < count; i++)
        {
            var holder = CreateHolder();
            var owner = SelectOwnerTemplate(i);
            var position = CreateRandomPosition();
            holder.SetTarget(owner);

            uiPrefabManager.Register(holder);
            if (owner.targets != null && owner.targets.Count > 2)
                holder.SetText(2, $"HUD-{i:000}");

            uiPrefabManager.Generate(holder);
            holders.Add(holder);
            positions[holder] = position;
        }
    }

    private IUIPrefabHolder CreateHolder()
    {
        if (useSlim)
            return new DataPrefabHolder<UIMeshDataX>();

        return new DataPrefabHolder<UIMeshData>();
    }

    private UIPrefabOwner SelectOwnerTemplate(int index)
    {
        if (randomizeOwners)
            return ownerTemplates[UnityEngine.Random.Range(0, ownerTemplates.Length)];

        return ownerTemplates[index % ownerTemplates.Length];
    }

    private Vector3 CreateRandomPosition()
    {
        return new Vector3(
            UnityEngine.Random.Range(positionRangeX.x, positionRangeX.y),
            UnityEngine.Random.Range(positionRangeY.x, positionRangeY.y),
            0f);
    }

    private void RebuildMesh()
    {
        if (batchRenderer == null)
            batchRenderer = new HolderBatchRenderer(enable8TexSlots ? 7 : 3);

        batchRenderer.Rebuild(holders, positions, CreateMaterial, uiPrefabManager.UpdateTexture);
        UpdateStatusText();
    }

    private Material CreateMaterial()
    {
        var material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        material.SetTexture("_MainTex0", font != null ? font.material.mainTexture : null);
        material.renderQueue = 3000;
        if (enable8TexSlots)
            uiPrefabManager.Enable8TexSlots(material);
        return material;
    }

    private void ClearSceneState()
    {
        batchRenderer?.Dispose();
        batchRenderer = null;

        foreach (var holder in holders)
        {
            uiPrefabManager.RemoveHolder(holder);
            if (holder.UIMeshDatas == null)
                continue;

            for (int i = 0; i < holder.UIMeshDatas.Count; i++)
                holder.UIMeshDatas[i]?.Dispose();
        }

        holders.Clear();
        positions.Clear();
    }

    private void UpdateStatusText()
    {
        if (statusText == null)
            return;

        statusText.text = $"holders:{holders.Count} batches:{BatchCount} probe:{(Probe != null ? Probe.Count : 0)}\n{LastCsvPath}";
    }
}
