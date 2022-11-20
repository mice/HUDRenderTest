using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public interface IUIPrefabDataOwner
{
    IUIPrefabHolder DataHolder { get; }
}

/// <summary>
/// 保存对应的Prefab
/// 需要知道显示需要的position,scale,rotation
/// </summary>
public interface IUIPrefabHolder
{
    UIPrefabOwner Target { get; }
    Vector3 Position { get; }
    UIPrefabRegistration wrapper { get; }

    void SetTarget(UIPrefabOwner target);

    IList<IUIData> UIMeshDatas { get; }
    void SetWrapper(UIPrefabRegistration wrapper);
    void BuildMesh(IUIDrawTarget[] draws);

    void Fill(List<Vector3> vertList_, List<Vector4> uvs_, List<Color32> colors_, List<int> triangles_, Vector3 localPosition);

    void Fill(List<int> triangles_, Vector3 localPosition);
}

public interface ITextureRecorder
{
    void OnTextureRegister(int guid,Texture obj);
    void OnTextureUnRegister(int guid);
}

public interface ITextureNotify
{
    void ReplaceTextureID(int lastIndex, int tIndex);
    void RemoveTextureID(int lastIndex);
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
    public UIPrefabOwner Owner { get; private set; }
    /// <summary>
    /// UI element
    /// </summary>
    public IUIDrawTarget[] draws;

    public UIPrefabRegistration(UIPrefabOwner owner, ITextureRecorder textureCall)
    {
        this.Owner = owner;
        TextureCall = textureCall;
        RecordDraws();
    }

    private void RecordDraws()
    {
        draws = new IUIDrawTarget[Owner.targets.Count];
        for (int i = 0; i < Owner.targets.Count; i++)
        {
            draws[i] = Owner.targets[i].GetComponent<IUIDrawTarget>();
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
public class UIPrefabManager : ITextureRecorder, ITextureNotify
{
    public static UIPrefabManager Instance { get; } = new UIPrefabManager();
    public Dictionary<UIPrefabOwner, UIPrefabRegistration> owners = new Dictionary<UIPrefabOwner, UIPrefabRegistration>();
    private readonly List<Texture>  textures = new List<Texture>();
    private readonly Dictionary<int, Texture> textureDict = new Dictionary<int, Texture>();

    public ITextureNotify textureNotify;

    private HashSet<IUIPrefabHolder> holders = new HashSet<IUIPrefabHolder>();
    private UIPrefabManager()
    {
        this.textureNotify = this;
    }

    void ITextureNotify.ReplaceTextureID(int lastIndex, int tIndex)
    {
        foreach(var holder in holders)
        {
            foreach(var item in holder.UIMeshDatas)
            {
                if (item.TextureIndex == lastIndex)
                {
                    item.UpdateTextureIndex(tIndex);
                }
            }
        }
    }

    void ITextureNotify.RemoveTextureID(int lastIndex)
    {
        foreach (var holder in holders)
        {
            foreach (var item in holder.UIMeshDatas)
            {
                if (item.TextureIndex == lastIndex)
                {
                    item.UpdateTextureIndex(-1);
                }
            }
        }
    }

    public void AddHolder(IUIPrefabHolder holder)
    {
        holders.Add(holder);
    }

    public void RemoveHolder(IUIPrefabHolder holder)
    {
        holders.Remove(holder);
    }

    public int GetTextureIndex(Texture texture)
    {
        UnityEngine.Debug.LogError($"GetTextureIndex:{textures.IndexOf(texture)}");
        return textures.IndexOf(texture) + 1;
    }

    /// <summary>
    /// 记录Prefab
    /// </summary>
    /// <param name="prefabOwner"></param>
    public void Register(IUIPrefabHolder holder)
    {
        var prefabOwner = holder?.Target;
        if (prefabOwner == null)
        {
            UnityEngine.Debug.LogError("Shold Not Be Null::");
            return;
        }

        if (!this.owners.TryGetValue(prefabOwner,out _))
        {
            var reg = new UIPrefabRegistration(prefabOwner, this);
            holder.SetWrapper(reg);
            this.owners.Add(prefabOwner, reg);
        }
    }
    /// <summary>
    /// guid为Image的InstanceID
    /// </summary>
    /// <param name="guid"></param>
    /// <param name="obj"></param>
    void ITextureRecorder.OnTextureRegister(int guid, Texture obj)
    {
        if (obj != null) { 

            if(textureDict.TryGetValue(guid,out var _tex))
            {
                if (_tex != obj)
                {
                    OnTextureUnRegister(guid, false);
                    textureDict[guid] = obj;
                    if (!textures.Contains(obj))
                        textures.Add(obj);
                    UnityEngine.Debug.LogError($"AddTexture:{obj}");
                }
            }
            else
            {
                textureDict.Add(guid, obj);
                UnityEngine.Debug.LogError($"AddTexture:{obj}");
                if (!textures.Contains(obj))
                    textures.Add(obj);
            }
        }
        else
        {
            OnTextureUnRegister(guid,false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnTextureUnRegister(int guid,bool removeFromDict)
    {
        if(textureDict.TryGetValue(guid,out var _tex))
        {
            int texCount = 0;
            //如果当前数量只有一个,那么就表示移除了以后,就没有这个Texture了.
            foreach (var item in textureDict)
            {
                if (item.Value == _tex)
                {
                    texCount++;
                }
            }

            if (texCount == 1)
            {
                var tIndex = textures.IndexOf(_tex);
                var lastIndex = textures.Count - 1;
                if(tIndex != -1)
                {
                    if (tIndex != lastIndex)
                    {
                        var replace = textures[lastIndex];
                        textures[tIndex] = textures[lastIndex];
                        textures.RemoveAt(lastIndex);
                        //现在的问题是要update:TextureID;
                        textureNotify?.ReplaceTextureID(lastIndex, tIndex);

                    }
                    else
                    {
                        textures.RemoveAt(lastIndex);
                        textureNotify?.RemoveTextureID(lastIndex + 1);
                    }
                }
            }
            if(removeFromDict)
                textureDict.Remove(guid);
            UnityEngine.Debug.LogError($"RemoveTexture:{_tex}");
        }
    }

    void ITextureRecorder.OnTextureUnRegister(int guid)
    {
        OnTextureUnRegister(guid,true);
    }

    public UIPrefabRegistration Generate(IUIPrefabHolder holder)
    {
        if(this.owners.TryGetValue(holder.Target,out var reg)){
            reg.Generate(holder);
            return reg;
        }
        return null;
    }

    private readonly int[] MaterialProperties = new int[] {
        Shader.PropertyToID("_MainTex1"),
        Shader.PropertyToID("_MainTex2"),
        Shader.PropertyToID("_MainTex3"),
        Shader.PropertyToID("_MainTex4"),
        Shader.PropertyToID("_MainTex5"),
        Shader.PropertyToID("_MainTex6"),
        Shader.PropertyToID("_MainTex7"),
    };

    public void UpdateTexture(Material comb_Material)
    {
        if (comb_Material != null)
        {
            var count = Mathf.Min(textures.Count, MaterialProperties.Length);
            for (int i = 0; i < count; i++)
            {
                comb_Material.SetTexture(MaterialProperties[i], textures[i]);
            }
        }
    }
}
