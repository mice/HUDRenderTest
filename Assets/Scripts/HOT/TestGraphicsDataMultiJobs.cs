using Stella3D;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UI;
using UIData;

//Unity.Collections.LowLevel.Unsafe
/// <summary>
/// 测试目标:
/// 从Image中获取Mesh数据,然后渲染出来.
/// </summary>
public class TestGraphicsDataMultiJobs : MonoBehaviour
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
    public bool SingleTest = false;
    public bool StartRun = false;
    private UIMeshData[] meshArray;
    private NativeArray<Vector3> posList;

    NativeArray<Vector3> result_pos;
    NativeArray<Vector4> result_uv;
    SharedArray<int> result_triangle;
    NativeArray<Color32> result_colors;

    private Queue<(MergeVertexJob,MergeColorJob,MergeUVJob,MergeIndicsJob, JobHandle)> jobs = new Queue<(MergeVertexJob, MergeColorJob,MergeUVJob, MergeIndicsJob ,JobHandle)>();

    [Button("ReCreate")]
    public string _X;
    public Button btn_test;
    public InputField txt_count;

    private void Start()
    {
        if(btn_test != null)
            btn_test.onClick.AddListener(ReCreate);
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

        meshArray = new UIMeshData[4];
        meshArray[0] = (img_meshData_1);
        meshArray[1] = (img_meshData_2);
        meshArray[2] = (txt_meshData_1);
        if (txt_count != null && int.TryParse(txt_count.text, out var _tmpCount))
        {
            _totalCount = _tmpCount;
        }

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
        if (SingleTest)
        {
            CreateMergeTask();
        }
        else
        {
            StartRun = true;
        }
    }

    private void Update()
    {
        if (StartRun)
        {
            CreateMergeTask();
        }
    }


    private void CreateMergeTask()
    {
        for (int i = 0; i < _totalCount; i++)
        {
            posList[i] = new Vector3(UnityEngine.Random.Range(0, 1200) - 600, UnityEngine.Random.Range(0, 700) - 350);
        }
        var tmp_vertex_job = new MergeVertexJob()
        {
            arr = meshArray,
            MeshCount = 3,
            pos = posList,
            result_pos = result_pos,
        };

        var tmp_color_job = new MergeColorJob()
        {
            arr = meshArray,
            MeshCount = 3,
            totalCount = _totalCount,
            result_colors = result_colors,
        };

        var tmp_uv_job = new MergeUVJob()
        {
            arr = meshArray,
            MeshCount = 3,
            totalCount = _totalCount,
            result_uv = result_uv,
        };

        var tmp_indics_job = new MergeIndicsJob()
        {
            arr = meshArray,
            MeshCount = 3,
            totalCount = _totalCount,
            result_triangle = result_triangle,
            result_count = new NativeArray<int>(2, Allocator.TempJob),
        };
      
        if (SingleTest)
        {
            tmp_vertex_job.Execute();
            tmp_color_job.Execute();
            tmp_uv_job.Execute();
            tmp_indics_job.Execute();
            ConsumeMergeJob(tmp_vertex_job, tmp_uv_job, tmp_indics_job);
        }
        else
        {
            jobTaskMgr.ScheduleTask(tmp_vertex_job);
            jobTaskMgr.ScheduleTask(tmp_color_job);
            jobTaskMgr.ScheduleTask(tmp_uv_job);
            var jobHandle = jobTaskMgr.ScheduleTask(tmp_indics_job);
            jobs.Enqueue((tmp_vertex_job, tmp_color_job,tmp_uv_job, tmp_indics_job, jobHandle));
        }
    }

    private void LateUpdate()
    {
        if (StartRun)
        {
            if (jobs.Count > 0 && jobs.Peek().Item5.IsCompleted)
            {
                (var vertex_job,var tmp_color_job,var uv_job,var indics_job,_) = jobs.Dequeue();
               ConsumeMergeJob(vertex_job,uv_job,indics_job);
            }

        }
        ShowSharedMesh();
    }

    private void ShowSharedMesh()
    {
        if (combine_mesh != null)
        {
            var matix = txt_1.transform.parent.localToWorldMatrix;
            Graphics.DrawMesh(combine_mesh, matix, comb_Material, 5, ui_Camera);
        }
    }

    private void ConsumeMergeJob(MergeVertexJob tmp_job,MergeUVJob uvJob,MergeIndicsJob indicsJob)
    {
        var vertCount = indicsJob.result_count[0];
        var triangleCount = indicsJob.result_count[1];
        //UnityEngine.Debug.LogError($"$Result VertexCount::{vertCount},TriangeCount:{triangleCount}::,");

        combine_mesh.SetVertices(result_pos, 0, vertCount);
        combine_mesh.SetUVs(0, result_uv, 0, vertCount);
        combine_mesh.SetColors(result_colors, 0, vertCount);
        combine_mesh.SetTriangles(result_triangle, 0, triangleCount, 0);
        combine_mesh.RecalculateBounds();
        indicsJob.result_count.Dispose();
    }

    private void OnDestroy()
    {
        DisposeNativeContainer(ref posList);
        DisposeNativeContainer(ref result_pos);
        DisposeNativeContainer(ref result_uv);
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
