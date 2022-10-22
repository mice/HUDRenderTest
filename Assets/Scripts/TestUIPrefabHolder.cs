﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public class TestUIPrefabHolder : MonoBehaviour
{
    public Font font;
    public Camera ui_Camera;
    public Transform ui_root;


    public List<UIPrefaHolder> holders;

    public Sprite texture_1;

    public Sprite texture_2;

    private Mesh compbine_mesh;
    private Material comb_Material;

    private UIPrefabManager uiPrefabManager = new UIPrefabManager();

    [Button("ReCreate")]
    public string _X;

    [Button("ModifyText")]
    public string _Y;

    [ContextMenu("ReCreate")]
    public void ReCreate()
    {
        if (!UnityEngine.Application.isPlaying)
        {
            UnityEngine.Debug.LogError("Only Run In PlayModel");
            return;
        }
        foreach(var holder in holders)
        {
            uiPrefabManager.Register(holder);
            holder.SetText(2, "NiHao" + UnityEngine.Random.Range(1, 10));
            uiPrefabManager.Generate(holder);
        }
        if (2 < holders.Count)
        {
            holders[2].SetTextureIndex(2,2);
        }
        compbine_mesh = compbine_mesh ?? new Mesh();

        RebuildMesh();

        comb_Material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        comb_Material.SetTexture("_MainTex0", font.material.mainTexture);
        comb_Material.SetTexture("_MainTex1", texture_1.texture);
        comb_Material.SetTexture("_MainTex2", texture_2.texture);
        comb_Material.renderQueue = 3000;
    }

    private void ModifyText()
    {
        holders[0].SetText(2,"NiHao" + UnityEngine.Random.Range(1,10));
        holders[0].SetWidth(1, 80 + UnityEngine.Random.Range(10,20));
        RebuildMesh();
    }

    /// <summary>
    /// 能定点修改么?
    /// </summary>
    private void RebuildMesh()
    {
        var vertBuff = new List<Vector3>();
        var uvs = new List<Vector4>();
        var colors = new List<Color32>();
        var triangles = new List<int>();

        if (UIMeshData.UseSlim)
        {
            foreach (var holder in holders)
            {
                holder.Fill(triangles, holder.transform.localPosition);
            }
            var uiGeometry = UIMeshData.geometry;
            int vertexCount = uiGeometry.drawVertList.Length;
            compbine_mesh.SetVertices(uiGeometry.drawVertList,0, vertexCount);
            compbine_mesh.SetUVs(0, uiGeometry.uvs, 0, vertexCount);
            compbine_mesh.SetColors(uiGeometry.colors, 0, vertexCount);
            compbine_mesh.SetTriangles(triangles, 0);
            compbine_mesh.RecalculateBounds();
        }
        else
        {
            foreach (var holder in holders)
            {
                holder.Fill(vertBuff, uvs, colors, triangles, holder.transform.localPosition);
            }
            compbine_mesh.SetVertices(vertBuff);
            compbine_mesh.SetUVs(0, uvs);
            compbine_mesh.SetColors(colors);
            compbine_mesh.SetTriangles(triangles, 0);
            compbine_mesh.RecalculateBounds();
        }

      
    }

    private void LateUpdate()
    {
        if (compbine_mesh != null)
        {
            var matix = ui_root.localToWorldMatrix;
            Graphics.DrawMesh(compbine_mesh, matix, comb_Material, 5, ui_Camera);
        }
    }
}
