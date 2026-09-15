using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum DrawMode
{
    NO_RENDER,
    MESH,
    COMBINE,
}

public class UITextRender : MonoBehaviour
{
    public Font font;
    public Text txt_1;
    public Text txt_2;
    public Image img_t;
    public Material material;
    public Camera ui_Camera;
    public DrawMode NeedRender = DrawMode.NO_RENDER;

    public Texture texture_1;
    public Texture texture_2;

    [Range(0.25f,5)]
    public float scale = 1.0f;
    private Mesh mesh_1;
    private Mesh mesh_2;
    private List<Vector3> mesh_2_vertex = new List<Vector3>();
    private Mesh compbine_mesh;
    private Material comb_Material;

    [Button(nameof(ReCreate))]
    public string _x;
    private void Start()
    {
        var str = txt_1.text + txt_2.text;
        font.RequestCharactersInTexture(str, txt_2.fontSize);
        this.mesh_1 = ResourceUtility.CreateTextMesh(font, txt_1.text, Color.white, txt_1.fontSize, 0);
        this.mesh_2 = ResourceUtility.CreateTextMesh(font, txt_2.text, Color.white, txt_2.fontSize, 1);
        mesh_2_vertex.Clear();
        this.mesh_2.GetVertices(mesh_2_vertex);
    }

    private void FillX(List<Vector3> vertList, List<Vector4> uvs, List<Color> colors, List<int> triangles)
    {
        var rect = RectTransformUtility.PixelAdjustRect(img_t.rectTransform, img_t.GetComponentInParent<Canvas>());
        UnityEngine.Debug.LogError($"$rect Of Image_T: {rect}");
        var v = new Vector4(rect.x, rect.y, rect.x + rect.width, rect.y + rect.height);

        var offset = vertList.Count;
        vertList.Add(new Vector3(v.x, v.y) + img_t.transform.localPosition);
        vertList.Add(new Vector3(v.x, v.w) + img_t.transform.localPosition);
        vertList.Add(new Vector3(v.z, v.w) + img_t.transform.localPosition);
        vertList.Add(new Vector3(v.z, v.y) + img_t.transform.localPosition);

        colors.Add(Color.white);
        colors.Add(Color.white);
        colors.Add(Color.white);
        colors.Add(Color.white);


        uvs.Add(new Vector4(0f, 0f,1,0));
        uvs.Add(new Vector4(0f, 1f,1, 0));
        uvs.Add(new Vector4(1f, 1f, 1, 0));
        uvs.Add(new Vector4(1f, 0f, 1, 0));

        triangles.Add(offset + 0);
        triangles.Add(offset + 1);
        triangles.Add(offset + 2);

        triangles.Add(offset + 2);
        triangles.Add(offset + 3);
        triangles.Add(offset + 0);

        //vh.AddTriangle(0, 1, 2);
        //vh.AddTriangle(2, 3, 0);
    }

    public void ReCreate()
    {
        var str = txt_1.text + txt_2.text;
        font.RequestCharactersInTexture(str, txt_2.fontSize);
        this.mesh_1 = ResourceUtility.CreateTextMesh(font, txt_1.text, Color.white,txt_1.fontSize, 0);
        this.mesh_2 = ResourceUtility.CreateTextMesh(font, txt_2.text, Color.red, txt_2.fontSize,0);
        mesh_2_vertex.Clear();
        this.mesh_2.GetVertices(mesh_2_vertex);

        compbine_mesh = new Mesh();
        var vertBuff = new List<Vector3>();
        var uvs = new List<Vector4>();
        var colors = new List<Color>();
        var triangles = new List<int>();

        FillX(vertBuff, uvs, colors,triangles);
        
        var offset = vertBuff.Count;

        foreach (var v in this.mesh_1.vertices)
        {
            vertBuff.Add(v + txt_1.transform.localPosition);
        }

        foreach (var v in this.mesh_2.vertices)
        {
            vertBuff.Add(v + txt_2.transform.localPosition);
        }

        colors.AddRange(this.mesh_1.colors);
        colors.AddRange(this.mesh_2.colors);



        foreach (var item in this.mesh_1.triangles)
        {
            triangles.Add(item + offset);
        }

        offset = offset + this.mesh_1.vertexCount;
        foreach(var item in this.mesh_2.triangles)
        {
            triangles.Add(item + offset);
        }

      
        var tmpV = new List<Vector4>();
        mesh_1.GetUVs(0, tmpV);
        uvs.AddRange(tmpV);
        tmpV.Clear();
        mesh_2.GetUVs(0, tmpV);
        uvs.AddRange(tmpV);

        var rect = RectTransformUtility.PixelAdjustRect(img_t.rectTransform, img_t.GetComponentInParent<Canvas>());
        UnityEngine.Debug.LogError($"$rect Of Image_T: {rect}");



        compbine_mesh.SetVertices(vertBuff);
        compbine_mesh.SetUVs(0, uvs);
        compbine_mesh.SetColors(colors);
        compbine_mesh.SetTriangles(triangles,0);
        compbine_mesh.RecalculateBounds();
        compbine_mesh.RecalculateNormals();


        comb_Material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        comb_Material.SetTexture("_MainTex0", font.material.mainTexture);
        comb_Material.SetTexture("_MainTex1", texture_2);
        comb_Material.renderQueue = 3000;
    }

    private void LateUpdate()
    {
        if (NeedRender == DrawMode.MESH)
        {
            var matix_1 = txt_1.transform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(scale,scale,scale)) ;// Matrix4x4.identity;
            material.mainTexture =  font.material.mainTexture;
            //var material = font.material;
            Graphics.DrawMesh(mesh_1, matix_1, material, 5, ui_Camera);

            var matix = txt_2.transform.localToWorldMatrix;
            var mtx_txt = Matrix4x4.Scale(new Vector3(scale, scale, scale));// Matrix4x4.identity;
            var nat_vertex = new NativeArray<Vector3>(mesh_2.vertexCount,Allocator.Persistent);
            for (int i = 0; i < mesh_2_vertex.Count; i++)
            {
                nat_vertex[i] = mtx_txt.MultiplyPoint(mesh_2_vertex[i]);
            }
            material.mainTexture = font.material.mainTexture;
            mesh_2.SetVertices(nat_vertex);
            //var material = font.material;
            Graphics.DrawMesh(mesh_2, matix, material, 5, ui_Camera);
            nat_vertex.Dispose();
        }else if(NeedRender == DrawMode.COMBINE)
        {
            if (compbine_mesh != null)
            {
                var matix = txt_1.transform.parent.localToWorldMatrix;
                Graphics.DrawMesh(compbine_mesh, matix, comb_Material, 5, ui_Camera);
            }
        }
    }
}
