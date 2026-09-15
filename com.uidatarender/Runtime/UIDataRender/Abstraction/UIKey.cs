using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IUIKeyBuilder
{

}

public interface IUIMeshDataManager
{

}

public interface IUIDataPrefab
{

}


public class UIKeyBuilder: IUIKeyBuilder
{
    /// <summary>
    /// 什么时候清理呢?
    /// </summary>
    public Dictionary<string,int> contentKeys = new Dictionary<string,int>();
    public List<string> keys = new List<string>();

    /// <summary>
    /// 什么时候清理呢?
    /// </summary>
    public Dictionary<string, int> textureKeys = new Dictionary<string, int>();
    public List<string> textures = new List<string>();

    /// <summary>
    /// 假设只有一种字体,所以key始终为0;
    /// 那什么时候清理这个Texture呢?
    /// </summary>
    /// <returns></returns>
    public UIKey FromText(string content)
    {
        int _hashContent;
        if (!contentKeys.TryGetValue(content,out _hashContent))
        {
            keys.Add(content);
            _hashContent = keys.Count - 1;
            contentKeys.Add(content, _hashContent);
        }
        return new UIKey()
        {
            key = 0,
            contentHash = _hashContent
        };
    }

    /// <summary>
    /// 这个key要包含4个要素,1,Atlas,2,Sprite,width,height
    /// [16|16|w|h]
    /// 移除怎么判断呢?
    /// </summary>
    /// <param name="sp"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <returns></returns>
    public UIKey FromSimpleSprite(Sprite sp, int width, int height)
    {
        return new UIKey();
    }

}

public struct UIKey :IEquatable<UIKey>,IEqualityComparer<UIKey>
{
    public int key;
    public int contentHash;


    public bool Equals(UIKey other)
    {
        return other.key == this.key && other.contentHash == this.contentHash;
    }

    public bool Equals(UIKey x, UIKey y)
    {
        return x.key == y.key && x.contentHash == y.contentHash;
    }

    public int GetHashCode(UIKey obj)
    {
        return (obj.key,obj.contentHash).GetHashCode();
    }
}
