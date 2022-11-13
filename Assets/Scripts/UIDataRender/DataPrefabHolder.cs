using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;



/// <summary>
/// 每个prefab都会生成一个DataPrefabHolder用来记录当前的prefab的UI信息;
/// </summary>
public class DataPrefabHolder : IUIPrefabHolder
{
    [SerializeField]
    protected UIPrefabOwner _target;
    public UIPrefabOwner Target => _target;
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
        if (uIMeshDatas == Array.Empty<UIMeshData>())
        {
            uIMeshDatas = new UIMeshData[draws.Length];
        }
        else if (uIMeshDatas.Length < draws.Length)
        {
            Array.Resize(ref uIMeshDatas, draws.Length);
        }
        for (int i = 0; i < draws.Length; i++)
        {
            uIMeshDatas[i] = uIMeshDatas[i] ?? new UIMeshData();
            draws[i].DoGenerate(uIMeshDatas[i], Target.transform);
        }
    }

    public void SetText(int index, string text)
    {
        var uiText = wrapper?.SetText(index, text);
        if (uiText != null && index < uIMeshDatas.Length)
        {
            uiText.DoGenerate(uIMeshDatas[index], Target.transform);
        }
    }

    public void SetSprite(int index, Sprite text)
    {
        var uiImg = wrapper?.SetSprite(index, text);
        if (uiImg != null && index < uIMeshDatas.Length)
        {
            uiImg.DoGenerate(uIMeshDatas[index], Target.transform);
        }
    }

    public void SetWidth(int index, int width)
    {
        var item = wrapper?.SetWidth(index, width);
        if (item != null && index < uIMeshDatas.Length)
        {
            item.DoGenerate(uIMeshDatas[index], Target.transform);
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

    /// <summary>
    /// 只改变triangle
    /// </summary>
    /// <param name="triangles_"></param>
    /// <param name="localPosition"></param>
    public void Fill(List<int> triangles_, Vector3 localPosition)
    {
        foreach (var item in uIMeshDatas)
        {
            item.FillToTriangleData(triangles_, localPosition);
        }
    }
}