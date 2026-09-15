using Stella3D;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UI;

//Unity.Collections.LowLevel.Unsafe
/// <summary>
/// 测试目标:
/// 从Image中获取Mesh数据,然后渲染出来.
/// </summary>
public class TestGraphicsDataMetric : MonoBehaviour
{
    public Font font;
    public Camera ui_Camera;
    public ManagedCodeInJob jobTaskMgr;

    //九宫格的图片
    public UIImage img_ui_1;
    //正常的图片Simple
    public UIImage img_ui_2;
    //Text文本.
    public UIText txt_1;

    public int _totalCount = 1;

    private Mesh combine_mesh;
    private Material comb_Material;

    public bool StartRun = false;
    private List<UIMeshData> meshArray;
    private NativeArray<Vector3> posList;

    NativeArray<Vector3> result_pos;
    NativeArray<Vector4> result_uv;
    SharedArray<int> result_triangle;
    NativeArray<Color32> result_colors;

    private Queue<(MergeJob,JobHandle)> jobs = new Queue<(MergeJob, JobHandle)>();

    [Button("ReCreate")]
    public string _X;
  

    private void __CreateMeshData(UIMeshData img_meshData_1, UIMeshData img_meshData_2, UIMeshData txt_meshData_1)
    {
        var vertBuff = new List<Vector3>();
        var uvs = new List<Vector4>();
        var colors = new List<Color32>();
        var triangles = new List<int>();
        int offset = 3;
        int totalCount = offset + _totalCount;
        var vec = new Vector3[totalCount];

        vec[0] = new Vector3(-400, 0);
        vec[1] = new Vector3(-200, 0);
        vec[2] = new Vector3(100, 0);
        for (int i = offset; i < totalCount; i++)
        {
            vec[i] = new Vector3(UnityEngine.Random.Range(0, 1200) - 600, UnityEngine.Random.Range(0, 700) - 350);
        }

        img_meshData_1.FillToDrawData(vertBuff, uvs, colors, triangles, vec[0]);
        img_meshData_2.FillToDrawData(vertBuff, uvs, colors, triangles, vec[1]);
        txt_meshData_1.FillToDrawData(vertBuff, uvs, colors, triangles, vec[2]);
        for (int i = offset; i < totalCount; i++)
        {
            img_meshData_2.FillToDrawData(vertBuff, uvs, colors, triangles, vec[i]);
            img_meshData_1.FillToDrawData(vertBuff, uvs, colors, triangles, vec[i]);
            txt_meshData_1.FillToDrawData(vertBuff, uvs, colors, triangles, vec[i]);
        }

        combine_mesh.SetVertices(vertBuff);
        combine_mesh.SetUVs(0, uvs);
        combine_mesh.SetColors(colors);
        combine_mesh.SetTriangles(triangles, 0);
        combine_mesh.RecalculateBounds();
    }


    public void NativeCreate()
    {
        var img_meshData_1 = new UIMeshData();
        img_meshData_1.TextureIndex = 1;
        img_ui_1.DoGenerate(img_meshData_1);

        var img_meshData_2 = new UIMeshData();
        img_meshData_2.TextureIndex = 1;
        img_ui_2.DoGenerate(img_meshData_2);

        var txt_meshData_1 = new UIMeshData();
        txt_meshData_1.TextureIndex = 0;
        txt_1.DoGenerate(txt_meshData_1);
        __CreateMeshData(img_meshData_1,img_meshData_2,txt_meshData_1);
    }

    public void ReCreate()
    {
        if (!Application.isPlaying)
        {
            UnityEngine.Debug.LogError("Only Run In PlayModel");
            return;
        }
        var img_meshData_1 = new UIMeshData();
        img_meshData_1.TextureIndex = 1;
        img_ui_1.DoGenerate(img_meshData_1);

        var img_meshData_2= new UIMeshData();
        img_meshData_2.TextureIndex = 1;
        img_ui_2.DoGenerate(img_meshData_2);

        var txt_meshData_1 = new UIMeshData();
        txt_meshData_1.TextureIndex = 0;
        txt_1.DoGenerate(txt_meshData_1);

        meshArray = new List<UIMeshData>(3);
        meshArray.Add(img_meshData_1);
        meshArray.Add(img_meshData_2);
        meshArray.Add(txt_meshData_1);

        if (!posList.IsCreated)
        {
            posList = new NativeArray<Vector3>(_totalCount, Allocator.Persistent);

            result_pos = new NativeArray<Vector3>(40960, Allocator.Persistent);
            result_uv = new NativeArray<Vector4>(40960, Allocator.Persistent);
            result_triangle = new SharedArray<int>(81720);// new NativeArray<int>(81720, Allocator.Persistent);
            result_colors = new NativeArray<Color32>(40960, Allocator.Persistent);
        }
       
        combine_mesh = combine_mesh ?? new Mesh();

        comb_Material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        comb_Material.SetTexture("_MainTex0", font.material.mainTexture);
        if(img_ui_1!=null  && img_ui_1.sprite != null)
        {
            comb_Material.SetTexture("_MainTex1", img_ui_1.sprite.texture);
        }
        comb_Material.renderQueue = 3000;

        StartRun = true;
    }

    private void Update()
    {
        if (StartRun)
        {
            CreateMergeTask();
        }
    }

    public struct MergeJob : IJobTask
    {
        public List<UIMeshData> arr;
       
        [ReadOnly]
        public NativeArray<Vector3> pos;
        [ReadOnly]
        public NativeArray<Vector3> result_pos;
        [ReadOnly]
        public NativeArray<Vector4> result_uv;
        [ReadOnly]
        public NativeArray<int> result_triangle;
        [WriteOnly]
        public NativeArray<int> result_count;

        public NativeArray<Color32> result_colors;
        /**
         可以优化的方式
        先计算出可能的分批,然后分批计算合并结果.
         */
        public unsafe void Execute()
        {
            int totalCount = pos.Length;
            int index = 0;
            int tIndics = 0;
            int offset = 0;
            Color32 white = Color.white;
            int vertCountTotal = 0;
            int indexCountTotal = 0;
            void* pos_buffer = pos.GetUnsafeReadOnlyPtr();
            void* result_pos_buffer = result_pos.GetUnsafePtr();
            void* result_colors_buffer = result_colors.GetUnsafePtr();
            void* result_uv_buffer = result_uv.GetUnsafePtr();
            void* result_triangle_buffer = result_triangle.GetUnsafePtr();


            for (int i = 0; i < totalCount; i++)
            {
                foreach (var item in arr)
                {
                    int item_vert_count = item.mesh.VertexCount;
                    UnsafeFastCopy.CopyVec4(item.uvs, (Vector4*)result_uv_buffer, 0, index, item_vert_count);
                    for (int j = 0; j < item_vert_count; j++)
                    {
                        UnsafeUtility.WriteArrayElement(result_pos_buffer, index, item.vertList[j] + UnsafeUtility.ReadArrayElement<Vector3>(pos_buffer,i));
                        UnsafeUtility.WriteArrayElement(result_colors_buffer, index, white);
                        index++;
                    }

                    var indicsCount = item.mesh.IndicesCount;
                    for (int j = 0; j < indicsCount; j++)
                    {
                        UnsafeUtility.WriteArrayElement(result_triangle_buffer, tIndics++, offset + item.triangles[j]);
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

    private void CreateMergeTask()
    {
        for (int i = 0; i < _totalCount; i++)
        {
            posList[i] = new Vector3(UnityEngine.Random.Range(0, 1200) - 600, UnityEngine.Random.Range(0, 700) - 350);
        }
        var tmp_job = new MergeJob()
        {
            arr = meshArray,
            pos = posList,
            result_pos = result_pos,
            result_uv = result_uv,
            result_triangle = result_triangle,
            result_colors = result_colors,
            result_count =  new NativeArray<int>(2,Allocator.TempJob)
        };
        tmp_job.Execute(); 
        ConsumeMergeJob(tmp_job);
        //var jobHandle = jobTaskMgr.ScheduleTask(tmp_job);
        //jobs.Enqueue((tmp_job,jobHandle));
    }

    private void LateUpdate()
    {
        if (StartRun)
        {
            //mergehandle.Complete();
            if (jobs.Count > 0 && jobs.Peek().Item2.IsCompleted)
            {
                var tmp_job = jobs.Dequeue().Item1;
                ConsumeMergeJob(tmp_job);
            }

        }
        if (combine_mesh != null)
        {
            var matix = txt_1.transform.parent.localToWorldMatrix;
            Graphics.DrawMesh(combine_mesh, matix, comb_Material, 5, ui_Camera);
        }
    }

    private void ConsumeMergeJob(MergeJob tmp_job)
    {
        var vertCount = tmp_job.result_count[0];
        var triangleCount = tmp_job.result_count[1];
        //UnityEngine.Debug.LogError($"$Result VertexCount::{vertCount},TriangeCount:{triangleCount}::,");

        combine_mesh.SetVertices(result_pos, 0, vertCount);
        combine_mesh.SetUVs(0, result_uv, 0, vertCount);
        combine_mesh.SetColors(result_colors, 0, vertCount);
        combine_mesh.SetTriangles(result_triangle, 0, triangleCount, 0);
        combine_mesh.RecalculateBounds();
        tmp_job.result_count.Dispose();
    }

    private void OnDestroy()
    {
        DisposeNativeContainer(ref posList);
        DisposeNativeContainer(ref result_pos);
        DisposeNativeContainer(ref result_uv);
        //DisposeNativeContainer(ref result_triangle);
        DisposeNativeContainer(ref result_colors);
        if (result_triangle != null)
        {
            result_triangle.Dispose();
            result_triangle = null;
        }
       
    }

    private void DisposeNativeContainer<T>(ref NativeArray<T> container) where T:struct
    {
        if (container.IsCreated)
        {
            container.Dispose();
        }
    }
}
