using System.Collections.Generic;
using System.IO;
using UnityEngine;

public interface IPerfProbeSource
{
    UIData.PerfProbe Probe { get; }
}

public class TestUIPrefabHolder : MonoBehaviour, IPerfProbeSource
{
    public Font font;
    public Camera ui_Camera;
    public Transform ui_root;

    public List<UIPrefabHolder> holders;

    private const int MaxSlotsPerBatch = 3;
    private HolderBatchRenderer batchRenderer;
    private readonly List<IUIPrefabHolder> holderInterfaces = new List<IUIPrefabHolder>();
    private readonly Dictionary<IUIPrefabHolder, Vector3> positionMap = new Dictionary<IUIPrefabHolder, Vector3>();

    private readonly UIPrefabManager uiPrefabManager = UIPrefabManager.Instance;
    public bool useSlim = false;
    public bool enable8TexSlots;
    public bool autoRecreateOnStart;
    public string csvTag = "test_uiprefab";
    private bool created = false;

    public UIData.PerfProbe Probe => batchRenderer?.Probe;
    public UIData.PerfProbe PerfProbe => Probe;
    public int BatchCount => batchRenderer != null ? batchRenderer.BatchCount : 0;
    public string LastCsvPath { get; private set; }

    [Button("ReCreate")]
    public string _X;
    [Button("ModifyText")]
    public string _Y;
    [Button("NullIcon")]
    public string _Z;
    [Button(nameof(FlushProbe))]
    public string _Probe;
    [Button(nameof(OpenCsvFolder))]
    public string _Folder;

    private void Start()
    {
        if (autoRecreateOnStart)
            ReCreate();
    }

    public void ReCreate()
    {
        if (!UnityEngine.Application.isPlaying)
        {
            UnityEngine.Debug.LogError("Only Run In PlayModel");
            return;
        }

        created = true;
        uiPrefabManager.SetBatchTextureLimit(enable8TexSlots ? 7 : MaxSlotsPerBatch);
        foreach (var holder in holders)
        {
            holder.UseSlim(useSlim);
            uiPrefabManager.Register(holder.DataHolder);
            holder.SetText(2, "NiHao" + UnityEngine.Random.Range(1, 10));
            uiPrefabManager.Generate(holder.DataHolder);
        }

        batchRenderer?.Dispose();
        batchRenderer = new HolderBatchRenderer(enable8TexSlots ? 7 : MaxSlotsPerBatch);
        RebuildHolderIndex();
        RebuildMesh();
    }

    private void ModifyText()
    {
        if (!created) return;
        holders[0].SetText(2, "NiHao" + UnityEngine.Random.Range(1, 10));
        holders[0].SetWidth(1, 80 + UnityEngine.Random.Range(10, 20));
        RebuildMesh();
    }

    private void NullIcon()
    {
        if (!created) return;
        holders[2].SetSprite(2, null);
        RebuildMesh();
    }

    private void RebuildHolderIndex()
    {
        holderInterfaces.Clear();
        positionMap.Clear();
        foreach (var holder in holders)
        {
            holderInterfaces.Add(holder.DataHolder);
            positionMap[holder.DataHolder] = holder.transform.localPosition;
        }
    }

    private void UpdatePositions()
    {
        foreach (var holder in holders)
            positionMap[holder.DataHolder] = holder.transform.localPosition;
    }

    private void RebuildMesh()
    {
        UpdatePositions();
        batchRenderer.Rebuild(holderInterfaces, positionMap, CreateMaterial, uiPrefabManager.UpdateTexture);
    }

    private Material CreateMaterial()
    {
        var material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        material.SetTexture("_MainTex0", font.material.mainTexture);
        material.renderQueue = 3000;
        if (enable8TexSlots)
            uiPrefabManager.Enable8TexSlots(material);
        return material;
    }

    private void FlushProbe()
    {
        if (Probe == null)
            return;

        LastCsvPath = Probe.Flush(csvTag);
    }

    private void OpenCsvFolder()
    {
        string directory = string.IsNullOrEmpty(LastCsvPath)
            ? Application.persistentDataPath
            : Path.GetDirectoryName(LastCsvPath);

        if (string.IsNullOrEmpty(directory))
            directory = Application.persistentDataPath;

        Application.OpenURL(new System.Uri(directory).AbsoluteUri);
    }

    private void LateUpdate()
    {
        if (batchRenderer == null || ui_root == null) return;
        batchRenderer.Draw(ui_root.localToWorldMatrix, 5, ui_Camera);
    }

    private void OnDestroy()
    {
        batchRenderer?.Dispose();
        batchRenderer = null;
    }
}
