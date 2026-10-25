using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed partial class HudRcPerformanceRunner
{
    private struct HudHolderState
    {
        public float Width;
        public Vector3 Position;
        public Color EvenColor, OddColor;
    }

    [Serializable]
    public struct BucketRequestTrace
    {
        public int holder, mode, sampleFrame, requestFrame, completedFrame;
        public long requestVersion, completedVersion;
    }

    [Serializable]
    public struct BucketFinalState
    {
        public int holder, mode, completedFrame;
        public long requestedVersion, completedVersion;
    }

    private sealed class HudStateApplier : IHolderStateApplier<HudHolderState>
    {
        private readonly HudRcPerformanceRunner _owner;
        private int _opened;
        public int ScopeCount;
        public HudStateApplier(HudRcPerformanceRunner owner) { _owner = owner; }
        public void BeginBatch()
        {
            _opened = 0;
            for (int i = 0; i < _owner.images.Length; i++)
            {
                _owner.generationBatches[i] = _owner.images[i].BeginGenerationBatch(_owner.templateRoot.transform);
                _opened++; ScopeCount++;
            }
        }
        public Vector3 Apply(IUIPrefabHolder holder, in HudHolderState state)
        {
            for (int i = 0; i < _owner.images.Length; i++)
            {
                _owner.images[i].rectTransform.sizeDelta = new Vector2(state.Width, 3);
                _owner.images[i].color = i % 2 == 0 ? state.EvenColor : state.OddColor;
            }
            holder.BuildMesh(_owner.draws);
            return state.Position;
        }
        public void EndBatch()
        {
            for (int i = _opened - 1; i >= 0; i--) _owner.generationBatches[i].Dispose();
            _opened = 0;
        }
    }

    private HolderBucketScheduler<HudHolderState> bucketScheduler;
    private HolderBucketCameraPass bucketCameraPass;
    private HudStateApplier bucketApplier;
    private HolderBucketHandle[] bucketHandles;
    private HudHolderState[] bucketStates;
    private BucketRequestTrace[] bucketRequests;
    private BucketFinalState[] bucketFinalStates;
    private string[] bucketOutputChecksums;
    private int[] bucketTraceHeads, bucketTracePrevious;
    private long[] bucketLastCompleted;
    private int bucketFrame, bucketRequestCount;
    private double bucketInitializationMs;
    private HolderBucketStorage bucketStorage;

    private void PrepareBuckets()
    {
        long initializationStart = Stopwatch.GetTimestamp();
        bucketFrame = bucketRequestCount = 0;
        bucketStates = new HudHolderState[holders.Count];
        bucketHandles = new HolderBucketHandle[holders.Count];
        bucketTraceHeads = new int[holders.Count];
        bucketLastCompleted = new long[holders.Count];
        bucketFinalStates = new BucketFinalState[holders.Count * 3];
        bucketOutputChecksums = new string[3];
        int requests = 0;
        for (int m = 0; m < 3; m++)
            for (int f = 0; f < active.sampleFrames; f++) requests += PlannedUpdates((Workload)m, f, active);
        bucketRequests = new BucketRequestTrace[requests];
        bucketTracePrevious = new int[requests];
        for (int i = 0; i < holders.Count; i++)
        {
            bucketTraceHeads[i] = -1;
            bucketStates[i] = new HudHolderState { Width = 22, Position = positions[holders[i]], EvenColor = Color.green, OddColor = Color.cyan };
        }
        bucketApplier = new HudStateApplier(this);
        bucketScheduler = new HolderBucketScheduler<HudHolderState>(3, bucketApplier, createMaterial, bindTextures,
            () => UIPrefabManager.Instance.TextureVersion, active.bucketStrategy);
        RequireBucketSuccess(bucketScheduler.ReplaceMembers(holders, bucketStates, 0));
        for (int i = 0; i < holders.Count; i++) bucketHandles[i] = bucketScheduler.GetHandle(i);
        bucketCameraPass = new HolderBucketCameraPass(outputCamera, bucketScheduler);
        bucketCameraPass.Refresh(Matrix4x4.identity);
        UpdateBucketGeometryCounts();
        bucketInitializationMs = Milliseconds(Stopwatch.GetTimestamp() - initializationStart);
        bucketStorage = bucketScheduler.GetStorage();
    }

    private static void RequireBucketSuccess(HolderBucketTickResult result)
    {
        if (result.Status != HolderBucketStatus.Ready) throw new InvalidOperationException("Bucket update failed: " + result.Status);
    }

    private void AdvanceBucketFrame()
    {
        long advanceStart = Stopwatch.GetTimestamp();
        if (hasPending)
        {
            pending.frameIntervalMs = Milliseconds(advanceStart - previousUpdate);
            pending.mainThreadAvailable = mainThread.Valid && mainThread.Count > 0 && mainThread.LastValue > 0;
            pending.mainThreadMs = pending.mainThreadAvailable ? mainThread.LastValue / 1000000.0 : -1;
            pending.frameGcAvailable = frameGc.Valid && frameGc.Count > 0;
            pending.frameGcBytes = pending.frameGcAvailable ? frameGc.LastValue : -1;
            samples[sampleCount++] = pending;
            hasPending = false;
        }
        previousUpdate = advanceStart;
        if (modeFrame == active.warmupFrames + active.sampleFrames + 2)
        {
            CaptureBucketFinalState(mode);
            mode++; modeFrame = 0;
            if (mode == 3)
            {
                if (bucketRequestCount != bucketRequests.Length) throw new InvalidOperationException("Request trace count mismatch.");
                State = RunState.Completed;
                ReleaseResources();
                if (autoExport) ExportReport();
                return;
            }
        }

        bool sampling = modeFrame >= active.warmupFrames;
        int frame = sampling ? modeFrame - active.warmupFrames : modeFrame;
        bool drain = sampling && frame >= active.sampleFrames;
        State = sampling ? RunState.Sampling : RunState.Warming;
        bucketFrame++;
        long bytes = scopedAllocationAvailable ? GC.GetAllocatedBytesForCurrentThread() : 0;
        long updateStart = Stopwatch.GetTimestamp();
        int count = drain ? 0 : sampling ? PlannedUpdates((Workload)mode, frame, active) : active.holderCount;
        int start = (int)(((long)frame * Math.Max(1, count) + (uint)active.seed) % active.holderCount);
        int checksum = 0;
        bucketApplier.ScopeCount = 0;
        for (int j = 0; j < count; j++)
        {
            int index = (start + j) % active.holderCount;
            bucketStates[index].Width = 12 + ((index + frame + (uint)active.seed) % 11);
            long version = bucketScheduler.Submit(bucketHandles[index], in bucketStates[index], bucketFrame);
            checksum = unchecked(checksum * 31 + index + 1);
            if (sampling)
            {
                int trace = bucketRequestCount++;
                bucketRequests[trace] = new BucketRequestTrace { holder = index, mode = mode, sampleFrame = frame,
                    requestFrame = bucketFrame, requestVersion = version, completedFrame = -1, completedVersion = -1 };
                bucketTracePrevious[trace] = bucketTraceHeads[index];
                bucketTraceHeads[index] = trace;
            }
        }
        // Warmup ends with an explicit barrier so the static measurement starts fully converged.
        var result = modeFrame == active.warmupFrames - 1
            ? bucketScheduler.FlushAll(HolderFlushReason.Explicit, bucketFrame) : bucketScheduler.Tick(bucketFrame);
        RequireBucketSuccess(result);
        long updateEnd = Stopwatch.GetTimestamp();
        bucketCameraPass.Refresh(Matrix4x4.identity);
        long drawEnd = Stopwatch.GetTimestamp();
        long requestedSum = 0, completedSum = 0;
        for (int i = 0; i < bucketHandles.Length; i++)
        {
            var progress = bucketScheduler.GetProgress(bucketHandles[i]);
            requestedSum += progress.RequestedVersion; completedSum += progress.CompletedVersion;
            if (progress.CompletedVersion == bucketLastCompleted[i]) continue;
            bucketLastCompleted[i] = progress.CompletedVersion;
            int trace = bucketTraceHeads[i];
            while (trace >= 0)
            {
                if (bucketRequests[trace].requestVersion > progress.CompletedVersion)
                    throw new InvalidOperationException("Trace contains a future request at completion.");
                bucketRequests[trace].completedFrame = progress.CompletedFrame;
                bucketRequests[trace].completedVersion = progress.CompletedVersion;
                trace = bucketTracePrevious[trace];
            }
            bucketTraceHeads[i] = -1;
        }
        UpdateBucketGeometryCounts();
        long end = Stopwatch.GetTimestamp();
        long measuredBytes = scopedAllocationAvailable ? GC.GetAllocatedBytesForCurrentThread() - bytes : -1;
        if (sampling)
        {
            double updateMs = Milliseconds(updateEnd - updateStart);
            pending = new FrameSample
            {
                unityFrame = Time.frameCount, sampleFrame = frame, mode = mode,
                bucketScheduling = true, drainFrame = drain, barrierFrame = result.IsBarrier,
                requestedHolders = count, completedHolders = result.CompletedHolders, updatedHolders = result.CompletedHolders,
                pendingHolders = result.PendingHolders, logicalBuckets = bucketScheduler.BucketCount,
                refreshedBuckets = result.RefreshedBuckets, batches = result.MeshCount,
                vertices = VertexCount, indices = IndexCount, selectionChecksum = checksum,
                firstPendingAge = result.MaxFirstPendingAge, latestValueAge = result.MaxLatestValueAge,
                completedFirstPendingAge = result.MaxCompletedFirstPendingAge, completedLatestValueAge = result.MaxCompletedLatestValueAge,
                requestedVersionSum = requestedSum, completedVersionSum = completedSum,
                updateCpuMs = updateMs, generationMs = updateMs - result.MergeMs, mergeMs = result.MergeMs,
                drawSubmitMs = Milliseconds(drawEnd - updateEnd), telemetryMs = Milliseconds(end - drawEnd),
                runnerBookkeepingMs = Milliseconds(updateStart - advanceStart), hudCpuMs = Milliseconds(end - advanceStart),
                generationBytes = -1, mergeBytes = -1, measuredThreadBytes = measuredBytes,
                scopedAllocationAvailable = scopedAllocationAvailable, generationBatchCount = bucketApplier.ScopeCount,
                buildMeshStagesAvailable = false, buildMeshWriteAvailable = false,
                holderSelectionMs = -1, buildMeshMs = -1, buildMeshSetupMs = -1, buildMeshDataInitMs = -1,
                buildMeshDispatchMs = -1, buildMeshDoGenerateMs = -1, buildMeshSimplePathMs = -1,
                buildMeshUguiFallbackMs = -1, buildMeshTransformMs = -1, buildMeshSpriteDataMs = -1,
                buildMeshDrawingDimensionsMs = -1, buildMeshMeshWriteMs = -1, buildMeshWriteStorageMs = -1,
                buildMeshWriteVertexMs = -1, buildMeshWriteUvMs = -1, buildMeshWriteColorMs = -1, buildMeshWriteIndexMs = -1,
                mergePlanMs = -1, mergeFillMs = -1, meshUploadMs = -1
            };
            hasPending = true;
        }
        modeFrame++;
    }

    private void UpdateBucketGeometryCounts()
    {
        VertexCount = IndexCount = 0;
        for (int b = 0; b < bucketScheduler.BucketCount; b++)
            for (int m = 0; ; m++)
            {
                var mesh = bucketScheduler.GetPublishedMesh(b, m);
                if (mesh == null) break;
                VertexCount += mesh.vertexCount; IndexCount += (int)mesh.GetIndexCount(0);
            }
        if (VertexCount == 0 || IndexCount == 0) throw new InvalidOperationException("Bucket workload generated no geometry.");
    }

    private void CaptureBucketFinalState(int workload)
    {
        for (int i = 0; i < bucketHandles.Length; i++)
        {
            var progress = bucketScheduler.GetProgress(bucketHandles[i]);
            if (progress.Pending || bucketTraceHeads[i] >= 0) throw new InvalidOperationException("Bounded drain did not converge.");
            bucketFinalStates[workload * bucketHandles.Length + i] = new BucketFinalState { holder = i, mode = workload,
                requestedVersion = progress.RequestedVersion, completedVersion = progress.CompletedVersion, completedFrame = progress.CompletedFrame };
        }
        // Canonicalize split meshes to global vertex/index order. This readback is outside sampled update work.
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector4>(); var colors = new List<Color32>(); var indices = new List<int>();
            for (int b = 0; b < bucketScheduler.BucketCount; b++)
                for (int m = 0; ; m++)
                {
                    var mesh = bucketScheduler.GetPublishedMesh(b, m); if (mesh == null) break;
                    int offset = vertices.Count;
                    vertices.AddRange(mesh.vertices); colors.AddRange(mesh.colors32);
                    var meshUv = new List<Vector4>(); mesh.GetUVs(0, meshUv); uv.AddRange(meshUv);
                    foreach (int index in mesh.triangles) indices.Add(index + offset);
                }
            for (int i = 0; i < vertices.Count; i++)
            {
                writer.Write(vertices[i].x); writer.Write(vertices[i].y); writer.Write(vertices[i].z);
                writer.Write(uv[i].x); writer.Write(uv[i].y); writer.Write(uv[i].z); writer.Write(uv[i].w);
                writer.Write(colors[i].r); writer.Write(colors[i].g); writer.Write(colors[i].b); writer.Write(colors[i].a);
            }
            foreach (int index in indices) writer.Write(index);
            writer.Flush();
            using (var hash = SHA256.Create()) bucketOutputChecksums[workload] = BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-", "");
        }
    }

    private void ExportBucketCsv(string path)
    {
        using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
        {
            writer.WriteLine("mode,sample_frame,unity_frame,drain,barrier,requested,completed,pending,logical_buckets,refreshed_buckets,meshes,first_pending_age,latest_value_age,completed_first_pending_age,completed_latest_value_age,requested_version_sum,completed_version_sum,request_checksum,query_scopes,update_cpu_ms,merge_ms,draw_submit_ms,telemetry_ms,hud_cpu_ms");
            for (int i = 0; i < sampleCount; i++)
            {
                var s = samples[i];
                writer.WriteLine(string.Join(",", (Workload)s.mode, s.sampleFrame, s.unityFrame, s.drainFrame, s.barrierFrame,
                    s.requestedHolders, s.completedHolders, s.pendingHolders, s.logicalBuckets, s.refreshedBuckets, s.batches,
                    s.firstPendingAge, s.latestValueAge, s.completedFirstPendingAge, s.completedLatestValueAge,
                    s.requestedVersionSum, s.completedVersionSum, s.selectionChecksum, s.generationBatchCount,
                    F(s.updateCpuMs), F(s.mergeMs), F(s.drawSubmitMs), F(s.telemetryMs), F(s.hudCpuMs)));
            }
        }
    }

    private void ReleaseBuckets()
    {
        bucketCameraPass?.Dispose(); bucketCameraPass = null;
        bucketScheduler?.Dispose(); bucketScheduler = null;
        bucketApplier = null; bucketHandles = null; bucketStates = null;
        bucketTraceHeads = null; bucketTracePrevious = null; bucketLastCompleted = null;
    }

    private void ResetBucketReport()
    {
        bucketRequests = null; bucketFinalStates = null; bucketOutputChecksums = null;
        bucketFrame = bucketRequestCount = 0;
        bucketInitializationMs = 0;
        bucketStorage = default;
    }
}
