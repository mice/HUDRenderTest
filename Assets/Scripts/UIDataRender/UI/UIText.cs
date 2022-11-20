using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Mesh;

public class UIText : Text, IUIDrawTarget
{
    private bool _isGenerating = false;
    private IUIData tmp_meshData;
    private Transform tmp_root;
    public bool HasModifier = false;
    private UIOutline outline = null;

    public void DoGenerate(IUIData meshData,Transform root = null)
    {
        _isGenerating = true;
        tmp_meshData = meshData;
        tmp_root = root;

        outline = this.GetComponent<UIOutline>();
        HasModifier = outline != null;
        if(outline != null)
        {
            outline.tmp_meshData = tmp_meshData;
            outline.tmp_root = tmp_root;
        }

        UpdateGeometry();
        tmp_meshData = null;
        tmp_root = null;
        if (outline != null)
        {
            outline.tmp_meshData = null;
            outline.tmp_root = null;
        }
        outline = null;
        _isGenerating = false;
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        base.OnPopulateMesh(toFill);
        if (!HasModifier && _isGenerating && tmp_meshData != null)
        {
            tmp_meshData.FillVertex(toFill, 1);
            var _transform = transform;
            if (tmp_root != null && tmp_root != _transform && _transform.IsChildOf(tmp_root))
            {
                var mtx = Utility.CalcMatrix(tmp_root, _transform);
                tmp_meshData.TransformVertex(mtx);
            }
        }
    }
}
