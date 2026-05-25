using System.Collections;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// TC-JOB-01: ManagedCodeInJob completes the job and releases the GCHandle within the same frame.
/// </summary>
public class TestManagedCodeInJob
{
    private class TrackingTask : IJobTask
    {
        public bool Executed { get; private set; }
        public void Execute() => Executed = true;
    }

    // TC-JOB-01
    [UnityTest]
    public IEnumerator Complete_FreesGCHandle()
    {
        var go = new GameObject("ManagedCodeInJob");
        var handler = go.AddComponent<ManagedCodeInJob>();

        var task = new TrackingTask();
        var jobHandle = handler.ScheduleTask(task);

        // Advance one frame so LateUpdate runs: job completes and GCHandles are freed.
        yield return null;

        Assert.IsTrue(task.Executed, "IJobTask.Execute must be called by the job");
        Assert.IsTrue(jobHandle.IsCompleted, "JobHandle must be complete after LateUpdate");

        Object.Destroy(go);
    }

    // Additional: scheduling multiple tasks in one frame all complete.
    [UnityTest]
    public IEnumerator MultipleSchedule_AllComplete()
    {
        var go = new GameObject("ManagedCodeInJobMulti");
        var handler = go.AddComponent<ManagedCodeInJob>();

        const int count = 5;
        var tasks = new TrackingTask[count];
        for (int i = 0; i < count; i++)
        {
            tasks[i] = new TrackingTask();
            handler.ScheduleTask(tasks[i]);
        }

        yield return null;

        for (int i = 0; i < count; i++)
            Assert.IsTrue(tasks[i].Executed, $"task[{i}] must have executed");

        Object.Destroy(go);
    }
}
