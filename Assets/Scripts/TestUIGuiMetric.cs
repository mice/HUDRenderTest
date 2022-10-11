using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.U2D;
using UnityEngine.UI;

public class TestUIGuiMetric : MonoBehaviour
{
    private List<UIPrefabOwner> holders;
    public Camera ui_Camera;
    public Transform ui_root;

    public UIPrefabOwner[] owners;
    public TransformAccessArray tfmArray;


    public int count;
    public bool changePos;
    public bool setActiveTest;
    public bool changeTextTest;

    public bool useJob;

    public Button btn_test;
    public Toggle tgl_changePos;
    public Toggle tgl_setActive;
    public Toggle tgl_changeText;
    public Toggle tgl_job;

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

        if (tgl_setActive != null)
        {
            tgl_setActive.onValueChanged.AddListener((b) => {
                this.setActiveTest = b;
            });
        }

        if (tgl_changeText != null)
        {
            tgl_changeText.onValueChanged.AddListener((b) => {
                this.changeTextTest = b;
            });
        }

        if (tgl_job != null)
        {
            tgl_job.onValueChanged.AddListener((b) => {
                this.useJob = b;
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
        holders = holders?? new List<UIPrefabOwner>(count);
        holders.Clear();
        tfmArray = new TransformAccessArray(count);
        for (int i = 0; i < count; i++)
        {
            var holder = GameObject.Instantiate(owners[UnityEngine.Random.Range(0, 2)],ui_root);
            holder.transform.localPosition = (new Vector3(UnityEngine.Random.Range(0,800)-400,UnityEngine.Random.Range(0,700)-350,0));
            holders.Add(holder);
            tfmArray.Add(holder.transform);
        }

        //foreach(var holder in holders)
        //{
        //    holder.SetText(2, "NiHao" + UnityEngine.Random.Range(1, 10));
           
        //    if (holder.target == owners[1])
        //    {
        //        holder.SetTextureIndex(2, 2);
        //    }
        //}
     
    }

    [BurstCompile]
    public struct SetPositionJob : IJobParallelForTransform
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]
        public NativeArray<Vector3> localPostions;
        public void Execute(int index, TransformAccess transform)
        {
            transform.localPosition = localPostions[index];
        }
    }

    private UIPrefabOwner last_owner;
    private void Update()
    {
        if (changePos)
        {
            if (useJob)
            {
                var jobHandle = new JobHandle();

                var tmpPos = new NativeArray<Vector3>(count, Allocator.TempJob);
                for (int i = 0; i < count; i++)
                {
                    tmpPos[i] = (new Vector3(UnityEngine.Random.Range(0, 800) - 400, UnityEngine.Random.Range(0, 700) - 350, 0));
                }
                var job = new SetPositionJob()
                {
                    localPostions = tmpPos,
                };
                job.Schedule(tfmArray, jobHandle);
            }
            else
            {
                foreach (var holder in holders)
                {
                    holder.transform.localPosition = (new Vector3(UnityEngine.Random.Range(0, 800) - 400, UnityEngine.Random.Range(0, 700) - 350, 0));
                }
            }
           
            if (last_owner != null)
            {
                last_owner.gameObject.SetActive(true);
            }
            var tmp_holder = holders[UnityEngine.Random.Range(0, holders.Count)];
            if (changeTextTest)
            {
                var text = tmp_holder.targets[2].GetComponent<Text>();
                if (text != null)
                {
                    text.text = "NiHao" + UnityEngine.Random.Range(1, 10);
                }
            }
            if (setActiveTest)
            {
                tmp_holder.gameObject.SetActive(Random.Range(0, 2) == 1);
            }
            last_owner = tmp_holder;
        }
    }

    private void LateUpdate()
    {
        
    }

    private void OnDestroy()
    {
        if (tfmArray.isCreated)
        {
            tfmArray.Dispose();
        }
    }
}
