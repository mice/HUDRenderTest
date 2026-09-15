// Common interface for executing something
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Jobs;
using UnityEngine;

public interface IJobTask
{
    void Execute();
}

public class ManagedCodeInJob : MonoBehaviour
{
    private readonly List<GCHandle> gcHandles = new List<GCHandle>();
    private readonly List<JobHandle> jobHandles = new List<JobHandle>();

    public JobHandle ScheduleTask<T>(T task) where T: IJobTask
    {
        GCHandle gcHandle = GCHandle.Alloc(task);
        this.gcHandles.Add(gcHandle); // We remember this so we can free it later

        Job job = new Job()
        {
            handle = gcHandle
        };

        // We remember the JobHandle so we can complete it later
        var jobHandle = job.Schedule();
        this.jobHandles.Add(jobHandle);
        return jobHandle;
    }

    private void LateUpdate()
    {
        // Free and complete the scheduled jobs
        for (int i = 0; i < this.jobHandles.Count; ++i)
        {
            this.jobHandles[i].Complete();
            this.gcHandles[i].Free();
        }

        this.jobHandles.Clear();
        this.gcHandles.Clear();
    }

    private struct Job : IJob
    {
        public GCHandle handle;

        public void Execute()
        {
            IJobTask task = (IJobTask)handle.Target;
            task.Execute();
        }
    }
}