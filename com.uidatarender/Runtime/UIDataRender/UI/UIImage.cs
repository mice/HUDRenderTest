using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Profiling;

/// <summary>
/// 把Image
/// </summary>
public class UIImage :Image,IUIDrawTarget
{
    private static readonly ProfilerMarker GenerateMarker = new ProfilerMarker("UIData.UIImage.DoGenerate");
    private static readonly ProfilerMarker TextureLookupMarker = new ProfilerMarker("UIData.UIImage.TextureLookup");
    private static readonly ProfilerMarker UpdateGeometryMarker = new ProfilerMarker("UIData.UIImage.UpdateGeometry");
    private static readonly ProfilerMarker TransformMarker = new ProfilerMarker("UIData.UIImage.TransformVertex");
    private static readonly ProfilerMarker TransformCheckMarker = new ProfilerMarker("UIData.UIImage.Transform.Check");
    private static readonly ProfilerMarker IsChildOfMarker = new ProfilerMarker("UIData.UIImage.Transform.IsChildOf");
    private static readonly ProfilerMarker CalcMatrixMarker = new ProfilerMarker("UIData.UIImage.Transform.CalcMatrix");
    private static readonly ProfilerMarker BasePopulateMarker = new ProfilerMarker("UIData.UIImage.BasePopulateMesh");
    private static readonly ProfilerMarker CustomPopulateMarker = new ProfilerMarker("UIData.UIImage.CustomPopulateMesh");
    private bool _isGenerating = false;
    private IUIData tmp_meshData;
    private Transform tmp_root;
    private readonly VertexHelper extractionHelper = new VertexHelper();
    [ContextMenu("ReCreate")]
    private void _UpdateGeom()
    {
        _isGenerating = true;
        UpdateGeometry();
        _isGenerating = false;
    }

    public void DoGenerate(IUIData meshData,Transform root = null)
    {
        using (GenerateMarker.Auto())
        {
            _isGenerating = true;
            using (TextureLookupMarker.Auto())
            {
                if (sprite == null)
                    meshData.TextureIndex = -1;
                else
                    meshData.TextureIndex = UIPrefabManager.Instance.GetTextureIndex(sprite.texture);
            }
       
            tmp_meshData = meshData;
            tmp_root = root;
            extractionHelper.Clear();
            using (UpdateGeometryMarker.Auto())
                base.OnPopulateMesh(extractionHelper);
            using (CustomPopulateMarker.Auto())
                meshData.FillVertex(extractionHelper, 0);
            TransformExtractedMesh(meshData);
            tmp_meshData = null;
            tmp_root = null;
            _isGenerating = false;
        }
    }

    protected override void OnPopulateMesh(VertexHelper toFill)
    {
        using (BasePopulateMarker.Auto())
        {
            base.OnPopulateMesh(toFill);
        }

        if (_isGenerating && tmp_meshData != null)
        {
            using (CustomPopulateMarker.Auto())
                tmp_meshData.FillVertex(toFill, 0);

            using (TransformCheckMarker.Auto())
            {
                var _transform = transform;
                bool isChild = false;
                if (tmp_root != null && tmp_root != _transform)
                {
                    using (IsChildOfMarker.Auto())
                        isChild = _transform.IsChildOf(tmp_root);
                }
                if (isChild)
                {
                    Matrix4x4 mtx;
                    using (CalcMatrixMarker.Auto())
                        mtx = Utility.CalcMatrix(tmp_root, _transform);
                    using (TransformMarker.Auto())
                        tmp_meshData.TransformVertex(mtx);
                }
            }
        }
    }

    private void TransformExtractedMesh(IUIData meshData)
    {
        using (TransformCheckMarker.Auto())
        {
            var current = transform;
            bool isChild = false;
            if (tmp_root != null && tmp_root != current)
            {
                using (IsChildOfMarker.Auto())
                    isChild = current.IsChildOf(tmp_root);
            }
            if (!isChild)
                return;

            Matrix4x4 matrix;
            using (CalcMatrixMarker.Auto())
                matrix = Utility.CalcMatrix(tmp_root, current);
            using (TransformMarker.Auto())
                meshData.TransformVertex(matrix);
        }
    }
}
