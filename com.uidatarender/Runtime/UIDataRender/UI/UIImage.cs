using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Sprites;
using Unity.Profiling;

/// <summary>
/// 把Image
/// </summary>
public class UIImage :Image,IUIDrawTarget
{
    private static readonly ProfilerMarker GenerateMarker = new ProfilerMarker("UIData.UIImage.DoGenerate");
    private static readonly ProfilerMarker TextureLookupMarker = new ProfilerMarker("UIData.UIImage.TextureLookup");
    private static readonly ProfilerMarker UpdateGeometryMarker = new ProfilerMarker("UIData.UIImage.UpdateGeometry");
    private static readonly ProfilerMarker GenerateBasePopulateMarker = new ProfilerMarker("UIData.UIImage.Generate.BasePopulateMesh");
    private static readonly ProfilerMarker GenerateFillVertexMarker = new ProfilerMarker("UIData.UIImage.Generate.FillVertex");
    private static readonly ProfilerMarker GenerateTransformMarker = new ProfilerMarker("UIData.UIImage.Generate.TransformVertex");
    private static readonly ProfilerMarker TransformCheckMarker = new ProfilerMarker("UIData.UIImage.Transform.Check");
    private static readonly ProfilerMarker BasePopulateMarker = new ProfilerMarker("UIData.UIImage.BasePopulateMesh");
    private static readonly ProfilerMarker CustomPopulateMarker = new ProfilerMarker("UIData.UIImage.CustomPopulateMesh");
    private static readonly ProfilerMarker SimpleFastPathMarker = new ProfilerMarker("UIData.UIImage.SimpleFastPath");
    private static readonly ProfilerMarker UguiFallbackMarker = new ProfilerMarker("UIData.UIImage.UGUIFallback");
    public static bool EnableSimpleMeshFastPath { get; set; } = true;
    public bool LastGenerationUsedSimpleMeshFastPath { get; private set; }
    private BuildMeshStageTimings lastGenerationStageTimings;
    public BuildMeshStageTimings LastGenerationStageTimings => lastGenerationStageTimings;
    public SimpleQuadWriteTimings LastSimpleQuadWriteTimings { get; private set; }
    protected virtual bool AllowSimpleMeshFastPath => true;
    private bool _isGenerating = false;
    private IUIData tmp_meshData;
    private Transform tmp_root;
    private readonly VertexHelper extractionHelper = new VertexHelper();
    private Sprite cachedSprite;
    private Sprite cachedGeometrySprite;
    private Texture cachedTexture;
    private Vector4 cachedOuterUv;
    private Vector4 cachedPadding;
    private Vector2 cachedSpriteSize;
    private int cachedTextureVersion = -1;
    private int cachedTextureIndex;
    [ContextMenu("ReCreate")]
    private void _UpdateGeom()
    {
        _isGenerating = true;
        UpdateGeometry();
        _isGenerating = false;
    }

    public void DoGenerate(IUIData meshData,Transform root = null)
    {
        if (BuildMeshDiagnostics.Enabled)
        {
            lastGenerationStageTimings.Clear();
            LastSimpleQuadWriteTimings = new SimpleQuadWriteTimings();
        }
        using (GenerateMarker.Auto())
        {
            _isGenerating = true;
            LastGenerationUsedSimpleMeshFastPath = false;
            var manager = UIPrefabManager.Instance;
            var sourceSprite = sprite;
            var sourceTexture = sourceSprite != null ? sourceSprite.texture : null;
            using (TextureLookupMarker.Auto())
                meshData.TextureIndex = GetTextureIndex(manager, sourceSprite, sourceTexture);

            if (EnableSimpleMeshFastPath && IsSimpleMeshFastPathEligible())
            {
                LastGenerationUsedSimpleMeshFastPath = true;
                long simpleStart = BuildMeshDiagnostics.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                using (SimpleFastPathMarker.Auto())
                {
                    tmp_root = root;
                    long dimensionsStart = BuildMeshDiagnostics.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                    var drawing = GetSimpleDrawingDimensions();
                    if (BuildMeshDiagnostics.Enabled)
                        lastGenerationStageTimings.SimpleDrawingDimensionsMs = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - dimensionsStart);
                    long writeStart = BuildMeshDiagnostics.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                    meshData.WriteSimpleQuad(drawing, GetSimpleOuterUv(), color);
                    if (BuildMeshDiagnostics.Enabled && meshData is ISimpleQuadWriteStageSource writeSource)
                        LastSimpleQuadWriteTimings = writeSource.LastSimpleQuadWriteTimings;
                    if (BuildMeshDiagnostics.Enabled)
                        lastGenerationStageTimings.SimpleMeshWriteMs = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - writeStart);
                    TransformExtractedMesh(meshData);
                    tmp_root = null;
                }
                if (BuildMeshDiagnostics.Enabled)
                    lastGenerationStageTimings.SimplePathMs = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - simpleStart);
                _isGenerating = false;
                return;
            }
       
            tmp_meshData = meshData;
            tmp_root = root;
            extractionHelper.Clear();
            long fallbackStart = BuildMeshDiagnostics.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            using (UguiFallbackMarker.Auto())
            using (UpdateGeometryMarker.Auto())
            using (GenerateBasePopulateMarker.Auto())
                base.OnPopulateMesh(extractionHelper);
            using (GenerateFillVertexMarker.Auto())
                meshData.FillVertex(extractionHelper, 0);
            using (GenerateTransformMarker.Auto())
                TransformExtractedMesh(meshData);
            if (BuildMeshDiagnostics.Enabled)
                lastGenerationStageTimings.UguiFallbackMs = BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - fallbackStart);
            tmp_meshData = null;
            tmp_root = null;
            _isGenerating = false;
        }
    }

    internal bool IsSimpleMeshFastPathEligible()
    {
        return AllowSimpleMeshFastPath
            && type == Type.Simple
            && !useSpriteMesh
            && (sprite == null || (!preserveAspect && sprite.rect.width > 0f && sprite.rect.height > 0f));
    }

    private Vector4 GetSimpleDrawingDimensions()
    {
        if (sprite == null)
        {
            var pixelRect = GetPixelAdjustedRect();
            return new Vector4(pixelRect.x, pixelRect.y, pixelRect.x + pixelRect.width, pixelRect.y + pixelRect.height);
        }
        UpdateSpriteCache();
        var rect = GetPixelAdjustedRect();
        var spriteWidth = Mathf.RoundToInt(cachedSpriteSize.x);
        var spriteHeight = Mathf.RoundToInt(cachedSpriteSize.y);
        return new Vector4(
            rect.x + rect.width * cachedPadding.x / spriteWidth,
            rect.y + rect.height * cachedPadding.y / spriteHeight,
            rect.x + rect.width * (spriteWidth - cachedPadding.z) / spriteWidth,
            rect.y + rect.height * (spriteHeight - cachedPadding.w) / spriteHeight);
    }

    private Vector4 GetSimpleOuterUv()
    {
        return sprite == null ? new Vector4(0f, 0f, 1f, 1f) : cachedOuterUv;
    }

    private int GetTextureIndex(UIPrefabManager manager, Sprite sourceSprite, Texture sourceTexture)
    {
        if (sourceTexture == null)
        {
            cachedSprite = sourceSprite;
            cachedTexture = null;
            cachedTextureVersion = manager.TextureVersion;
            cachedTextureIndex = -1;
            return -1;
        }

        if (cachedTexture != sourceTexture || cachedTextureVersion != manager.TextureVersion)
        {
            cachedTexture = sourceTexture;
            cachedSprite = sourceSprite;
            cachedTextureVersion = manager.TextureVersion;
            cachedTextureIndex = manager.GetTextureIndex(sourceTexture);
        }
        return cachedTextureIndex;
    }

    private void UpdateSpriteCache()
    {
        long cacheStart = BuildMeshDiagnostics.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        var source = sprite;
        if (cachedGeometrySprite == source)
        {
            if (BuildMeshDiagnostics.Enabled)
                lastGenerationStageTimings.SimpleSpriteDataMs += BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - cacheStart);
            return;
        }
        cachedGeometrySprite = source;
        if (source == null)
        {
            cachedOuterUv = Vector4.zero;
            cachedPadding = Vector4.zero;
            cachedSpriteSize = Vector2.zero;
            if (BuildMeshDiagnostics.Enabled)
                lastGenerationStageTimings.SimpleSpriteDataMs += BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - cacheStart);
            return;
        }
        cachedOuterUv = DataUtility.GetOuterUV(source);
        cachedPadding = DataUtility.GetPadding(source);
        cachedSpriteSize = source.rect.size;
        if (BuildMeshDiagnostics.Enabled)
            lastGenerationStageTimings.SimpleSpriteDataMs += BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - cacheStart);
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        using (BasePopulateMarker.Auto())
        {
            base.OnPopulateMesh(toFill);
        }

        if (_isGenerating && tmp_meshData != null)
        {
            using (CustomPopulateMarker.Auto())
                tmp_meshData.FillVertex(toFill, 0);

            using (TransformCheckMarker.Auto())
            {
                var _transform = transform;
                bool isChild = false;
                if (tmp_root != null && tmp_root != _transform)
                    isChild = _transform.IsChildOf(tmp_root);
                if (isChild)
                {
                    var mtx = Utility.CalcMatrix(tmp_root, _transform);
                    tmp_meshData.TransformVertex(mtx);
                }
            }
        }
    }

    private void TransformExtractedMesh(IUIData meshData)
    {
        long transformStart = BuildMeshDiagnostics.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        using (TransformCheckMarker.Auto())
        {
            var current = transform;
            bool isChild = false;
            if (tmp_root != null && tmp_root != current)
                isChild = current.IsChildOf(tmp_root);
            if (!isChild)
                return;

            var matrix = Utility.CalcMatrix(tmp_root, current);
            meshData.TransformVertex(matrix);
        }
        if (BuildMeshDiagnostics.Enabled)
            lastGenerationStageTimings.TransformMs += BuildMeshDiagnostics.Milliseconds(System.Diagnostics.Stopwatch.GetTimestamp() - transformStart);
    }
}
