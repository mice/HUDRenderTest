using Stella3D;
using System.Collections;
using System.Collections.Generic;
using UIData;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

public class TestUIPrefabHolderMetric : MonoBehaviour
{
    private List<DataPrefaHolder> holders;
    private UIMeshData[] meshArray;
    private int UIMeshCount = 0;
    public Font font;
    public Camera ui_Camera;
    public Transform ui_root;
    public ManagedCodeInJob jobTaskMgr;

    public UIPrefabOwner[] owners;


    public Sprite texture_1;
    public Sprite texture_2;

    public int count;
    public bool changePos;
    public bool useJob;

    private Mesh combine_mesh;
    private Material comb_Material;

    private UIPrefabManager uiPrefabManager = UIPrefabManager.Instance;
    private List<Vector3> vertBuff = new List<Vector3>();
    private List<Vector4> uvs = new List<Vector4>();
    private List<Color32> colors = new List<Color32>();
    private List<int>  triangles = new List<int>();

    private NativeArray<Vector3> posList;

    NativeArray<Vector3> result_pos;
    NativeArray<Vector4> result_uv;
    SharedArray<int> result_triangle;
    NativeArray<Color32> result_colors;


    NativeArray<int> result_count;

    private Queue<(MergeXVertexJob, MergeXColorJob, MergeXUVJob, MergeXIndicsJob, JobHandle, JobHandle, JobHandle, JobHandle)> jobs = 
    new Queue<(MergeXVertexJob, MergeXColorJob, MergeXUVJob, MergeXIndicsJob, JobHandle, JobHandle, JobHandle, JobHandle)>();


    public Button btn_test;
    public Toggle tgl_changePos;
    public InputField input_count;

    private bool SingleTest = false;

    [Button("ReCreate")]
    public string _X;


    private void Awake()
    {
        if (btn_test != null)
        {
            btn_test.onClick.AddListener(() => ReCreate());
        }

        if (tgl_changePos != null)
        {
            tgl_changePos.onValueChanged.AddListener((b) => {
                this.changePos = b;
            });
        }
    }

    public void ReCreate()
    {
        if (!UnityEngine.Application.isPlaying)
        {
            UnityEngine.Debug.LogError("Only Run In PlayModel");
            return;
        }
        if(int.TryParse(input_count.text,out var _t)&& _t>0)
        {
            count = _t;
        }
        holders = holders?? new List<DataPrefaHolder>(count);
        meshArray = meshArray ?? new UIMeshData[1024];
        holders.Clear();
        for (int i = 0; i < count; i++)
        {
            var holder = new DataPrefaHolder();
            holder.SetTarget(owners[UnityEngine.Random.Range(0,2)]);
            holder.SetPosition(new Vector3(UnityEngine.Random.Range(0,800)-400,UnityEngine.Random.Range(0,700)-350,0));
            uiPrefabManager.Register(holder);
            holders.Add(holder);
        }

        var index = 0;
        foreach(var holder in holders)
        {
            holder.SetText(2, "NiHao" + UnityEngine.Random.Range(1, 10));
           
            uiPrefabManager.Generate(holder);
            if (holder.Target == owners[1])
            {
                holder.SetTextureIndex(2, 2);
            }
            for (int i = 0; i < holder.uIMeshDatas.Length; i++)
            {
                meshArray[index++] = holder.uIMeshDatas[i];
            }
        }
        UIMeshCount = index;

        combine_mesh = combine_mesh ?? new Mesh();

        int totalVertCount = 1024 * 80;
        int totalTriangleCount = 65420;
        if (!posList.IsCreated)
        {
            posList = new NativeArray<Vector3>(1024,Allocator.Persistent);
      
            result_pos = new NativeArray<Vector3>(totalVertCount, Allocator.Persistent);
            result_uv = new NativeArray<Vector4>(totalVertCount, Allocator.Persistent);
            result_triangle = new SharedArray<int>(totalTriangleCount);
            result_colors = new NativeArray<Color32>(totalVertCount, Allocator.Persistent);
            result_count = new NativeArray<int>(2, Allocator.Persistent);
        }

        comb_Material = new Material(Shader.Find("Hidden/UIE-AtlasBlit"));
        comb_Material.SetTexture("_MainTex0", font.material.mainTexture);
        comb_Material.SetTexture("_MainTex1", texture_1.texture);
        comb_Material.SetTexture("_MainTex2", texture_2.texture);
        comb_Material.renderQueue = 3000;
        if (useJob)
        {
            CreateWholeTask();
        }
        else
        {
            RebuildMesh();
        }
        
    }

    /// <summary>
    /// 暴力List
    /// </summary>
    private void RebuildMesh()
    {
        vertBuff.Clear();uvs.Clear(); colors.Clear(); triangles.Clear();
        foreach (var holder in holders)
        {
            holder.Fill(vertBuff, uvs, colors, triangles, holder.Position);
        }
        combine_mesh.Clear();
        combine_mesh.SetVertices(vertBuff);
        combine_mesh.SetUVs(0, uvs);
        combine_mesh.SetColors(colors);
        combine_mesh.SetTriangles(triangles, 0);
        combine_mesh.RecalculateBounds();
    }

    private void Update()
    {
        if (changePos)
        {
            if (useJob)
            {
                CreateMergeTask();
            }
            else
            {
                foreach (var holder in holders)
                {
                    holder.SetPosition(new Vector3(UnityEngine.Random.Range(0, 1200) - 600, UnityEngine.Random.Range(0, 700) - 350,0));
                }
                RebuildMesh();
            }
        }
    }

    private unsafe void CreatePosList()
    {
        var _totalCount = count;
        void* pos_buffer = posList.GetUnsafeReadOnlyPtr();
        int index = 0;
        for (int i = 0; i < _totalCount; i++)
        {
            var count = holders[i].uIMeshDatas.Length;
            var vec = new Vector3(UnityEngine.Random.Range(0, 1200) - 600, UnityEngine.Random.Range(0, 700) - 350, 0);
            for (int j = 0; j < count; j++)
            {
                UnsafeUtility.WriteArrayElement<Vector3>(pos_buffer,index++,vec);
            }
        }
    }

    private void CreateWholeTask()
    {
        CreatePosList();
        var tmp_whole_job = new BigXMergeJob()
        {
            arr = meshArray,
            UIMeshCount = UIMeshCount,
            pos = posList,
            result_pos = result_pos,
            result_colors = result_colors,
            result_uv = result_uv,
            result_triangle = result_triangle,
            result_count = result_count,
        };
       tmp_whole_job.Execute();
       ConsumeMergeJob(tmp_whole_job);
    }

    private void CreateMergeTask()
    {
        CreatePosList();
        var tmp_vertex_job = new MergeXVertexJob()
        {
            arr = meshArray,
            UIMeshCount = UIMeshCount,
            pos = posList,
            result_pos = result_pos,
        };

        var tmp_color_job = new MergeXColorJob()
        {
            arr = meshArray,
            UIMeshCount = UIMeshCount,
            result_colors = result_colors,
        };

        var tmp_uv_job = new MergeXUVJob()
        {
            arr = meshArray,
            UIMeshCount = UIMeshCount,
            result_uv = result_uv,
        };

        var tmp_indics_job = new MergeXIndicsJob()
        {
            arr = meshArray,
            UIMeshCount = UIMeshCount,
            result_triangle = result_triangle,
            result_count = result_count,
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
            var jobHandle0 = jobTaskMgr.ScheduleTask(tmp_vertex_job);
            var jobHandle1 = jobTaskMgr.ScheduleTask(tmp_color_job);
            var jobHandle2 = jobTaskMgr.ScheduleTask(tmp_uv_job);
            var jobHandle3 = jobTaskMgr.ScheduleTask(tmp_indics_job);
            jobs.Enqueue((tmp_vertex_job, tmp_color_job, tmp_uv_job, tmp_indics_job, jobHandle0,jobHandle1,jobHandle2,jobHandle3));
        }
    }

    private void ConsumeMergeJob(BigXMergeJob indicsJob)
    {
        var vertCount = indicsJob.result_count[0];
        var triangleCount = indicsJob.result_count[1];
        //UnityEngine.Debug.LogError($"$Result VertexCount::{vertCount},TriangeCount:{triangleCount}::,");
        combine_mesh.Clear();
        combine_mesh.SetVertices(result_pos, 0, vertCount);
        combine_mesh.SetUVs(0, result_uv, 0, vertCount);
        combine_mesh.SetColors(result_colors, 0, vertCount);
        combine_mesh.SetTriangles(result_triangle, 0, triangleCount, 0,false);
        combine_mesh.RecalculateBounds();
    }

    private void ConsumeMergeJob(MergeXVertexJob tmp_job, MergeXUVJob uvJob, MergeXIndicsJob indicsJob)
    {
        var vertCount = indicsJob.result_count[0];
        var triangleCount = indicsJob.result_count[1];
        //UnityEngine.Debug.LogError($"$Result VertexCount::{vertCount},TriangeCount:{triangleCount}::,");
        combine_mesh.Clear();
        combine_mesh.SetVertices(result_pos, 0, vertCount);
        combine_mesh.SetUVs(0, result_uv, 0, vertCount);
        combine_mesh.SetColors(result_colors, 0, vertCount);
        combine_mesh.SetTriangles(result_triangle, 0, triangleCount, 0,false);
        combine_mesh.RecalculateBounds();
    }

    private void LateUpdate()
    {
        if (useJob)
        {
            if (jobs.Count > 0 )
            {
                (var vertex_job, var tmp_color_job, var uv_job, var indics_job,var jobHandle_0,var jobHandle_1,var jobHandle_2,var jobHandle_3) = jobs.Dequeue();
                if(!jobHandle_0.IsCompleted)
                    jobHandle_0.Complete();
                if(!jobHandle_1.IsCompleted)
                    jobHandle_1.Complete();
                if(!jobHandle_2.IsCompleted)
                    jobHandle_2.Complete();
                if(!jobHandle_3.IsCompleted)  
                    jobHandle_3.Complete();
                
                ConsumeMergeJob(vertex_job, uv_job, indics_job);
            }

        }
        if (combine_mesh != null)
        {
            var matix = ui_root.localToWorldMatrix;
            Graphics.DrawMesh(combine_mesh, matix, comb_Material, 5, ui_Camera);
        }
    }

    private void OnDestroy()
    {
        if (posList.IsCreated)
        {
            posList.Dispose();
        }
        if(result_pos.IsCreated)
        {
            result_pos.Dispose();
        }

        if (result_uv.IsCreated)
        {
            result_uv.Dispose();
        }

        if (result_triangle!=null)
        {
            result_triangle.Dispose();
            result_triangle = null;
        }

        if (result_colors.IsCreated)
        {
            result_colors.Dispose();
        }

        if (result_count.IsCreated)
        {
            result_count.Dispose();
        }
    }
}
