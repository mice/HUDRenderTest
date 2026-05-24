using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.UI
{
    public static class VertexHelperUtils
    {
        public static (int, int) FillData2(this VertexHelper vertexHelper, ref Vector3[] vertList, ref Color32[] colors, ref Vector4[] uvs, ref int[] triangles, int textureIndex, int flags)
        {
            throw new System.NotImplementedException();
        }

        public static void FillData3(this VertexHelper vertexHelper, ref Vector3[] vertList, ref Color32[] colors, ref Vector4[] uvs, ref int[] triangles, int vertexOffset, int indicesOffset, int textureIndex, int flags)
        {
            vertexHelper.m_Positions.Clear();
            throw new System.NotImplementedException();
        }
    }
}
