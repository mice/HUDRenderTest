using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.WSA.Input;


/// <summary>
/// 每个prefab都会生成一个DataPrefabHolder用来记录当前的prefab的UI信息;
/// </summary>
public class DataPrefabHolder<T> : IUIPrefabHolder 
    where T: class, IUIData, new()
{
    [SerializeField]
    protected UIPrefabOwner _target;
    public UIPrefabOwner Target => _target;
    public UIPrefabRegistration wrapper { get; set; }

    private Vector3 _position;
    public Vector3 Position => _position;

    public IList<IUIData> UIMeshDatas => uIMeshDatas;
    /// <summary>
    /// UI element 对应的Mesh信息
    /// </summary>
    public T[] uIMeshDatas = Array.Empty<T>();

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
        if (uIMeshDatas == Array.Empty<T>())
        {
            uIMeshDatas = new T[draws.Length];
        }
        else if (uIMeshDatas.Length < draws.Length)
        {
            Array.Resize(ref uIMeshDatas, draws.Length);
        }
        for (int i = 0; i < draws.Length; i++)
        {
            uIMeshDatas[i] = uIMeshDatas[i] ?? new T();
            draws[i].DoGenerate(uIMeshDatas[i], Target.transform);
        }
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

public static class PrefabHolderExt
{

    public static void SetText(this IUIPrefabHolder target,int index, string text)
    {
        if (target == null) return;
        var uiText = target.wrapper?.SetText(index, text);
        var uiMeshData = target?.UIMeshDatas;
        if (uiText != null && index < uiMeshData.Count)
        {
            uiText.DoGenerate(uiMeshData[index], target.Target.transform);
        }
    }

    public static void SetSprite(this IUIPrefabHolder target, int index, Sprite text)
    {
        if (target == null) return;
        var uiImg = target.wrapper?.SetSprite(index, text);
        var uiMeshData = target?.UIMeshDatas;
        if (uiImg != null && index < uiMeshData.Count)
        {
            uiImg.DoGenerate(uiMeshData[index], target.Target.transform);
        }
    }

    public static void SetWidth(this IUIPrefabHolder target, int index, int width)
    {
        if (target == null) return;
        var item = target.wrapper?.SetWidth(index, width);
        var uiMeshData = target?.UIMeshDatas;
        if (item != null && index < uiMeshData.Count)
        {
            item.DoGenerate(uiMeshData[index], target.Target.transform);
        }
    }

    public static void SetTextureIndex(this IUIPrefabHolder target, int index, int textureIndex)
    {
        if (target == null) return;
        var uiMeshData = target?.UIMeshDatas;
        if (index < uiMeshData.Count)
            uiMeshData[index].UpdateTextureIndex(textureIndex);
    }

}