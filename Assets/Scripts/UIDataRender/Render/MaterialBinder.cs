using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Binds textures from a <see cref="TextureSlotTable"/> to Material shader properties
/// <c>_MainTex1</c>..<c>_MainTexN</c>.
/// Extracted from <see cref="UIPrefabManager.UpdateTexture"/>.
/// </summary>
public static class MaterialBinder
{
    private static readonly int[] ShaderProperties =
    {
        Shader.PropertyToID("_MainTex1"),
        Shader.PropertyToID("_MainTex2"),
        Shader.PropertyToID("_MainTex3"),
        Shader.PropertyToID("_MainTex4"),
        Shader.PropertyToID("_MainTex5"),
        Shader.PropertyToID("_MainTex6"),
        Shader.PropertyToID("_MainTex7"),
    };

    /// <summary>
    /// Writes <c>textures[i]</c> to <c>_MainTex{i+1}</c> for each texture in the list.
    /// </summary>
    public static void Bind(Material material, IReadOnlyList<Texture> textures)
    {
        if (material == null) return;
        var count = Mathf.Min(textures.Count, ShaderProperties.Length);
        for (int i = 0; i < count; i++)
            material.SetTexture(ShaderProperties[i], textures[i]);
    }
}
