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

    public IList<T> UIMeshDatas => uIMeshDatas;
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

    IEnumerator<IUIData> IEnumerable<IUIData>.GetEnumerator()
    {
        return new ListEnumerator(uIMeshDatas);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new ListEnumerator(uIMeshDatas);
    }

    public class ListEnumerator : IEnumerator<IUIData>
    {
        public IUIData Current => _current;
        private IList<IUIData> _list;
        private IUIData _current;
        private int _index;
        private int _total = 0;

        public ListEnumerator(IList<IUIData> list)
        {
            this._list = list;
            _total = list.Count;
            _index = 0;
            _current = default(IUIData);
        }

        object System.Collections.IEnumerator.Current => _current;

        public void Dispose()
        {
            
        }

        public bool MoveNext()
        {
            if(_total > 0 && _index < _total)
            {
                _current = _list[_index];
                _index++;
                return true;

            }
            _index = -1;
            _current = default;
            return false; 
        }

        public void Reset()
        {
            _current = default;
        }
    }

}