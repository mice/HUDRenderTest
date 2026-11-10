using System.Diagnostics;
using TMPro;
using Unity.Profiling;
using UnityEngine;

public class UITMPText : TextMeshProUGUI, IUITextDrawTarget
{
    private static readonly ProfilerMarker CanvasMarker = new ProfilerMarker("UIData.TMP.Canvas");
    private static readonly ProfilerMarker MeshMarker = new ProfilerMarker("UIData.TMP.ForceMeshUpdate");
    private static readonly ProfilerMarker ExtractionMarker = new ProfilerMarker("UIData.TMP.Extraction");
    private static readonly ProfilerMarker TransformMarker = new ProfilerMarker("UIData.TMP.Transform");
    public TmpGenerationTimings LastGenerationStageTimings { get; private set; }

    public string Text
    {
        get => text;
        set => text = value;
    }

    public Texture TextTexture
    {
        get
        {
            if (font != null && font.atlasTexture != null)
                return font.atlasTexture;
            return fontMaterial != null ? fontMaterial.mainTexture : null;
        }
    }

    public Material TextMaterial => fontSharedMaterial != null ? fontSharedMaterial : fontMaterial;

    public void DoGenerate(IUIData meshData, Transform root = null)
    {
        LastGenerationStageTimings = default;
        if (meshData == null)
            return;

        var timing = new TmpGenerationTimings();
        bool profile = BuildMeshDiagnostics.Enabled;
        long start = profile ? Stopwatch.GetTimestamp() : 0;
        using (CanvasMarker.Auto()) Canvas.ForceUpdateCanvases();
        if (profile) { timing.CanvasMs = BuildMeshDiagnostics.Milliseconds(Stopwatch.GetTimestamp() - start); start = Stopwatch.GetTimestamp(); }
        using (MeshMarker.Auto()) ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        if (profile) { timing.MeshMs = BuildMeshDiagnostics.Milliseconds(Stopwatch.GetTimestamp() - start); start = Stopwatch.GetTimestamp(); }
        using (ExtractionMarker.Auto()) TMPMeshDataUtility.Fill(textInfo, meshData);
        if (profile) { timing.ExtractionMs = BuildMeshDiagnostics.Milliseconds(Stopwatch.GetTimestamp() - start); start = Stopwatch.GetTimestamp(); }
        using (TransformMarker.Auto())
        {
            if (root != null && root != transform && transform.IsChildOf(root))
            {
                var mtx = Utility.CalcMatrix(root, transform);
                meshData.TransformVertex(mtx);
            }
        }
        if (profile)
        {
            timing.TransformMs = BuildMeshDiagnostics.Milliseconds(Stopwatch.GetTimestamp() - start);
            timing.Samples = 1;
            LastGenerationStageTimings = timing;
        }
    }
}
