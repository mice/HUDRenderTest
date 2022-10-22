using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UIPrefaHolder : MonoBehaviour,IUIPrefabHolder
{
    [SerializeField]
    protected UIPrefabOwner _target;
    public UIPrefabOwner target => _target;

    public Vector3 Position => transform.localPosition;
    private DataPrefaHolder dataHolder;

    private void Awake()
    {
        InitDataHolder();
    }

    public void SetWrapper(UIPrefabRegistration wrapper)
    {
        dataHolder.SetWrapper(wrapper);
    }

    private void InitDataHolder()
    {
        dataHolder = dataHolder ?? new DataPrefaHolder();
        dataHolder.SetTarget(_target);
    }

    /// <summary>
    /// 需要在什么时候处理呢
    /// </summary>
    /// <param name="draws"></param>
    public void BuildMesh(IUIDrawTarget[] draws)
    {
        InitDataHolder();
        dataHolder.BuildMesh(draws);
    }

    public void SetText(int index, string text)
    {
        dataHolder.SetText(index, text);
    }

    public void SetSprite(int index, Sprite spr)
    {
        dataHolder?.SetSprite(index, spr);
    }

    public void SetWidth(int index,int width)
    {
        dataHolder?.SetWidth(index, width);
    }

    //这个应该是内部算好.
    public void SetTextureIndex(int index,int textureIndex)
    {
        dataHolder?.SetTextureIndex(index, textureIndex);
    }

    /// <summary>
    /// 算法应该是:
    /// 遇到Text,不需要改变UV
    /// 遇到Image,需要改变UV
    /// </summary>
    /// <param name="vertList_"></param>
    /// <param name="uvs_"></param>
    /// <param name="colors_"></param>
    /// <param name="triangles_"></param>
    /// <param name="localPosition"></param>
    public void Fill(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Vector3 localPosition)
    {
        dataHolder.Fill(vertList_, uvs_, colors_, triangles_, localPosition);
    }

    public void Fill(List<int> triangles_, Vector3 localPosition)
    {
        dataHolder.Fill(triangles_, localPosition);
    }
}

public class DataPrefaHolder : IUIPrefabHolder
{
    [SerializeField]
    protected UIPrefabOwner _target;
    public UIPrefabOwner target => _target;
    public UIPrefabRegistration wrapper { get; set; }

    private Vector3 _position;
    public Vector3 Position => _position; 

    /// <summary>
    /// UI element 对应的Mesh信息
    /// </summary>
    public UIMeshData[] uIMeshDatas = Array.Empty<UIMeshData>();

    public void SetTarget(UIPrefabOwner target)
    {
        _target = target;
    }

    public void SetWrapper(UIPrefabRegistration wrapper)
    {
        this.wrapper = wrapper;
    }

    public void SetPosition(Vector3 position)
    {
        _position = position;
    }

    /// <summary>
    /// 需要在什么时候处理呢
    /// </summary>
    /// <param name="draws"></param>
    public void BuildMesh(IUIDrawTarget[] draws)
    {
        if(uIMeshDatas == Array.Empty<UIMeshData>())
        {
            uIMeshDatas = new UIMeshData[draws.Length];
        }else if (uIMeshDatas.Length<draws.Length)
        {
            Array.Resize(ref uIMeshDatas, draws.Length);
        }
        for (int i = 0; i < draws.Length; i++)
        {
            uIMeshDatas[i] = uIMeshDatas[i] ?? new UIMeshData();
            draws[i].DoGenerate(uIMeshDatas[i], target.transform);
        }
    }

    public void SetText(int index, string text)
    {
        var uiText = wrapper?.SetText(index, text);
        if (uiText != null && index < uIMeshDatas.Length)
        {
            uiText.DoGenerate(uIMeshDatas[index], target.transform);
        }
    }

    public void SetSprite(int index, Sprite text)
    {
        var uiImg = wrapper?.SetSprite(index, text);
        if (uiImg != null && index < uIMeshDatas.Length)
        {
            uiImg.DoGenerate(uIMeshDatas[index], target.transform);
        }
    }

    public void SetWidth(int index, int width)
    {
        var item = wrapper?.SetWidth(index, width);
        if (item != null && index < uIMeshDatas.Length)
        {
            item.DoGenerate(uIMeshDatas[index], target.transform);
        }
    }

    public void SetTextureIndex(int index, int textureIndex)
    {
        if (index < uIMeshDatas.Length)
            uIMeshDatas[index].UpdateTextureIndex(textureIndex);
    }

    /// <summary>
    /// 算法应该是:
    /// 遇到Text,不需要改变UV
    /// 遇到Image,需要改变UV
    /// </summary>
    /// <param name="vertList_"></param>
    /// <param name="uvs_"></param>
    /// <param name="colors_"></param>
    /// <param name="triangles_"></param>
    /// <param name="localPosition"></param>
    public void Fill(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Vector3 localPosition)
    {
        foreach (var item in uIMeshDatas)
        {
            item.FillToDrawData(vertList_, uvs_, colors_, triangles_, localPosition);
        }
    }

    public void Fill(List<int> triangles_, Vector3 localPosition)
    {
        foreach(var item in uIMeshDatas)
        {
            item.FillToTriangleData(triangles_,localPosition);
        }
    }
}
