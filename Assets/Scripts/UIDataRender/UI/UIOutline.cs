using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIOutline : Outline
{
    public IUIData tmp_meshData;
    public Transform tmp_root;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (tmp_meshData != null)
        {
            tmp_meshData.FillVertex(vh, 1);
            var _transform = transform;
            if (tmp_root != null && tmp_root != _transform && _transform.IsChildOf(tmp_root))
            {
                var mtx = Utility.CalcMatrix(tmp_root, _transform);
                tmp_meshData.TransformVertex(mtx);
            }
        }
        base.ModifyMesh(vh);
        if (tmp_meshData != null)
        { 
            tmp_meshData.FillVertex(vh, 1);
            var _transform = transform;
            if (tmp_root != null && tmp_root != _transform && _transform.IsChildOf(tmp_root))
            {
                var mtx = Utility.CalcMatrix(tmp_root, _transform);
                tmp_meshData.TransformVertex(mtx);
            }
        }
    }
}
