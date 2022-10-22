
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utility
{
    // Start is called before the first frame update
    public static Matrix4x4 CalcMatrix(Transform root, Transform subNode)
    {
        var tmpNode = subNode;
        var mtx = Matrix4x4.TRS(tmpNode.localPosition, tmpNode.localRotation, tmpNode.localScale);
        while (tmpNode.parent != root)
        {
            tmpNode = tmpNode.parent;
            mtx = Matrix4x4.TRS(tmpNode.localPosition, tmpNode.localRotation, tmpNode.localScale) * mtx;
        }
        return mtx;
    }
}
