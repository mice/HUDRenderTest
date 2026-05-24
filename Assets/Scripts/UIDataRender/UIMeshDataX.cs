using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

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
        LogDebug($"New UI UIMeshDataX :{NEXT++}");
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
        var white = (Color32)Color.white;

        for (int i = mesh.VertexOffset; i < mesh.VertexOffset + mesh.VertexCount; i++)
        {
            vertList_.Add(geometry.vertex[i] + localPosition);
            uvs_.Add(geometry.uvs[i]);
            colors_.Add(geometry.colors[i].Equals(default(Color32)) ? white : geometry.colors[i]);
        }

        for (int i = mesh.IndicesOffset; i < mesh.IndicesOffset + mesh.IndicesCount; i++)
        {
            triangles_.Add(geometry.indices[i] - mesh.VertexOffset + offset);
        }
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

        for (int i = mesh.IndicesOffset; i < mesh.IndicesOffset + mesh.IndicesCount; i++)
        {
            triangles_.Add(geometry.indices[i]);
        }
    }

    public void FillWithMatrix(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Matrix4x4 mtx)
    {
        var offset = vertList_.Count;
        var white = (Color32)Color.white;

        for (int i = mesh.VertexOffset; i < mesh.VertexOffset + mesh.VertexCount; i++)
        {
            vertList_.Add(mtx.MultiplyPoint(geometry.vertex[i]));
            uvs_.Add(geometry.uvs[i]);
            colors_.Add(geometry.colors[i].Equals(default(Color32)) ? white : geometry.colors[i]);
        }

        for (int i = mesh.IndicesOffset; i < mesh.IndicesOffset + mesh.IndicesCount; i++)
        {
            triangles_.Add(geometry.indices[i] - mesh.VertexOffset + offset);
        }
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
                LogDebug($"Relocated:: + {mesh.Index}=>{oldVertexCount}=>{vertexCount},indices:{oldIndicesCount}=>{indicesCount}");
            }
        }

        LogDebug($"FillVertex Mesh {mesh.Index}::{mesh.VertexOffset},{mesh.VertexCount}::{mesh.IndicesOffset}:{mesh.IndicesCount}");
        toFill.FillData3(ref geometry.vertex, ref geometry.colors, ref geometry.uvs, ref geometry.indices, mesh.VertexOffset, mesh.IndicesOffset, TextureIndex, flags);
      
    }

    private void Clear()
    {

    }

    [Conditional("UI_VERBOSE")]
    private static void LogDebug(string message)
    {
        UnityEngine.Debug.Log(message);
    }
}
