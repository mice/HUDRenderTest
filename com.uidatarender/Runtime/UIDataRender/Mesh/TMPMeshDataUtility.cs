using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TMPMeshDataUtility
{
    private static VertexHelper _scratch;
    private static bool _scratchInUse;
    private static bool _releaseRequested;

    /// <summary>Extracts current geometry on the main thread. Warm nonnested calls reuse scratch.</summary>
    public static void Fill(TMP_TextInfo textInfo, IUIData meshData)
    {
        bool shared = !_scratchInUse;
        var helper = shared ? (_scratch ?? (_scratch = CreateHelper())) : CreateHelper();
        if (shared) _scratchInUse = true;
        try
        {
            var white = (Color32)Color.white;

            if (textInfo != null && textInfo.characterInfo != null && textInfo.meshInfo != null)
            {
                for (int i = 0; i < textInfo.characterCount; i++)
                {
                    var character = textInfo.characterInfo[i];
                    if (!character.isVisible)
                        continue;

                    int materialIndex = character.materialReferenceIndex;
                    if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length)
                        continue;

                    var meshInfo = textInfo.meshInfo[materialIndex];
                    int vertexIndex = character.vertexIndex;
                    if (vertexIndex < 0 || vertexIndex + 3 >= meshInfo.vertices.Length)
                        continue;

                    int offset = helper.currentVertCount;
                    for (int v = 0; v < 4; v++)
                    {
                        int sourceIndex = vertexIndex + v;
                        var vertex = new UIVertex
                        {
                            position = meshInfo.vertices[sourceIndex],
                            color = meshInfo.colors32 != null && sourceIndex < meshInfo.colors32.Length
                                ? meshInfo.colors32[sourceIndex]
                                : white,
                            uv0 = meshInfo.uvs0 != null && sourceIndex < meshInfo.uvs0.Length
                                ? meshInfo.uvs0[sourceIndex]
                                : Vector2.zero
                        };
                        helper.AddVert(vertex);
                    }

                    helper.AddTriangle(offset, offset + 1, offset + 2);
                    helper.AddTriangle(offset + 2, offset + 3, offset);
                }
            }

            meshData.TextureIndex = 0;
            meshData.FillVertex(helper, 1);
        }
        finally
        {
            helper.Clear();
            if (shared)
            {
                _scratchInUse = false;
                if (_releaseRequested) ReleaseScratch();
            }
            else helper.Dispose();
        }
    }

    private static VertexHelper CreateHelper()
    {
        var helper = new VertexHelper();
        // Initialize every list even for the first empty extraction. Some custom
        // UGUI CopyIndices implementations require initialized index storage.
        helper.AddVert(default(UIVertex));
        helper.Clear();
        return helper;
    }

    /// <summary>
    /// Releases retained extraction scratch on the main thread. Active extraction
    /// defers release until it exits. UGUI's list pool can retain returned capacity.
    /// </summary>
    public static void ReleaseScratch()
    {
        if (_scratchInUse)
        {
            _releaseRequested = true;
            return;
        }
        _scratch?.Dispose();
        _scratch = null;
        _releaseRequested = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetScratch()
    {
        ReleaseScratch();
        Application.quitting -= ReleaseScratch;
        Application.quitting += ReleaseScratch;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterScratchCleanup()
    {
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseScratch;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseScratch;
        UnityEditor.EditorApplication.quitting -= ReleaseScratch;
        UnityEditor.EditorApplication.quitting += ReleaseScratch;
    }
#endif
}
