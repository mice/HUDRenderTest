using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public class TestUIPrefabHolder : MonoBehaviour
{
    public Font font;
    public Camera ui_Camera;
    public Transform ui_root;


    public List<UIPrefaHolder> holders;

    private Mesh combine_mesh;
    private Material comb_Material;

    private readonly UIPrefabManager uiPrefabManager = UIPrefabManager.Instance;

    private bool UseSlim = false;

    private bool created = false;
    [Button("ReCreate")]
    public string _X;

    [Button("ModifyText")]
    public string _Y;

    [Button("NullIcon")]
    public string _Z;
    public void ReCreate()
    {
        if (!UnityEngine.Application.isPlaying)
        {
            UnityEngine.Debug.LogError("Only Run In PlayModel");
            return;
        }
        created = true;
        foreach (var holder in holders)
        {
            holder.UseSlim(UseSlim);
            uiPrefabManager.Register(holder.DataHolder);
            holder.SetText(2, "NiHao" + UnityEngine.Random.Range(1, 10));
            uiPrefabManager.Generate(holder.DataHolder);
        }
       
        combine_mesh = combine_mesh ?? new Mesh();

        RebuildMesh();

        comb_Material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        uiPrefabManager.UpdateTexture(comb_Material);
        comb_Material.SetTexture("_MainTex0", font.material.mainTexture);
        comb_Material.renderQueue = 3000;
    }

    private void ModifyText()
    {
        if (!created)
            return;

        holders[0].SetText(2,"NiHao" + UnityEngine.Random.Range(1,10));
        holders[0].SetWidth(1, 80 + UnityEngine.Random.Range(10,20));
        RebuildMesh();
    }

    private void NullIcon()
    {
        if (!created)
            return;
        holders[2].SetSprite(2, null);
        RebuildMesh();
    }

    List<int> triangles = new List<int>();
    /// <summary>
    /// 能定点修改么?
    /// </summary>
    private void RebuildMesh()
    {
        if (UseSlim)
        {
            foreach (var holder in holders)
            {
                holder.Fill(triangles, holder.transform.localPosition);
            }
            var uiGeometry = UIMeshDataX.geometry;
            int vertexCount = uiGeometry.drawVertex.Length;
            combine_mesh.SetVertices(uiGeometry.drawVertex,0, vertexCount);
            combine_mesh.SetUVs(0, uiGeometry.uvs, 0, vertexCount);
            combine_mesh.SetColors(uiGeometry.colors, 0, vertexCount);
            combine_mesh.SetTriangles(triangles, 0);
            combine_mesh.RecalculateBounds();
        }
        else
        {

            var vertBuff = new List<Vector3>();
            var uvs = new List<Vector4>();
            var colors = new List<Color32>();

            foreach (var holder in holders)
            {
                holder.Fill(vertBuff, uvs, colors, triangles, holder.transform.localPosition);
            }
            combine_mesh.SetVertices(vertBuff);
            combine_mesh.SetUVs(0, uvs);
            combine_mesh.SetColors(colors);
            combine_mesh.SetTriangles(triangles, 0);
            combine_mesh.RecalculateBounds();
        }
    }

    private void LateUpdate()
    {
        if (combine_mesh != null)
        {
            var matix = ui_root.localToWorldMatrix;
            Graphics.DrawMesh(combine_mesh, matix, comb_Material, 5, ui_Camera);
        }
    }
}
