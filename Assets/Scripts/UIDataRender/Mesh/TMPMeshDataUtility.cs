using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class TMPMeshDataUtility
{
    public static void Fill(TMP_TextInfo textInfo, IUIData meshData)
    {
        using (var helper = new VertexHelper())
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
    }
}
