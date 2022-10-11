using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 测试目标:
/// 从Image中获取Mesh数据,然后渲染出来.
/// </summary>
public class TestGraphicsData : MonoBehaviour
{
    public Font font;
    public Camera ui_Camera;

    //正常的图片Simple
    public UIImage img_ui_1;
    //九宫格的图片
    public UIImage img_ui_2;
    //Text文本.
    public UIText txt_1;


    private Mesh combine_mesh;
    private Material comb_Material;


    [Button("ReCreate")]
    public string _X;

    public void ReCreate()
    {
        var img_meshData_1 = new UIMeshData();
        img_meshData_1.Index = 1;
        img_ui_1.DoGenerate(img_meshData_1);

        var img_meshData_2= new UIMeshData();
        img_meshData_2.Index = 1;
        img_ui_2.DoGenerate(img_meshData_2);

        var txt_meshData_1 = new UIMeshData();
        txt_meshData_1.Index = 0;
        txt_1.DoGenerate(txt_meshData_1);

        combine_mesh = combine_mesh ?? new Mesh();
        var vertBuff = new List<Vector3>();
        var uvs = new List<Vector4>();
        var colors = new List<Color32>();
        var triangles = new List<int>();
        int offset = 3;
        int totalCount = offset+100;
        var vec = new Vector3[totalCount];

        vec[0] = new Vector3(-400, 0);
        vec[1] = new Vector3(-200, 0);
        vec[2] = new Vector3(100, 0);
        for (int i = offset; i < totalCount; i++)
        {
            vec[i] = new Vector3(UnityEngine.Random.Range(0, 1200) - 600, UnityEngine.Random.Range(0, 700) - 350);
        }

        img_meshData_1.Fill(vertBuff, uvs, colors, triangles, vec[0]);
        img_meshData_2.Fill(vertBuff, uvs, colors, triangles, vec[1]);
        txt_meshData_1.Fill(vertBuff, uvs, colors, triangles, vec[2]);
       
        for (int i = offset; i < totalCount; i++)
        {
            //dataUIRender.Fill(vertBuff, uvs, colors, triangles, vec[i]);
            //text_uidata.Fill(vertBuff, uvs, colors, triangles, vec[i]);
           
            img_meshData_2.Fill(vertBuff, uvs, colors, triangles, vec[i]);
            img_meshData_1.Fill(vertBuff, uvs, colors, triangles, vec[i]);
            txt_meshData_1.Fill(vertBuff, uvs, colors, triangles, vec[i]);
        }



        combine_mesh.SetVertices(vertBuff);
        combine_mesh.SetUVs(0, uvs);
        combine_mesh.SetColors(colors);
        combine_mesh.SetTriangles(triangles, 0);
        combine_mesh.RecalculateBounds();
        combine_mesh.RecalculateNormals();


        comb_Material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        comb_Material.SetTexture("_MainTex0", font.material.mainTexture);
        if(img_ui_1!=null  && img_ui_1.sprite != null)
        {
            comb_Material.SetTexture("_MainTex1", img_ui_1.sprite.texture);
        }
        comb_Material.renderQueue = 3000;
    }


    private void LateUpdate()
    {
        if (combine_mesh != null)
        {
            var matix = txt_1.transform.parent.localToWorldMatrix;
            Graphics.DrawMesh(combine_mesh, matix, comb_Material, 5, ui_Camera);
        }
    }
}
