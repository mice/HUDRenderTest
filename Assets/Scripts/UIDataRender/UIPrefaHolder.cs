using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 他的生命周期.
/// </summary>
public class UIPrefaHolder : MonoBehaviour,IUIPrefabHolder
{
    [SerializeField]
    protected UIPrefabOwner _target;
    public UIPrefabOwner Target => _target;

    public Vector3 Position => transform.localPosition;
   
    private DataPrefabHolder<UIMeshData> dataHolder;

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
        dataHolder = dataHolder ?? new DataPrefabHolder<UIMeshData>();
        dataHolder.SetTarget(_target);
        UIPrefabManager.Instance.AddHolder(dataHolder);
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

    /// <summary>
    /// 清楚的时候
    /// </summary>
    void OnDestroy()
    {
        if (dataHolder != null)
        {
            UIPrefabManager.Instance.RemoveHolder(dataHolder);
            dataHolder = null;
        }
    }

    public IEnumerator<IUIData> GetEnumerator()
    {
        throw new Exception("No Imple");
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new Exception("No Imple");
    }
}
