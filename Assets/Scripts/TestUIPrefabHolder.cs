using System.Collections.Generic;
using UnityEngine;

public class TestUIPrefabHolder : MonoBehaviour
{
    public Font font;
    public Camera ui_Camera;
    public Transform ui_root;

    public List<UIPrefabHolder> holders;

    // Slim path: shared geometry buffer, single draw call
    private Mesh combine_mesh;
    private Material comb_Material;

    // Non-slim batched path
    private const int MaxSlotsPerBatch = 3;
    private MergeBatcher _batcher;
    private readonly List<IUIPrefabHolder> _holderInterfaces = new List<IUIPrefabHolder>();
    private readonly Dictionary<IUIPrefabHolder, Vector3> _positionMap = new Dictionary<IUIPrefabHolder, Vector3>();
    private Mesh[] _batchMeshes = System.Array.Empty<Mesh>();
    private Material[] _batchMaterials = System.Array.Empty<Material>();
    private int _batchCount;

    private readonly UIPrefabManager uiPrefabManager = UIPrefabManager.Instance;
    private bool UseSlim = false;
    private bool created = false;

    [Button("ReCreate")]
    public string _X;
    [Button("ModifyText")]
    public string _Y;
    [Button("NullIcon")]
    public string _Z;

    public void ReCreate()
    {
        if (!UnityEngine.Application.isPlaying)
        {
            UnityEngine.Debug.LogError("Only Run In PlayModel");
            return;
        }
        created = true;
        foreach (var holder in holders)
        {
            holder.UseSlim(UseSlim);
            uiPrefabManager.Register(holder.DataHolder);
            holder.SetText(2, "NiHao" + UnityEngine.Random.Range(1, 10));
            uiPrefabManager.Generate(holder.DataHolder);
        }

        if (UseSlim)
        {
            combine_mesh = combine_mesh ?? new Mesh();
            comb_Material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
            uiPrefabManager.UpdateTexture(comb_Material);
            comb_Material.SetTexture("_MainTex0", font.material.mainTexture);
            comb_Material.renderQueue = 3000;
        }
        else
        {
            _batcher = new MergeBatcher(MaxSlotsPerBatch);
            _holderInterfaces.Clear();
            _positionMap.Clear();
            foreach (var h in holders)
            {
                _holderInterfaces.Add(h.DataHolder);
                _positionMap[h.DataHolder] = h.transform.localPosition;
            }
        }

        RebuildMesh();
    }

    private void ModifyText()
    {
        if (!created) return;
        holders[0].SetText(2, "NiHao" + UnityEngine.Random.Range(1, 10));
        holders[0].SetWidth(1, 80 + UnityEngine.Random.Range(10, 20));
        UpdatePositions();
        RebuildMesh();
    }

    private void NullIcon()
    {
        if (!created) return;
        holders[2].SetSprite(2, null);
        RebuildMesh();
    }

    private void UpdatePositions()
    {
        if (UseSlim) return;
        foreach (var h in holders)
            _positionMap[h.DataHolder] = h.transform.localPosition;
    }

    private readonly List<int> triangles = new List<int>();

    private void RebuildMesh()
    {
        triangles.Clear();
        if (UseSlim)
        {
            foreach (var holder in holders)
                holder.Fill(triangles, holder.transform.localPosition);
            var geo = UIMeshDataX.geometry;
            int vc = geo.drawVertex.Length;
            combine_mesh.SetVertices(geo.drawVertex, 0, vc);
            combine_mesh.SetUVs(0, geo.uvs, 0, vc);
            combine_mesh.SetColors(geo.colors, 0, vc);
            combine_mesh.SetTriangles(triangles, 0);
            combine_mesh.RecalculateBounds();
            return;
        }

        var batches = _batcher.Plan(_holderInterfaces);
        _batchCount = batches.Count;
        EnsureBatchArrays(_batchCount);

        for (int b = 0; b < batches.Count; b++)
        {
            var batch = batches[b];
            var localSlotMap = batch.BuildLocalSlotMap();

            var vertBuff = new List<Vector3>();
            var uvs = new List<Vector4>();
            var colors = new List<Color32>();
            triangles.Clear();

            foreach (var holder in batch.Holders)
            {
                var pos = _positionMap.TryGetValue(holder, out var p) ? p : Vector3.zero;
                holder.Fill(vertBuff, uvs, colors, triangles, pos);
            }

            ApplySlotRemap(uvs, localSlotMap);

            _batchMeshes[b].SetVertices(vertBuff);
            _batchMeshes[b].SetUVs(0, uvs);
            _batchMeshes[b].SetColors(colors);
            _batchMeshes[b].SetTriangles(triangles, 0);
            _batchMeshes[b].RecalculateBounds();

            uiPrefabManager.UpdateTexture(_batchMaterials[b], batch);
        }
    }

    private void EnsureBatchArrays(int needed)
    {
        if (_batchMeshes.Length >= needed && _batchMaterials.Length >= needed) return;
        var newMeshes = new Mesh[needed];
        var newMats = new Material[needed];
        for (int i = 0; i < needed; i++)
        {
            newMeshes[i] = i < _batchMeshes.Length ? _batchMeshes[i] : new Mesh();
            if (i < _batchMaterials.Length)
            {
                newMats[i] = _batchMaterials[i];
            }
            else
            {
                newMats[i] = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
                newMats[i].SetTexture("_MainTex0", font.material.mainTexture);
                newMats[i].renderQueue = 3000;
            }
        }
        _batchMeshes = newMeshes;
        _batchMaterials = newMats;
    }

    private static void ApplySlotRemap(List<Vector4> uvs, IReadOnlyDictionary<int, int> localSlotMap)
    {
        for (int i = 0; i < uvs.Count; i++)
        {
            var uv = uvs[i];
            int globalSlot = (int)uv.z;
            if (globalSlot > 0 && localSlotMap.TryGetValue(globalSlot, out int localSlot))
                uvs[i] = new Vector4(uv.x, uv.y, localSlot, uv.w);
        }
    }

    private void LateUpdate()
    {
        var matrix = ui_root.localToWorldMatrix;
        if (UseSlim)
        {
            if (combine_mesh != null)
                Graphics.DrawMesh(combine_mesh, matrix, comb_Material, 5, ui_Camera);
        }
        else
        {
            for (int b = 0; b < _batchCount; b++)
                Graphics.DrawMesh(_batchMeshes[b], matrix, _batchMaterials[b], 5, ui_Camera);
        }
    }
}
