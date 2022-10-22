using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 把Image
/// </summary>
public class UIImage :Image,IUIDrawTarget
{
    private bool _isGenerating = false;
    private UIMeshData tmp_meshData;
    private Transform tmp_root;
    [ContextMenu("ReCreate")]
    private void _UpdateGeom()
    {
        _isGenerating = true;
        UpdateGeometry();
        _isGenerating = false;
    }

    public void DoGenerate(UIMeshData meshData,Transform root = null)
    {
        _isGenerating = true;
        meshData.MaterialIndex = 1;
        tmp_meshData = meshData;
        tmp_root = root;
        UpdateGeometry();
        tmp_meshData = null;
        tmp_root = null;
        _isGenerating = false;
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        base.OnPopulateMesh(toFill);
        if (_isGenerating && tmp_meshData != null)
        {
            tmp_meshData.FillVertex(toFill, 0);
            var _transform = transform;
            if(tmp_root != null && tmp_root!= _transform && _transform.IsChildOf(tmp_root))
            {
                var mtx = Utility.CalcMatrix(tmp_root, _transform);
                tmp_meshData.TransformVertex(mtx);
            }
        }
    }
}
