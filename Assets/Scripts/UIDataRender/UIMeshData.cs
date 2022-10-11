using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// 1. 不支持Scale
/// 2. 不支持Rotation
/// 3. 不支持9宫格
/// </summary>

public interface IUIData : System.IDisposable
{
    void Fill(List<Vector3> vertList, List<Vector4> uvs, List<Color32> colors, List<int> triangles, Vector3 localPosition);
}


public class UIMeshData : IUIData
{
    /// <summary>
    /// uv,xy为UV
    /// z为:index
    /// w为:flags
    /// </summary>
    public Vector4[] uvs = new Vector4[4];
    public Vector3[] vertList = new Vector3[4];
    public Color32[] colors = Array.Empty<Color32>();
    public int[] triangles = new int[6];
    public int VertexCount;
    public int IndicsCount;
    public int Index { get; set; }

    public void TransformVertex(Matrix4x4 mtx)
    {
        var vertexCount = VertexCount;
        for (int i = 0; i < vertexCount; i++)
        {
            vertList[i] = mtx.MultiplyPoint(vertList[i]);
        }
    }

    public static UIMeshData CreateTextData(Text txt_t)
    {
        UIMeshData textData = new UIMeshData();
        textData.Create(txt_t.text,txt_t.font,txt_t.fontSize);
        return textData;
    }

    public static UIMeshData CreateImageData(Image img_t)
    {
        UIMeshData textData = new UIMeshData();
        var rect = RectTransformUtility.PixelAdjustRect(img_t.rectTransform, img_t.GetComponentInParent<Canvas>());
        var v = new Vector4(rect.x, rect.y, rect.x + rect.width, rect.y + rect.height);
        textData.Create(v,0);
        return textData;
    }

    private void Create(string text,Font font,int size)
    {
        Clear();
        ResourceUtility.BuildTextMesh(font, size, 0, text, vertList, uvs, triangles);
    }

    private void Create(Vector4 v,int flags)
    {
        Clear();

        for (int i = 0; i < 4; i++)
        {
            vertList[i] = (Vector3.zero);
            uvs[i] = (Vector3.zero);
        }
        for (int i = 0; i < 6; i++)
        {
            triangles[i] = 0;
        }

        vertList[0] = (new Vector3(v.x, v.y));
        vertList[1] = (new Vector3(v.x, v.w));
        vertList[2] = (new Vector3(v.z, v.w));
        vertList[3] = (new Vector3(v.z, v.y));


        uvs[0] = (new Vector4(0f, 0f, 1, flags));
        uvs[1] = (new Vector4(0f, 1f, 1, flags));
        uvs[2] = (new Vector4(1f, 1f, 1, flags));
        uvs[3] = (new Vector4(1f, 0f, 1, flags));

        triangles[0] = 0;
        triangles[1] = 1;
        triangles[2] = 2;

        triangles[3] = 2;
        triangles[4] = 3;
        triangles[5] = 0;
    }


    public void UpdateTextureIndex(int textureIndex)
    {
        for (int i = 0; i < uvs.Length; i++)
        {
            ref Vector4 uv = ref uvs[i];
            uv.z = textureIndex;
        }
    }


    public void Fill(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Vector3 localPosition)
    {
        var offset = vertList_.Count;
        var vertexCount = this.VertexCount;
        if(this.VertexCount!= vertList.Length)
        {
            UnityEngine.Debug.LogError($"Error:Length:{VertexCount},{vertList.Length}");
        }

        if (this.VertexCount != uvs.Length)
        {
            UnityEngine.Debug.LogError($"Error:Length:{VertexCount},{uvs.Length}");
        }

        if (colors.Length!= 0 && this.VertexCount != colors.Length)
        {
            UnityEngine.Debug.LogError($"Error:Length:{VertexCount},{colors.Length}");
        }


        for (int i = 0; i < vertexCount; i++)
        {
            vertList_.Add(vertList[i] + localPosition);
        }
        if(colors == Array.Empty<Color32>() || this.colors.Length!=vertexCount)
        {
            Color32 white = Color.white; //new Color32(255, 255, 255, 125);// 
            for (int i = 0; i < vertexCount; i++)
            {
                colors_.Add(white);
            }
        }
        else
        {
            colors_.AddRange(this.colors);
        }
       
        uvs_.AddRange(this.uvs);

        foreach (var i in this.triangles)
        {
            triangles_.Add(offset + i);
        }
    }

    public void FillWithMatrix(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Matrix4x4 mtx)
    {
        var offset = vertList_.Count;
        var vertexCount = this.VertexCount;
        for (int i = 0; i < vertexCount; i++)
        {
                vertList_.Add(mtx.MultiplyPoint(vertList[i]));
        }

        var white = Color.white;
        for (int i = 0; i < vertexCount; i++)
        {
            colors_.Add(white);
        }

        uvs_.AddRange(this.uvs);

        foreach (var i in this.triangles)
        {
            triangles_.Add(offset + i);
        }
    }

    public void Dispose()
    {
        Clear();
    }

    public void FillVertex(VertexHelper toFill,int flags,bool needColors = false)
    {
        this.Clear();
        if (needColors)
        {
            (this.VertexCount, this.IndicsCount) = toFill.FillData2(ref this.vertList, ref this.colors, ref this.uvs, ref this.triangles, Index, flags);
        }
        else
        {
            (this.VertexCount,this.IndicsCount) = toFill.FillData2(ref this.vertList,ref this.uvs,ref this.triangles, Index, flags);
        }
    } 

    private void Clear()
    {
      
    }

   
}

public class UIPackData : IUIData
{
    public List<UIMeshData> lists = new List<UIMeshData>();
    private static List<MaskableGraphic> tmp_graphics = new List<MaskableGraphic>();
    public static UIPackData Create(Transform ui)
    {
        var uiPackData = new UIPackData();
        tmp_graphics.Clear();
        ui.GetComponentsInChildren<MaskableGraphic>(true, tmp_graphics);
        foreach(var item in tmp_graphics)
        {
            if(item is Text txt)
            {
                uiPackData.lists.Add(UIMeshData.CreateTextData(txt));
            }else if(item is Image img)
            {
                uiPackData.lists.Add(UIMeshData.CreateImageData(img));
            }
        }
        tmp_graphics.Clear();
        return uiPackData;
    }

    public void Fill(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Vector3 localPosition)
    {
        foreach(var item in lists)
        {
            item.Fill(vertList_, uvs_, colors_, triangles_, localPosition);
        }
    }

    public void Dispose()
    {
        lists.Clear();
    }
}
