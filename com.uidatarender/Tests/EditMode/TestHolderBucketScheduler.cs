using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

public class TestHolderBucketScheduler
{
    private struct State
    {
        public float Width;
        public Color32 Color;
        public Vector3 Position;
        public int Slot;
    }

    private sealed class Holder : IUIPrefabHolder, IDisposable
    {
        public readonly IUIData Data = new UIMeshDataX();
        private readonly IUIData[] _meshes;
        public Holder() { _meshes = new[] { Data }; }
        public UIPrefabOwner Target => null;
        public Vector3 Position => Vector3.zero;
        public UIPrefabRegistration wrapper => null;
        public IList<IUIData> UIMeshDatas => _meshes;
        public void SetTarget(UIPrefabOwner target) { }
        public void SetWrapper(UIPrefabRegistration registration) { }
        public void BuildMesh(IUIDrawTarget[] draws) { }
        public void Fill(List<Vector3> v, List<Vector4> uv, List<Color32> c, List<int> t, Vector3 position)
            => Data.FillToDrawData(v, uv, c, t, position);
        public void Fill(List<int> t, Vector3 position) => Data.FillToTriangleData(t, position);
        public void Dispose() => Data.Dispose();
    }

    private sealed class Applier : IHolderStateApplier<State>
    {
        public int Calls, Begins, Ends;
        public int FailAt = -1;
        public Action Callback;
        public State SharedTemplate;
        public void BeginBatch() { Begins++; }
        public Vector3 Apply(IUIPrefabHolder holder, in State state)
        {
            Calls++;
            Callback?.Invoke();
            if (Calls == FailAt) throw new InvalidOperationException("injected generation failure");
            SharedTemplate = state;
            var mesh = holder.UIMeshDatas[0];
            mesh.TextureIndex = SharedTemplate.Slot;
            mesh.WriteSimpleQuad(new Vector4(0, 0, SharedTemplate.Width, 1), new Vector4(0, 0, 1, 1), SharedTemplate.Color);
            return SharedTemplate.Position;
        }
        public void EndBatch() { Ends++; }
    }

    private readonly List<Holder> _holders = new List<Holder>();
    private readonly List<HolderBucketScheduler<State>> _schedulers = new List<HolderBucketScheduler<State>>();
    private int _resourceVersion;
    private bool _failBinding;
    private static State Value(float width, int slot = 0) => new State { Width = width, Color = new Color32(10, 20, 30, 255), Position = new Vector3(width, 0, 0), Slot = slot };

    private HolderBucketScheduler<State> Create(int count, out Applier applier, HolderBucketStrategy strategy = HolderBucketStrategy.Rotating)
    {
        applier = new Applier();
        var scheduler = new HolderBucketScheduler<State>(1, applier,
            () => new Material(Shader.Find("Sprites/Default")), (_, _) =>
            { if (_failBinding) throw new InvalidOperationException("injected binding failure"); }, () => _resourceVersion, strategy);
        _schedulers.Add(scheduler);
        var holders = new IUIPrefabHolder[count];
        var states = new State[count];
        for (int i = 0; i < count; i++) { var holder = new Holder(); _holders.Add(holder); holders[i] = holder; states[i] = Value(1); }
        Assert.AreEqual(HolderBucketStatus.Ready, scheduler.ReplaceMembers(holders, states, 0).Status);
        return scheduler;
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var scheduler in _schedulers) scheduler.Dispose();
        foreach (var holder in _holders) holder.Dispose();
        _schedulers.Clear(); _holders.Clear(); _resourceVersion = 0; _failBinding = false;
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_011.md
    [Test]
    [Category("UT_BATCH_011")]
    public void Membership_UsesStableBoundaries_AndRejectsStaleForeignOrDuplicateMembers()
    {
        foreach (int count in new[] { 1, 128, 129, 256 })
        {
            var scheduler = Create(count, out _);
            Assert.AreEqual((count + 127) / 128, scheduler.BucketCount);
            var old = scheduler.GetHandle(0);
            var member = new Holder(); _holders.Add(member);
            Assert.Throws<ArgumentException>(() => scheduler.ReplaceMembers(new IUIPrefabHolder[] { member, member }, new State[2], 1));
            Assert.Throws<ArgumentException>(() => scheduler.ReplaceMembers(new IUIPrefabHolder[257], new State[257], 1));
            Assert.AreEqual(HolderBucketStatus.Ready, scheduler.ReplaceMembers(new[] { member }, new[] { Value(2) }, 1).Status);
            Assert.Throws<ArgumentException>(() => scheduler.Submit(old, Value(3), 2));
            Assert.Throws<ArgumentException>(() => scheduler.Submit(default, Value(3), 2));
        }
        Assert.Throws<ArgumentException>(() => _schedulers[0].GetProgress(_schedulers[1].GetHandle(0)));
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_012.md
    [Test]
    [Category("UT_BATCH_012")]
    public void Rotation_LatestSnapshotKeepsFirstPendingAge_AndDoesNotStarveSecondBucket()
    {
        var s = Create(256, out var applier);
        var a = s.GetHandle(0); var b = s.GetHandle(128);
        var oldB = s.GetPublishedMesh(1, 0);
        var oldVertices = oldB.vertices;
        int calls = applier.Calls;
        s.Submit(a, Value(2), 1); s.Submit(b, Value(3), 1);
        Assert.AreEqual(calls, applier.Calls, "Submit must not generate.");
        Assert.AreEqual(1, s.Tick(1).CompletedHolders);
        Assert.AreSame(oldB, s.GetPublishedMesh(1, 0));
        CollectionAssert.AreEqual(oldVertices, oldB.vertices);
        s.Submit(a, Value(4), 2); s.Submit(b, Value(5), 2);
        applier.SharedTemplate = Value(999);
        var result = s.Tick(2);
        Assert.AreEqual(1, result.RefreshedBuckets);
        Assert.AreEqual(1, result.MaxCompletedFirstPendingAge);
        Assert.AreEqual(0, result.MaxCompletedLatestValueAge);
        Assert.AreEqual(2, s.GetProgress(b).CompletedVersion);
        Assert.IsTrue(s.GetProgress(a).Pending);
        Assert.AreEqual(5, s.GetPublishedMesh(1, 0).vertices[0].x);
        Assert.AreNotSame(oldB, s.GetPublishedMesh(1, 0));
        s.Tick(3);
        Assert.IsFalse(s.GetProgress(a).Pending);
        Assert.AreEqual(applier.Begins, applier.Ends);
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_013.md
    [Test]
    [Category("UT_BATCH_013")]
    public void Tick_RejectsReentrancyAndBackwardsFrames_AndDefersPostCutoffRequests()
    {
        var s = Create(1, out var applier); var h = s.GetHandle(0);
        s.Tick(1); s.Submit(h, Value(2), 1);
        Assert.AreEqual(0, s.Tick(1).CompletedHolders);
        Assert.IsTrue(s.GetProgress(h).Pending);
        applier.Callback = () => s.Submit(h, Value(3), 2);
        Assert.AreEqual(HolderBucketStatus.Faulted, s.Tick(2).Status);
        Assert.IsInstanceOf<InvalidOperationException>(s.LastError);
        Assert.AreEqual(0, s.GetProgress(h).CompletedVersion);
        applier.Callback = null;
        Assert.AreEqual(HolderBucketStatus.Ready, s.FlushAll(HolderFlushReason.Recovery, 2).Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Tick(1));
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_014.md
    [Test]
    [Category("UT_BATCH_014")]
    public void FailedGeneration_PreservesFrontAndVersions_UntilFullRecovery()
    {
        var s = Create(128, out var applier);
        Mesh front = s.GetPublishedMesh(0, 0); var vertices = front.vertices;
        s.Submit(s.GetHandle(0), Value(8), 1); s.Submit(s.GetHandle(1), Value(9), 1);
        applier.FailAt = applier.Calls + 2;
        Assert.AreEqual(HolderBucketStatus.Faulted, s.Tick(1).Status);
        Assert.IsTrue(s.CanDraw);
        Assert.AreSame(front, s.GetPublishedMesh(0, 0));
        CollectionAssert.AreEqual(vertices, front.vertices);
        Assert.AreEqual(0, s.GetProgress(s.GetHandle(0)).CompletedVersion);
        Assert.AreEqual(0, s.Tick(2).RefreshedBuckets);
        applier.FailAt = -1;
        var result = s.FlushAll(HolderFlushReason.Recovery, 2);
        Assert.AreEqual(HolderBucketStatus.Ready, result.Status);
        Assert.AreEqual(2, result.CompletedHolders);
        Assert.IsTrue(result.IsBarrier);
        Assert.AreEqual(8, s.GetPublishedMesh(0, 0).vertices[0].x);
        Assert.AreEqual(9, s.GetPublishedMesh(0, 0).vertices[4].x);
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_015.md
    [Test]
    [Category("UT_BATCH_015")]
    public void StructuralBarrier_DoesNotPublishAnyBucket_WhenLaterBucketFails()
    {
        var s = Create(256, out var applier);
        var a = s.GetPublishedMesh(0, 0); var b = s.GetPublishedMesh(1, 0);
        s.Submit(s.GetHandle(0), Value(6), 1);
        applier.FailAt = applier.Calls + 129;
        Assert.AreEqual(HolderBucketStatus.Faulted, s.FlushAll(HolderFlushReason.Structure, 1).Status);
        Assert.AreSame(a, s.GetPublishedMesh(0, 0)); Assert.AreSame(b, s.GetPublishedMesh(1, 0));
        Assert.IsTrue(s.GetProgress(s.GetHandle(0)).Pending);
        applier.FailAt = -1;
        Assert.AreEqual(2, s.FlushAll(HolderFlushReason.Recovery, 2).RefreshedBuckets);
        Assert.AreEqual(6, s.GetPublishedMesh(0, 0).vertices[0].x);
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_016.md
    [Test]
    [Category("UT_BATCH_016")]
    public void ResourceChanges_BlockDrawAndOrdinaryTicks_UntilExplicitResynchronization()
    {
        var s = Create(129, out var applier);
        _resourceVersion++;
        Assert.IsFalse(s.CanDraw);
        Assert.AreEqual(HolderBucketStatus.NeedsResync, s.Tick(1).Status);
        Assert.AreEqual(HolderBucketStatus.Ready, s.FlushAll(HolderFlushReason.Resources, 1).Status);
        Assert.IsTrue(s.CanDraw);
        applier.Callback = () => _resourceVersion++;
        s.Submit(s.GetHandle(128), Value(2), 2);
        Assert.AreEqual(HolderBucketStatus.NeedsResync, s.Tick(2).Status);
        Assert.IsFalse(s.CanDraw);
        Assert.IsTrue(s.GetProgress(s.GetHandle(128)).Pending);
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_017.md
    [Test]
    [Category("UT_BATCH_017")]
    public void OrderedPlanning_PreservesAlternatingTextures_AndRecoversAfterOversizedHolder()
    {
        var a = new Holder(); var b = new Holder(); var c = new Holder();
        _holders.Add(a); _holders.Add(b); _holders.Add(c);
        a.Data.TextureIndex = 1; b.Data.TextureIndex = 2; c.Data.TextureIndex = 1;
        var ordered = new MergeBatcher(1, true);
        LogAssert.Expect(LogType.Warning, new Regex("split into 3 batches"));
        var batches = ordered.Plan(new[] { a, b, c });
        Assert.AreEqual(3, batches.Count);
        Assert.AreSame(a, batches[0].Holders[0]); Assert.AreSame(b, batches[1].Holders[0]); Assert.AreSame(c, batches[2].Holders[0]);
        LogAssert.Expect(LogType.Warning, new Regex("split into 2 batches"));
        Assert.AreEqual(2, new MergeBatcher(1).Plan(new[] { a, b, c }).Count);
        // Capacity failure must invalidate the plan cache, even when the old input is restored.
        var oversized = new MultiHolder(a.Data, b.Data);
        Assert.Throws<InvalidOperationException>(() => ordered.Plan(new IUIPrefabHolder[] { a, oversized }));
        LogAssert.Expect(LogType.Warning, new Regex("split into 3 batches"));
        Assert.AreEqual(3, ordered.Plan(new[] { a, b, c }).Count);
        Assert.IsFalse(ordered.LastPlanCacheHit);
    }

    private sealed class MultiHolder : IUIPrefabHolder
    {
        public MultiHolder(params IUIData[] meshes) { UIMeshDatas = meshes; }
        public UIPrefabOwner Target => null;
        public Vector3 Position => Vector3.zero;
        public UIPrefabRegistration wrapper => null;
        public IList<IUIData> UIMeshDatas { get; }
        public void SetTarget(UIPrefabOwner target) { }
        public void SetWrapper(UIPrefabRegistration wrapper) { }
        public void BuildMesh(IUIDrawTarget[] draws) { }
        public void Fill(List<Vector3> v, List<Vector4> uv, List<Color32> c, List<int> t, Vector3 p) { }
        public void Fill(List<int> t, Vector3 p) { }
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_018.md
    [Test]
    [Category("UT_BATCH_018")]
    public void WarmSubmitAndTick_DoNotAllocate_WithPositiveCounterControl()
    {
        var s = Create(256, out _);
        var handles = new HolderBucketHandle[256];
        for (int i = 0; i < handles.Length; i++) handles[i] = s.GetHandle(i);
        for (int f = 1; f <= 16; f++)
        { for (int i = 0; i < handles.Length; i++) s.Submit(handles[i], Value(f), f); s.Tick(f); }
        using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 4096, ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
        {
            GC.KeepAlive(new byte[4096]); recorder.Stop();
            Assert.IsTrue(recorder.Valid); Assert.Greater(recorder.Count, 0);
            recorder.Reset(); recorder.Start();
            for (int f = 17; f <= 48; f++)
            { for (int i = 0; i < handles.Length; i++) s.Submit(handles[i], Value(f), f); s.Tick(f); }
            recorder.Stop();
            Assert.AreEqual(0, recorder.Count, "Warm snapshots, scheduling, fill and upload must not allocate managed memory.");
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_019.md
    [Test]
    [Category("UT_BATCH_019")]
    public void Strategies_ConvergeToIdenticalGeometry_AndDisposeOwnedResourcesOnly()
    {
        var strategies = new[] { HolderBucketStrategy.SingleBucket, HolderBucketStrategy.SameFrame, HolderBucketStrategy.Rotating };
        var outputs = new List<Vector3[]>();
        foreach (var strategy in strategies)
        {
            var s = Create(256, out _, strategy);
            var storage = s.GetStorage();
            Assert.AreEqual(strategy == HolderBucketStrategy.SingleBucket ? 2 : 4, storage.Renderers);
            Assert.AreEqual(storage.Renderers, storage.Meshes);
            Assert.AreEqual(storage.Renderers, storage.Materials);
            for (int f = 1; f <= 4; f++)
            {
                for (int i = 0; i < 256; i++) s.Submit(s.GetHandle(i), Value(i + f), f);
                var result = s.Tick(f);
                Assert.AreEqual(strategy == HolderBucketStrategy.SameFrame ? 2 : 1, result.RefreshedBuckets);
            }
            Assert.AreEqual(0, s.Tick(5).PendingHolders);
            var all = new List<Vector3>();
            for (int b = 0; b < s.BucketCount; b++) all.AddRange(s.GetPublishedMesh(b, 0).vertices);
            outputs.Add(all.ToArray());
            var mesh = s.GetPublishedMesh(0, 0); s.Dispose();
            Assert.IsTrue(mesh == null); Assert.IsFalse(s.CanDraw);
        }
        CollectionAssert.AreEqual(outputs[0], outputs[1]); CollectionAssert.AreEqual(outputs[0], outputs[2]);
        // Scheduler disposal must not dispose externally owned holder data.
        _holders[0].Data.WriteSimpleQuad(new Vector4(0, 0, 1, 1), Vector4.zero, Color.white);
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_020.md
    [Test]
    [Category("UT_BATCH_020")]
    public void FailedBindingAfterUpload_DoesNotPublishOrCompleteRequest()
    {
        var s = Create(1, out _); var h = s.GetHandle(0);
        var front = s.GetPublishedMesh(0, 0); var original = front.vertices;
        s.Submit(h, Value(12), 1); _failBinding = true;
        Assert.AreEqual(HolderBucketStatus.Faulted, s.Tick(1).Status);
        Assert.AreSame(front, s.GetPublishedMesh(0, 0));
        CollectionAssert.AreEqual(original, front.vertices);
        Assert.IsTrue(s.GetProgress(h).Pending);
        _failBinding = false;
        Assert.AreEqual(HolderBucketStatus.Ready, s.FlushAll(HolderFlushReason.Recovery, 2).Status);
        Assert.AreEqual(12, s.GetPublishedMesh(0, 0).vertices[0].x);
        Assert.IsFalse(s.GetProgress(h).Pending);
    }

    // TestRecord: Documentation~/Testing/Unit/Batching/UT_BATCH_021.md
    [Test]
    [Category("UT_BATCH_021")]
    public void FailedReplacement_KeepsOldEpochAndOutput_ThenSuccessfulReplacementCancelsRequests()
    {
        var s = Create(1, out var applier); var old = s.GetHandle(0);
        var front = s.GetPublishedMesh(0, 0);
        var replacement = new Holder(); _holders.Add(replacement);
        s.Submit(old, Value(3), 1);
        applier.FailAt = applier.Calls + 1;
        Assert.AreEqual(HolderBucketStatus.Faulted, s.ReplaceMembers(new[] { replacement }, new[] { Value(4) }, 1).Status);
        Assert.AreSame(front, s.GetPublishedMesh(0, 0));
        Assert.IsTrue(s.GetProgress(old).Pending);
        applier.FailAt = -1;
        Assert.AreEqual(HolderBucketStatus.Ready, s.ReplaceMembers(new[] { replacement }, new[] { Value(5) }, 2).Status);
        Assert.Throws<ArgumentException>(() => s.GetProgress(old));
        Assert.AreEqual(5, s.GetPublishedMesh(0, 0).vertices[0].x);
        Assert.IsTrue(front == null, "Successful replacement releases old owned output.");
    }
}
