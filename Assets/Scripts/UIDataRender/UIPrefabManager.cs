using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

/// <summary>
/// 保存对应的Prefab
/// 需要知道显示需要的position,scale,rotation
/// </summary>
public interface IUIPrefabHolder
{
    UIPrefabOwner target { get; }
    Vector3 Position { get; }
    void SetWrapper(UIPrefabRegistration wrapper);
    void BuildMesh(IUIDrawTarget[] draws);
}

public interface ITextureRecorder
{
    void OnTextureRegister(int guid,Texture obj);
    void OnTextureUnRegister(int guid);
}


public class UIPrefabWrapper : IDisposable
{
    public UIPrefabOwner target;
    /// <summary>
    /// UI element
    /// </summary>
    public List<IUIDrawTarget> draws = new List<IUIDrawTarget>();
    /// <summary>
    /// UI element 对应的Mesh信息
    /// </summary>
    public List<UIMeshData> uIMeshDatas = new List<UIMeshData>();

    public void Dispose()
    {
        draws.Clear();
        uIMeshDatas.Clear();
    }
}

/// <summary>
/// 只记录显示对象,不负责Mesh生成
/// </summary>
public partial class UIPrefabRegistration
{
    /// <summary>
    /// 这里需要有一些统计功能,保证uv设置indics的次数最少.
    /// </summary>
    public ITextureRecorder TextureCall;
    public UIPrefabOwner owner { get; private set; }
    /// <summary>
    /// UI element
    /// </summary>
    public IUIDrawTarget[] draws;

    public UIPrefabRegistration(UIPrefabOwner owner, ITextureRecorder textureCall)
    {
        this.owner = owner;
        TextureCall = textureCall;
        RecordDraws();
    }

    private void RecordDraws()
    {
        draws = new IUIDrawTarget[owner.targets.Count];
        for (int i = 0; i < owner.targets.Count; i++)
        {
            draws[i] = owner.targets[i].GetComponent<IUIDrawTarget>();
            if (draws[i] is UIImage uiImg)
            {
                TextureCall.OnTextureRegister(uiImg.GetInstanceID(), uiImg.sprite? uiImg.sprite.texture:null);
            }
        }
    }

    internal void Generate(IUIPrefabHolder holder)
    {
        holder.BuildMesh(draws);
    }
}

/// <summary>
/// 需要动态写UV的Index.
/// </summary>
public class UIPrefabManager: ITextureRecorder
{
    public Dictionary<UIPrefabOwner, UIPrefabRegistration> owners = new Dictionary<UIPrefabOwner, UIPrefabRegistration>();
    private List<Texture> textures = new List<Texture>();
    private Dictionary<int, Texture> textureDict = new Dictionary<int, Texture>();

    public UIPrefabManager()
    {
        
    }

    /// <summary>
    /// 记录Prefab
    /// </summary>
    /// <param name="prefabOwner"></param>
    public void Register(IUIPrefabHolder holder)
    {
        var prefabOwner = holder?.target;
        if (prefabOwner == null)
        {
            UnityEngine.Debug.LogError("Shold Not Be Null::");
            return;
        }

        if (!this.owners.TryGetValue(prefabOwner,out var reg))
        {
            reg = new UIPrefabRegistration(prefabOwner, this);
            holder.SetWrapper(reg);
            this.owners.Add(prefabOwner, reg);
        }
    }

    void ITextureRecorder.OnTextureRegister(int guid, Texture obj)
    {
        if (obj != null) { 

            if(textureDict.TryGetValue(guid,out var _tex))
            {
                if (_tex != obj)
                {
                    _OnTextureUnRegister(guid, false);
                    textureDict[guid] = obj;
                    if (!textures.Contains(_tex))
                        textures.Add(_tex);
                }
            }
            else
            {
                textureDict.Add(guid, _tex);
                if (!textures.Contains(_tex))
                    textures.Add(_tex);
            }
        }
        else
        {
            _OnTextureUnRegister(guid,false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void _OnTextureUnRegister(int guid,bool removeFromDict)
    {
        if(textureDict.TryGetValue(guid,out var _tex))
        {
            int texCount = 0;
            foreach (var item in textureDict)
            {
                if (item.Value == _tex)
                {
                    texCount++;
                }
            }
            if (texCount == 1)
            {
                textures.Remove(_tex);
            }
            if(removeFromDict)
                textureDict.Remove(guid);
        }
    }

    void ITextureRecorder.OnTextureUnRegister(int guid)
    {
        _OnTextureUnRegister(guid,true);
    }

    public UIPrefabRegistration Generate(IUIPrefabHolder holder)
    {
        if(this.owners.TryGetValue(holder.target,out var reg)){
            reg.Generate(holder);
            return reg;
        }
        return null;
    }
}
