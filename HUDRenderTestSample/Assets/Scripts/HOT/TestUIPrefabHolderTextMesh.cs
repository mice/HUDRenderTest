using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class TestUIPrefabHolderTextMesh : MonoBehaviour, IPerfProbeSource
{
    [FormerlySerializedAs("font")]
    public TMP_FontAsset fontAsset;
    public Material fontMaterial;
    public bool useTmpSdfShader;
    public Camera ui_Camera;
    public Transform ui_root;

    public List<UIPrefabHolder> holders;

    private const int MaxSlotsPerBatch = 3;
    private HolderBatchRenderer batchRenderer;
    private readonly List<IUIPrefabHolder> holderInterfaces = new List<IUIPrefabHolder>();
    private readonly Dictionary<IUIPrefabHolder, Vector3> positionMap = new Dictionary<IUIPrefabHolder, Vector3>();
    private UIPrefabRegistration textRegistration;

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

    [Button(nameof(ReCreate))]
    public string _X;
    [Button(nameof(ModifyText))]
    public string _Y;
    [Button(nameof(NullIcon))]
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

    private void OnValidate()
    {
        SyncTextTargetsForPreview();
    }

    public void ReCreate()
    {
        if (!UnityEngine.Application.isPlaying)
        {
            UnityEngine.Debug.LogError("Only Run In PlayModel");
            return;
        }

        created = true;
        textRegistration = null;
        uiPrefabManager.SetBatchTextureLimit(enable8TexSlots ? 7 : MaxSlotsPerBatch);
        foreach (var holder in holders)
        {
            holder.UseSlim(useSlim);
            ConfigureTextTargets(holder.DataHolder.Target);
            uiPrefabManager.Register(holder.DataHolder);
            holder.SetText(2, "NiHao" + UnityEngine.Random.Range(1, 10));
            var registration = uiPrefabManager.Generate(holder.DataHolder);
            if (textRegistration == null && registration?.GetPrimaryTextTarget() != null)
                textRegistration = registration;
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
        batchRenderer.Rebuild(holderInterfaces, positionMap, CreateMaterial, BindTextures);
    }

    private Material CreateMaterial()
    {
        var shaderName = useTmpSdfShader ? "Hidden/UIE-TMP-SDF" : "Hidden/UIE-AtlasBlit";
        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            UnityEngine.Debug.LogError($"Shader not found: {shaderName}");
            shader = Shader.Find("Hidden/UIE-AtlasBlit");
        }

        var material = new Material(shader);
        material.renderQueue = 3000;
        BindTextTexture(material);
        if (enable8TexSlots)
            uiPrefabManager.Enable8TexSlots(material);
        return material;
    }

    private void BindTextures(Material material, RenderBatch batch)
    {
        BindTextTexture(material);
        uiPrefabManager.UpdateTexture(material, batch);
    }

    private void BindTextTexture(Material material)
    {
        if (useTmpSdfShader)
        {
            var sourceMaterial = fontMaterial != null
                ? fontMaterial
                : textRegistration?.GetPrimaryTextTarget()?.TextMaterial ?? ResolveFontMaterial();
            MaterialBinder.BindTmpSdf(material, ResolveFontTexture(), sourceMaterial);
            return;
        }

        uiPrefabManager.UpdateTextTexture(material, textRegistration);
        if (textRegistration?.GetPrimaryTextTarget() == null)
            MaterialBinder.BindText(material, ResolveFontTexture());
    }

    private void ConfigureTextTargets(UIPrefabOwner owner)
    {
        var resolvedFont = ResolveFontAsset();
        if (owner?.targets == null)
            return;

        foreach (var target in owner.targets)
        {
            if (target == null || !target.TryGetComponent<UITMPText>(out var tmpText))
                continue;

            if (resolvedFont != null)
                tmpText.font = resolvedFont;
            if (fontMaterial != null)
                tmpText.fontSharedMaterial = fontMaterial;
        }
    }

    private void SyncTextTargetsForPreview()
    {
        if (holders == null)
            return;

        foreach (var holder in holders)
            ConfigureTextTargets(holder != null ? holder.TargetOwner : null);
    }

    private TMP_FontAsset ResolveFontAsset()
    {
        return fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
    }

    private Texture ResolveFontTexture()
    {
        var resolvedMaterial = ResolveFontMaterial();
        if (resolvedMaterial != null && resolvedMaterial.mainTexture != null)
            return resolvedMaterial.mainTexture;

        return ResolveFontAsset()?.atlasTexture;
    }

    private Material ResolveFontMaterial()
    {
        if (fontMaterial != null)
            return fontMaterial;

        var resolvedFont = ResolveFontAsset();
        return resolvedFont != null ? resolvedFont.material : null;
    }

    private void FlushProbe()
    {
        if (Probe == null)
            return;

        LastCsvPath = Probe.Flush(
            csvTag,
            new Dictionary<string, string>
            {
                { "useSlim", useSlim.ToString() },
                { "enable8TexSlots", enable8TexSlots.ToString() },
                { "holderCount", holders != null ? holders.Count.ToString() : "0" },
                { "batchCount", BatchCount.ToString() }
            });
    }

    private void OpenCsvFolder()
    {
        string directory = string.IsNullOrEmpty(LastCsvPath)
            ? UIData.PerfProbe.GetOutputDirectory()
            : Path.GetDirectoryName(LastCsvPath);

        if (string.IsNullOrEmpty(directory))
            directory = UIData.PerfProbe.GetOutputDirectory();

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
