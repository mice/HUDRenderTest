using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BatchUIDataRender : MonoBehaviour
{
    public Font font;
    public Text txt_1;
    public Image img_t;
    public Material material;
    public Camera ui_Camera;

    public Texture texture_1;

    private Mesh compbine_mesh;
    private Material comb_Material;

    private UIMeshData text_uidata;

    private UIMeshData dataUIRender;

    private UIPackData uiPackData;

    [Button("ReCreate")]
    public string _X;
    private void Start()
    {
        var str = txt_1.text;
        font.RequestCharactersInTexture(str, txt_1.fontSize);
        text_uidata = UIMeshData.CreateTextData(txt_1);
        dataUIRender = UIMeshData.CreateImageData(img_t);

        uiPackData = UIPackData.Create(img_t.transform);
    }


    [ContextMenu("ReCreate")]
    public void ReCreate()
    {
        var str = txt_1.text;
        font.RequestCharactersInTexture(str, txt_1.fontSize);
        text_uidata = UIMeshData.CreateTextData(txt_1);
        compbine_mesh = compbine_mesh??new Mesh();
        var vertBuff = new List<Vector3>();
        var uvs = new List<Vector4>();
        var colors = new List<Color32>();
        var triangles = new List<int>();
        int totalCount = 200;
        var vec = new Vector3[totalCount];
        for (int i = 0; i < totalCount; i++)
        {
            vec[i] = new Vector3(UnityEngine.Random.Range(0,1200) - 600,UnityEngine.Random.Range(0,700)-350);
        }
        for (int i = 0; i < totalCount; i++)
        {
            //dataUIRender.Fill(vertBuff, uvs, colors, triangles, vec[i]);
            //text_uidata.Fill(vertBuff, uvs, colors, triangles, vec[i]);
            uiPackData.Fill(vertBuff, uvs, colors, triangles, vec[i]);
        }
        


        compbine_mesh.SetVertices(vertBuff);
        compbine_mesh.SetUVs(0, uvs);
        compbine_mesh.SetColors(colors);
        compbine_mesh.SetTriangles(triangles,0);
        compbine_mesh.RecalculateBounds();
        compbine_mesh.RecalculateNormals();


        comb_Material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        comb_Material.SetTexture("_MainTex0", font.material.mainTexture);
        comb_Material.SetTexture("_MainTex1", texture_1);
        comb_Material.renderQueue = 3000;
    }

    private void LateUpdate()
    {
        if (compbine_mesh != null)
        {
            var matix = txt_1.transform.parent.localToWorldMatrix;
            Graphics.DrawMesh(compbine_mesh, matix, comb_Material, 5, ui_Camera);
        }
    }

    private void OnDestroy()
    {
        if (dataUIRender != null)
        {
            dataUIRender.Dispose();
        }
        dataUIRender = null;
    }
}
