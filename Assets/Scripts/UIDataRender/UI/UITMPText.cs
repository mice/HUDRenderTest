using TMPro;
using UnityEngine;

public class UITMPText : TextMeshProUGUI, IUITextDrawTarget
{
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

    public Material TextMaterial => fontMaterial;

    public void DoGenerate(IUIData meshData, Transform root = null)
    {
        if (meshData == null)
            return;

        Canvas.ForceUpdateCanvases();
        ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        TMPMeshDataUtility.Fill(textInfo, meshData);
        if (root != null && root != transform && transform.IsChildOf(root))
        {
            var mtx = Utility.CalcMatrix(root, transform);
            meshData.TransformVertex(mtx);
        }
    }
}
