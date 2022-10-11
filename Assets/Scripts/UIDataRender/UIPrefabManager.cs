using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

/// <summary>
/// �����Ӧ��Prefab
/// ��Ҫ֪����ʾ��Ҫ��position,scale,rotation
/// </summary>
public interface IUIPrefabHolder
{
    UIPrefabOwner target { get; }
    Vector3 Position { get; }
    void SetWrapper(UIPrefabRegistration wrapper);
    void BuildMesh(IUIDrawTarget[] draws);
}

public class UIPrefabWrapper : IDisposable
{
    public UIPrefabOwner target;
    /// <summary>
    /// UI element
    /// </summary>
    public List<IUIDrawTarget> draws = new List<IUIDrawTarget>();
    /// <summary>
    /// UI element ��Ӧ��Mesh��Ϣ
    /// </summary>
    public List<UIMeshData> uIMeshDatas = new List<UIMeshData>();

    public void Dispose()
    {
        draws.Clear();
        uIMeshDatas.Clear();
    }
}


public partial class UIPrefabRegistration
{
    /// <summary>
    /// ������Ҫ��һЩͳ�ƹ���,��֤uv����indics�Ĵ�������.
    /// </summary>
    public System.Action<Texture> TextureCall;
    public UIPrefabOwner owner { get; private set; }
    /// <summary>
    /// UI element
    /// </summary>
    public IUIDrawTarget[] draws;

    public UIPrefabRegistration(UIPrefabOwner owner, Action<Texture> textureCall)
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
                if (uiImg.sprite != null && uiImg.sprite.texture != null)
                {
                    TextureCall(uiImg.sprite.texture);
                }
            }
        }
    }

    internal void Generate(IUIPrefabHolder holder)
    {
        holder.BuildMesh(draws);
    }
}

/// <summary>
/// ��Ҫ��̬дUV��Index.
/// </summary>
public class UIPrefabManager
{
    public Dictionary<UIPrefabOwner, UIPrefabRegistration> owners = new Dictionary<UIPrefabOwner, UIPrefabRegistration>();
    private List<Texture> textures = new List<Texture>();

    private System.Action<Texture> OnTextureRegister_instance;
    public UIPrefabManager()
    {
        OnTextureRegister_instance = OnTextureRegister;
    }
    /// <summary>
    /// ��¼Prefab
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
            reg = new UIPrefabRegistration(prefabOwner, OnTextureRegister_instance);
            holder.SetWrapper(reg);
            this.owners.Add(prefabOwner, reg);
        }
    }

    private void OnTextureRegister(Texture obj)
    {
        if (obj!=null && !textures.Contains(obj))
        {
            textures.Add(obj);
        }
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
