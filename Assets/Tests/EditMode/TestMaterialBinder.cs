using NUnit.Framework;
using UnityEngine;

public class TestMaterialBinder
{
    [Test]
    public void Bind_NullMaterial_DoesNotThrow()
    {
        var texture = new Texture2D(1, 1);

        Assert.DoesNotThrow(() => MaterialBinder.Bind(null, new Texture[] { texture }));

        Object.DestroyImmediate(texture);
    }

    [Test]
    public void Bind_WritesSequentialSlots_AndCapsAtSeven()
    {
        var shader = Shader.Find("Hidden/UIE-AtlasBlit");
        Assert.IsNotNull(shader, "Hidden/UIE-AtlasBlit shader must be available for binding tests.");

        var material = new Material(shader);
        var textures = new Texture2D[8];
        for (int i = 0; i < textures.Length; i++)
            textures[i] = new Texture2D(1, 1);

        MaterialBinder.Bind(material, textures);

        for (int i = 0; i < 7; i++)
            Assert.AreSame(textures[i], material.GetTexture($"_MainTex{i + 1}"));

        Object.DestroyImmediate(material);
        for (int i = 0; i < textures.Length; i++)
            Object.DestroyImmediate(textures[i]);
    }
}
