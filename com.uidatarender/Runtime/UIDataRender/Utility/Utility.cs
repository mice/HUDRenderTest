
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class Utility
{
    // Start is called before the first frame update
    public static Matrix4x4 CalcMatrix(Transform root, Transform subNode)
    {
        return root.worldToLocalMatrix * subNode.localToWorldMatrix;
    }

}
