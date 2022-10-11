using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine;

///
/// 为了测试使用,因为引入了TotalCount.实际运行不是这样的..
/// 只是方便测试
////
namespace UIData
{
    public struct MergeVertexJob : IJobTask
    {
        public UIMeshData[] arr;
        public int MeshCount;

        [ReadOnly]
        public NativeArray<Vector3> pos;
        [ReadOnly]
        public NativeArray<Vector3> result_pos;
        /**
         可以优化的方式
        先计算出可能的分批,然后分批计算合并结果.
         */
        public unsafe void Execute()
        {
            int totalCount = pos.Length;
            int index = 0;
            void* pos_buffer = pos.GetUnsafeReadOnlyPtr();
            void* result_pos_buffer = result_pos.GetUnsafePtr();


            for (int i = 0; i < totalCount; i++)
            {
                for (int j  = 0; j < MeshCount; j++)
                {
                    var item = arr[j];
                    int item_vert_count = item.VertexCount;
                    for (int k = 0; k < item_vert_count; k++)
                    {
                        UnsafeUtility.WriteArrayElement(result_pos_buffer, index, item.vertList[k] + UnsafeUtility.ReadArrayElement<Vector3>(pos_buffer, i));
                        index++;
                    }
                }
            }
        }
    }


    public struct MergeColorJob : IJobTask
    {
        public UIMeshData[] arr;
        public int MeshCount;
        public int totalCount;

        public NativeArray<Color32> result_colors;
        /**
         可以优化的方式
        先计算出可能的分批,然后分批计算合并结果.
         */
        public unsafe void Execute()
        {
            int index = 0;
            Color32 white = Color.white;
            void* result_colors_buffer = result_colors.GetUnsafePtr();


            for (int i = 0; i < totalCount; i++)
            {
                for (int j = 0; j < MeshCount; j++)
                {
                    var item = arr[j];
                    int item_vert_count = item.VertexCount;
                    for (int k = 0; k < item_vert_count; k++)
                    {
                        UnsafeUtility.WriteArrayElement(result_colors_buffer, index, white);
                        index++;
                    }
                }
            }
        }
    }


    public struct MergeUVJob : IJobTask
    {
        public UIMeshData[] arr;
        public int MeshCount;
        public int totalCount;

        [ReadOnly]
        public NativeArray<Vector4> result_uv;

        public unsafe void Execute()
        {
            int index = 0;
            void* result_uv_buffer = result_uv.GetUnsafePtr();


            for (int i = 0; i < totalCount; i++)
            {
                for (int j = 0; j < MeshCount; j++)
                {
                    var item = arr[j];
                    int item_vert_count = item.VertexCount;
                    UnsafeFastCopy.CopyVec4(item.uvs, (Vector4*)result_uv_buffer, 0, index, item_vert_count);
                    index += item_vert_count;
                }
            }
        }
    }

    public struct MergeIndicsJob : IJobTask
    {
        public UIMeshData[] arr;
        public int MeshCount;
        public int totalCount;
        [ReadOnly]
        public NativeArray<int> result_triangle;
        [WriteOnly]
        public NativeArray<int> result_count;

        public unsafe void Execute()
        {
            int tIndics = 0;
            int offset = 0;
            int vertCountTotal = 0;
            int indexCountTotal = 0;
            void* result_triangle_buffer = result_triangle.GetUnsafePtr();

            for (int i = 0; i < totalCount; i++)
            {
                for (int j = 0; j < MeshCount; j++)
                {
                    var item = arr[j];
                    int item_vert_count = item.VertexCount;
                    var indicsCount = item.IndicsCount;
                    for (int k = 0; k < indicsCount; k++)
                    {
                        UnsafeUtility.WriteArrayElement(result_triangle_buffer, tIndics++, offset + item.triangles[k]);
                    }

                    offset += item_vert_count;
                    vertCountTotal += item_vert_count;
                    indexCountTotal += indicsCount;
                }
            }

            result_count[0] = vertCountTotal;
            result_count[1] = indexCountTotal;
        }
    }
}