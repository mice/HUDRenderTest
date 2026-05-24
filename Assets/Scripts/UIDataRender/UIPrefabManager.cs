using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public interface IUIPrefabDataOwner
{
    IUIPrefabHolder DataHolder { get; }
}

/// <summary>
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
    /// </summary>
    public List<UIMeshData> uIMeshDatas = new List<UIMeshData>();

    public void Dispose()
    {
        draws.Clear();
        uIMeshDatas.Clear();
    }
}

/// <summary>
/// </summary>
public partial class UIPrefabRegistration
{
    /// <summary>
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
        LogDebug($"GetTextureIndex:{textures.IndexOf(texture)}");
        return textures.IndexOf(texture) + 1;
    }

    public void Register(IUIPrefabHolder holder)
    {
        var prefabOwner = holder?.Target;
        if (prefabOwner == null)
        {
            LogDebug("Should Not Be Null::");
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
                    LogDebug($"AddTexture:{obj}");
                }
            }
            else
            {
                textureDict.Add(guid, obj);
                LogDebug($"AddTexture:{obj}");
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
                        textures[tIndex] = textures[lastIndex];
                        textures.RemoveAt(lastIndex);
                        // TextureIndex is 1-based in IUIData.
                        textureNotify?.ReplaceTextureID(lastIndex + 1, tIndex + 1);

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
            LogDebug($"RemoveTexture:{_tex}");
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

    [Conditional("UI_VERBOSE")]
    private static void LogDebug(string message)
    {
        UnityEngine.Debug.Log(message);
    }
}
