using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TreeEditor;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public struct MeshSlim:IEquatable<MeshSlim>,IEqualityComparer<MeshSlim>
{
    public int VertexOffset;
    public int VertexCount;
    public int IndicesOffset;
    public int IndicesCount;
    /// <summary>
    /// 当前的Index,没有任何意义,只是记录当前的MeshSlim是否有效;
    /// </summary>
    public int Index;

    public void Dispose()
    {
        Index = -1;
        VertexOffset = 0;
        VertexCount = 0;
        IndicesOffset = 0;
        IndicesCount = 0;
    }

    public bool Equals(MeshSlim x, MeshSlim y)
    {
        return x.Equals(y);
    }

    public bool Equals(MeshSlim other)
    {
        return other.VertexOffset == VertexOffset && other.VertexCount == VertexCount 
            && other.IndicesOffset == IndicesOffset && other.IndicesCount == IndicesCount;
    }

    public int GetHashCode(MeshSlim obj)
    {
        return (obj.VertexOffset,obj.VertexCount,obj.IndicesOffset,obj.IndicesCount).GetHashCode();
    }
}

///indirect idea
public class UIMeshDataX : IUIData
{
    /// <summary>
    /// uv,xy为UV
    /// z为:index
    /// w为:flags
    /// </summary>
    public MeshSlim mesh = new MeshSlim()
    {
        Index = -1,
    };
    public int TextureIndex { get; set; }
    public static UIGeometry geometry { get; } = new UIGeometry();
    private static int NEXT = -1;
    public UIMeshDataX()
    {
        UnityEngine.Debug.LogError($"New UI UIMeshDataX :{NEXT++}");
    }

    public void TransformVertex(Matrix4x4 mtx)
    {
        var vertexCount = mesh.VertexCount;
        for (int i = mesh.VertexOffset; i < mesh.VertexOffset + vertexCount; i++)
        {
            geometry.vertex[i] = mtx.MultiplyPoint(geometry.vertex[i]);
        }
    }

    public static UIMeshDataX CreateTextData(Text txt_t)
    {
        UIMeshDataX textData = new UIMeshDataX();
        textData.Create(txt_t.text, txt_t.font, txt_t.fontSize);
        return textData;
    }

    public static UIMeshDataX CreateImageData(Image img_t)
    {
        UIMeshDataX textData = new UIMeshDataX();
        var rect = RectTransformUtility.PixelAdjustRect(img_t.rectTransform, img_t.GetComponentInParent<Canvas>());
        var v = new Vector4(rect.x, rect.y, rect.x + rect.width, rect.y + rect.height);
        textData.Create(v, 0);
        return textData;
    }

    private void Create(string text, Font font, int size)
    {
        Clear();
        var tmp_vert_list = new List<Vector3>();
        var tmp_uv_list = new List<Vector4>();
        var tmp_triangles_list = new List<int>();

        ResourceUtility.BuildTextMesh(font, size, 0, text, tmp_vert_list, tmp_uv_list, tmp_triangles_list);
        this.mesh.VertexCount = tmp_vert_list.Count();
        this.mesh.IndicesCount = tmp_triangles_list.Count();
    }

    private void Create(Vector4 v, int flags)
    {
        Clear();
        this.mesh.VertexCount = 4;
        this.mesh.IndicesCount = 6;
    }


    public void UpdateTextureIndex(int textureIndex)
    {
        this.TextureIndex = textureIndex;
       
        for (int i = mesh.VertexOffset; i < mesh.VertexOffset + mesh.VertexCount; i++)
        {
            ref Vector4 uv = ref geometry.uvs[i];
            uv.z = textureIndex;
        }
    }


    public void FillToDrawData(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Vector3 localPosition)
    {
        var offset = vertList_.Count;
        var vertexCount = this.mesh.VertexCount;
    }

    /// <summary>
    /// 可以transform 封装下,传递给gpu.
    /// </summary>
    /// <param name="localPosition"></param>
    public void FillToTriangleData(List<int> triangles_, Vector3 localPosition)
    {
        for (int i = mesh.VertexOffset; i < mesh.VertexOffset + mesh.VertexCount; i++)
        {
            geometry.drawVertex[i] = (geometry.vertex[i] + localPosition);
        }

        for (int i = 0; i < mesh.IndicesCount; i++)
        {
            triangles_.Add(geometry.indices[i] + mesh.VertexOffset);
        }
    }

    public void FillWithMatrix(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Matrix4x4 mtx)
    {
        
    }

    public void Dispose()
    {
        if (mesh.Index != -1)
        {
            geometry.Release(mesh);
            mesh.Dispose();
        }
    }

    public void FillVertex(VertexHelper toFill, int flags)
    {
        this.Clear();
        var vertexCount = toFill.currentVertCount;
        var indicesCount = toFill.currentIndexCount;
        if (mesh.Index == -1)
        {
            mesh = geometry.Alloc(vertexCount, indicesCount);
        }
        else
        {
            if (mesh.VertexCount != vertexCount || mesh.IndicesCount != indicesCount)
            {
                var oldVertexCount = mesh.VertexCount;
                var oldIndicesCount = mesh.IndicesCount;
                geometry.ReAlloc(vertexCount, indicesCount, ref mesh);
                UnityEngine.Debug.LogError($"Relocated:: + {mesh.Index}=>{oldVertexCount}=>{vertexCount},indices:{oldIndicesCount}=>{indicesCount}");
            }
        }

        UnityEngine.Debug.LogError($"FillVertex Mesh {mesh.Index}::{mesh.VertexOffset},{mesh.VertexCount}::{mesh.IndicesOffset}:{mesh.IndicesCount}");
        toFill.FillData3(ref geometry.vertex, ref geometry.colors, ref geometry.uvs, ref geometry.indices, mesh.VertexOffset, mesh.IndicesOffset, TextureIndex, flags);
      
    }

    private void Clear()
    {

    }
}
