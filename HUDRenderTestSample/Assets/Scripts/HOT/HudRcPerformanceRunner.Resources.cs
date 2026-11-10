using System;
using System.IO;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;

public sealed partial class HudRcPerformanceRunner
{
    [Serializable]
    public sealed class ResourceIdentity
    {
        public string path, sha256, metaSha256, kind;
        public int width, height;
    }

    [Serializable]
    public sealed class RunRequest
    {
        public string requestId;
        public Configuration configuration;
    }

    public Sprite[] repositorySprites;
    public TMP_FontAsset repositoryFont;
    public Shader mixedShader;
    public ResourceIdentity[] resourceInventory;
    private UITMPText workloadText;
    private readonly Texture[] imageTextures = new Texture[3];
    private string[] textVariants;
    private string runRequestId = "embedded", runRequestSha256 = "none";

    private void LoadRunRequest()
    {
        string path = Path.Combine(Application.persistentDataPath, "hud_rc_request.json");
        if (!File.Exists(path)) return;
        byte[] bytes = File.ReadAllBytes(path);
        var request = JsonUtility.FromJson<RunRequest>(System.Text.Encoding.UTF8.GetString(bytes));
        if (request == null || request.configuration == null || string.IsNullOrEmpty(request.requestId))
            throw new InvalidOperationException("Invalid HUD RC run request.");
        request.configuration.Validate();
        configuration = request.configuration.Copy();
        runRequestId = request.requestId;
        using (var hash = SHA256.Create())
            runRequestSha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
    }

    private void ValidateResources(Configuration config)
    {
        if (!config.repositoryResources) return;
        if (repositorySprites == null || repositorySprites.Length != 3 || repositoryFont == null || mixedShader == null)
            throw new InvalidOperationException("Repository Sprite/font/shader references are required.");
        for (int i = 0; i < 3; i++)
        {
            if (repositorySprites[i] == null || repositorySprites[i].texture == null)
                throw new InvalidOperationException("Missing repository Sprite texture.");
            imageTextures[i] = repositorySprites[i].texture;
            for (int j = 0; j < i; j++)
                if (imageTextures[i] == imageTextures[j])
                    throw new InvalidOperationException("Repository fixture requires three distinct image textures.");
        }
        // Restrict to the adapter's supported primary atlas/material contract.
        const string required = "HP0123456789";
        foreach (char character in required)
            if (!repositoryFont.characterLookupTable.TryGetValue(character, out var glyph) || glyph.glyph.atlasIndex != 0)
                throw new InvalidOperationException("Repository font must contain all fixture glyphs in its primary atlas.");
    }

    private void PrepareWorkloadText(UIPrefabOwner owner)
    {
        if (!active.repositoryResources) return;
        textVariants = new string[11];
        for (int i = 0; i < textVariants.Length; i++) textVariants[i] = "HP " + (1000 + i).ToString("D4");
        var go = new GameObject("Primary atlas HUD label", typeof(RectTransform), typeof(CanvasRenderer), typeof(UITMPText));
        go.transform.SetParent(templateRoot.transform, false);
        workloadText = go.GetComponent<UITMPText>();
        workloadText.font = repositoryFont;
        workloadText.fontSize = 7;
        workloadText.enableWordWrapping = false;
        workloadText.richText = false;
        workloadText.raycastTarget = false;
        workloadText.rectTransform.sizeDelta = new Vector2(26, 10);
        workloadText.rectTransform.anchoredPosition = new Vector2(0, -6);
        workloadText.Text = textVariants[0];
        draws[images.Length] = workloadText;
        owner.targets.Add(go.transform);
        // TMP needs Awake/OnEnable once to initialize its font/canvas state. Keep automatic
        // Canvas rendering disabled; all geometry is generated explicitly by the holder.
        templateRoot.SetActive(true);
        templateRoot.GetComponent<Canvas>().enabled = false;
        foreach (var image in images) image.enabled = false;
        workloadText.enabled = false;
    }

    private void SetWorkloadText(int variant)
    {
        if (workloadText != null) workloadText.Text = textVariants[variant];
    }

    private void BuildWorkloadHolder(IUIPrefabHolder holder)
    {
        holder.BuildMesh(draws);
        if (!active.repositoryResources) return;
        for (int i = 0; i < images.Length; i++) holder.UIMeshDatas[i].UpdateTextureIndex(i + 1);
        var textData = holder.UIMeshDatas[images.Length];
        int vertices = textData is UIMeshDataX slim ? slim.mesh.VertexCount : ((UIMeshData)textData).mesh.VertexCount;
        if (vertices != 24)
            throw new InvalidOperationException("Repository TMP fixture must generate six visible glyph quads.");
    }

    private void BindWorkloadTextures(Material material, RenderBatch batch)
    {
        if (active.repositoryResources)
        {
            MaterialBinder.Bind(material, imageTextures);
            MaterialBinder.BindTmpSdf(material, workloadText.TextTexture, workloadText.TextMaterial);
        }
        else material.SetTexture("_MainTex0", Texture2D.whiteTexture);
    }
}
