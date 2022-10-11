using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class TestGraphicsDataWithScaleOrRotation : MonoBehaviour
{

    public enum ModifierType
    {
        None = 0,
        Scale = 1,
        Rotation = 2,
        Scale_AND_Rotation = 1|2,
    }

    public Font font;
    public Camera ui_Camera;


    //九宫格的图片,背景
    public UIImage img_ui_1;
    //正常的图片Simple
    public UIImage img_ui_2;
    //特效
    public UIImage img_ui_3;
    //Text文本.
    public UIText txt_1;


    private Mesh combine_mesh;
    private Material comb_Material;

    public ModifierType Modifier = ModifierType.None;
    [Range(0.25f,5.0f)]
    public float Scale = 1.0f;

    [Range(0f, 360f)]
    public float Rotation_Z = 1.0f;

    private List<Vector3> vertBuff = new List<Vector3>();
    private List<Vector4> uvs = new List<Vector4>();
    private List<Color32> colors = new List<Color32>();
    private List<int> triangles = new List<int>();

    UIMeshData img_meshData_1;
    UIMeshData img_meshData_2;
    UIMeshData img_meshData_3;
    UIMeshData txt_meshData_1;

    [Button("ReCreate")]
    public string _X;

    public void ReCreate()
    {
        img_meshData_1 = new UIMeshData();
        img_meshData_1.Index = 1;
        img_ui_1.DoGenerate(img_meshData_1);

        img_meshData_2 = new UIMeshData();
        img_meshData_2.Index = 1;
        img_ui_2.DoGenerate(img_meshData_2);

        img_meshData_3 = new UIMeshData();
        img_meshData_3.Index = 1;
        img_ui_3.DoGenerate(img_meshData_3,img_ui_1.transform);

        txt_meshData_1 = new UIMeshData();
        txt_meshData_1.Index = 0;
        txt_1.DoGenerate(txt_meshData_1, img_ui_1.transform);

        combine_mesh = combine_mesh ?? new Mesh();
        
        int offset = 3;
        int totalCount = offset;
        var vec = new Vector3(0, 0);

        vertBuff.Clear();
        uvs.Clear();
        colors.Clear();
        triangles.Clear();

        img_meshData_1.Fill(vertBuff, uvs, colors, triangles, vec);
        img_meshData_2.Fill(vertBuff, uvs, colors, triangles, vec);
        img_meshData_3.Fill(vertBuff, uvs, colors, triangles, vec);
        txt_meshData_1.Fill(vertBuff, uvs, colors, triangles, vec);


        combine_mesh.SetVertices(vertBuff);
        combine_mesh.SetUVs(0, uvs);
        combine_mesh.SetColors(colors);
        combine_mesh.SetTriangles(triangles, 0);
        combine_mesh.RecalculateBounds();


        comb_Material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        comb_Material.SetTexture("_MainTex0", font.material.mainTexture);
        if (img_ui_1 != null && img_ui_1.sprite != null)
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
            if ((int)this.Modifier>0)
            {
                vertBuff.Clear();
                uvs.Clear();
                colors.Clear();
                triangles.Clear();
                Vector3 scale = Vector3.one;
                Quaternion rot = Quaternion.identity;
                if ((Modifier & ModifierType.Scale) > 0)
                {
                    scale = new Vector3(Scale, Scale, Scale);
                }
                if((Modifier & ModifierType.Rotation) > 0)
                {
                    rot = Quaternion.Euler(0, 0, Rotation_Z);
                }
                Matrix4x4 mtx = Matrix4x4.TRS(Vector3.zero, rot, scale);

                img_meshData_1.FillWithMatrix(vertBuff, uvs, colors, triangles, mtx);
                img_meshData_2.FillWithMatrix(vertBuff, uvs, colors, triangles, mtx);
                img_meshData_3.FillWithMatrix(vertBuff, uvs, colors, triangles, mtx);
                txt_meshData_1.FillWithMatrix(vertBuff, uvs, colors, triangles, mtx);


                combine_mesh.SetVertices(vertBuff);
                combine_mesh.SetUVs(0, uvs);
                combine_mesh.SetColors(colors);
                combine_mesh.SetTriangles(triangles, 0);
                combine_mesh.RecalculateBounds();

                Graphics.DrawMesh(combine_mesh, matix, comb_Material, 5, ui_Camera);
            }
            else
            {
                Graphics.DrawMesh(combine_mesh, matix, comb_Material, 5, ui_Camera);
            }
        }
    }
}
