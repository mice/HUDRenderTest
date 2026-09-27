using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

public class TestVertexHelperUtils
{
    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_019.md
    [Test]
    [Category("UT_MESH_019")]
    public void CopyIndices_WritesTopologyWithDestinationAndVertexOffsets()
    {
        var destination = new int[10];
        for (int i = 0; i < destination.Length; i++)
            destination[i] = -1;

        using (var helper = CreateQuad())
        {
            helper.CopyIndices(destination, 2, 5);
        }

        CollectionAssert.AreEqual(new[] { -1, -1, 5, 6, 7, 7, 8, 5, -1, -1 }, destination);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_020.md
    [Test]
    [Category("UT_MESH_020")]
    public void CopyIndices_RejectsNullAndInsufficientDestination()
    {
        using (var helper = CreateQuad())
        {
            Assert.Throws<ArgumentNullException>(() => helper.CopyIndices(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => helper.CopyIndices(new int[5]));
            Assert.Throws<ArgumentOutOfRangeException>(() => helper.CopyIndices(new int[6], -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => helper.CopyIndices(new int[6], 1));
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_013.md
    [Test]
    [Category("UT_MESH_013")]
    public void FillData2_WritesTextureIndexAndFlags()
    {
        var verts = new Vector3[0];
        var colors = new Color32[0];
        var uvs = new Vector4[0];
        var triangles = new int[0];

        using (var helper = CreateQuad())
        {
            var result = helper.FillData2(ref verts, ref colors, ref uvs, ref triangles, 2, 9);

            Assert.AreEqual(4, result.Item1);
            Assert.AreEqual(6, result.Item2);
        }

        for (int i = 0; i < uvs.Length; i++)
        {
            Assert.AreEqual(2, uvs[i].z);
            Assert.AreEqual(9, uvs[i].w);
        }
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_014.md
    [Test]
    [Category("UT_MESH_014")]
    public void FillData3_IndicesIncludeVertexOffset()
    {
        var verts = new Vector3[0];
        var colors = new Color32[0];
        var uvs = new Vector4[0];
        var triangles = new int[0];
        const int vertexOffset = 5;
        const int indicesOffset = 7;

        using (var helper = CreateQuad())
        {
            helper.FillData3(ref verts, ref colors, ref uvs, ref triangles, vertexOffset, indicesOffset, 3, 11);
        }

        CollectionAssert.AreEqual(new[] { 5, 6, 7, 7, 8, 5 }, new[]
        {
            triangles[indicesOffset + 0],
            triangles[indicesOffset + 1],
            triangles[indicesOffset + 2],
            triangles[indicesOffset + 3],
            triangles[indicesOffset + 4],
            triangles[indicesOffset + 5],
        });
        Assert.AreEqual(3, uvs[vertexOffset].z);
        Assert.AreEqual(11, uvs[vertexOffset].w);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_016.md
    [Test]
    [Category("UT_MESH_016")]
    public void FillData2_PreservesIndexedTopologyAndVertexChannels()
    {
        var verts = Array.Empty<Vector3>();
        var colors = Array.Empty<Color32>();
        var uvs = Array.Empty<Vector4>();
        var triangles = Array.Empty<int>();

        using (var helper = CreateDetailedQuad())
        {
            var result = helper.FillData2(ref verts, ref colors, ref uvs, ref triangles, 7, 11);

            Assert.AreEqual(4, result.Item1);
            Assert.AreEqual(6, result.Item2);
        }

        CollectionAssert.AreEqual(new[]
        {
            new Vector3(1, 2, 0),
            new Vector3(3, 5, 0),
            new Vector3(8, 13, 0),
            new Vector3(21, 34, 0),
        }, verts);
        CollectionAssert.AreEqual(new[]
        {
            new Color32(10, 20, 30, 40),
            new Color32(50, 60, 70, 80),
            new Color32(90, 100, 110, 120),
            new Color32(130, 140, 150, 160),
        }, colors);
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 3, 0 }, triangles);

        Assert.AreEqual(new Vector4(0.1f, 0.2f, 7, 11), uvs[0]);
        Assert.AreEqual(new Vector4(0.3f, 0.4f, 7, 11), uvs[1]);
        Assert.AreEqual(new Vector4(0.5f, 0.6f, 7, 11), uvs[2]);
        Assert.AreEqual(new Vector4(0.7f, 0.8f, 7, 11), uvs[3]);
    }

    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_017.md
    [Test]
    [Category("UT_MESH_017")]
    public void FillData2And3_AfterWarmup_DoNotAllocateManagedMemory()
    {
        const int warmupIterations = 32;
        const int measuredIterations = 10000;
        var exactVerts = new Vector3[4];
        var exactColors = new Color32[4];
        var exactUvs = new Vector4[4];
        var exactTriangles = new int[6];
        var appendVerts = new Vector3[6];
        var appendColors = new Color32[6];
        var appendUvs = new Vector4[6];
        var appendTriangles = new int[9];
        var stopwatch = new Stopwatch();

        using (var helper = CreateQuad())
        {
            for (int i = 0; i < warmupIterations; i++)
            {
                helper.FillData2(ref exactVerts, ref exactColors, ref exactUvs, ref exactTriangles, 2, 9);
                helper.FillData3(ref appendVerts, ref appendColors, ref appendUvs, ref appendTriangles, 2, 3, 3, 11);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();


            stopwatch.Restart();
            for (int i = 0; i < measuredIterations; i++)
            {
                helper.FillData2(ref exactVerts, ref exactColors, ref exactUvs, ref exactTriangles, 2, 9);
            }
            stopwatch.Stop();

            long fill2Ticks = stopwatch.ElapsedTicks;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();


            stopwatch.Restart();
            for (int i = 0; i < measuredIterations; i++)
            {
                helper.FillData3(ref appendVerts, ref appendColors, ref appendUvs, ref appendTriangles, 2, 3, 3, 11);
            }
            stopwatch.Stop();

            long fill3Ticks = stopwatch.ElapsedTicks;
            long fill2Allocated = MeasureAllocation(() => {
                for (int i = 0; i < measuredIterations; i++)
                    helper.FillData2(ref exactVerts, ref exactColors, ref exactUvs, ref exactTriangles, 2, 9);
            });
            long fill3Allocated = MeasureAllocation(() => {
                for (int i = 0; i < measuredIterations; i++)
                    helper.FillData3(ref appendVerts, ref appendColors, ref appendUvs, ref appendTriangles, 2, 3, 3, 11);
            });

            TestContext.Out.WriteLine(
                $"VertexHelper extraction: iterations={measuredIterations}, " +
                $"FillData2Ticks={fill2Ticks}, FillData2AllocationEvents={fill2Allocated}, " +
                $"FillData3Ticks={fill3Ticks}, FillData3AllocationEvents={fill3Allocated}");

            Assert.AreEqual(0, fill2Allocated, "FillData2 allocated managed memory after warm-up.");
            Assert.AreEqual(0, fill3Allocated, "FillData3 allocated managed memory after warm-up.");
        }
    }

    // PERF-01 benchmark evidence: keep the fixture and sample configuration stable when comparing runs.
    // TestRecord: Documentation~/Testing/Unit/Mesh/UT_MESH_018.md
    [Test]
    [Category("UT_MESH_018")]
    public void PublicExtraction_Benchmark_ReportsMedianAndTailCosts()
    {
        const int warmupIterations = 32;
        const int sampleCount = 21;
        const int iterationsPerSample = 500;
        var exactVerts = new Vector3[4];
        var exactColors = new Color32[4];
        var exactUvs = new Vector4[4];
        var exactTriangles = new int[6];
        var appendVerts = new Vector3[8];
        var appendColors = new Color32[8];
        var appendUvs = new Vector4[8];
        var appendTriangles = new int[12];
        var mesh = new Mesh { name = "UIDataRender benchmark scratch" };
        var scratchIndices = new List<int>(6);

        using (var helper = CreateDetailedQuad())
        {
            for (int i = 0; i < warmupIterations; i++)
            {
                helper.FillData2(ref exactVerts, ref exactColors, ref exactUvs, ref exactTriangles, 2, 9);
                helper.FillData3(ref appendVerts, ref appendColors, ref appendUvs, ref appendTriangles, 3, 4, 3, 11);
                MeasureVertexChannelRead(helper, exactVerts, exactColors, exactUvs);
                MeasureIndexRead(helper, mesh, scratchIndices);
            }

            var fill2 = MeasureSamples(sampleCount, iterationsPerSample, () =>
                helper.FillData2(ref exactVerts, ref exactColors, ref exactUvs, ref exactTriangles, 2, 9));
            var fill3 = MeasureSamples(sampleCount, iterationsPerSample, () =>
                helper.FillData3(ref appendVerts, ref appendColors, ref appendUvs, ref appendTriangles, 3, 4, 3, 11));
            var vertexChannels = MeasureSamples(sampleCount, iterationsPerSample, () =>
                MeasureVertexChannelRead(helper, exactVerts, exactColors, exactUvs));
            var indices = MeasureSamples(sampleCount, iterationsPerSample, () =>
                MeasureIndexRead(helper, mesh, scratchIndices));

            long fill2Allocated = MeasureAllocation(() =>
                helper.FillData2(ref exactVerts, ref exactColors, ref exactUvs, ref exactTriangles, 2, 9));
            long fill3Allocated = MeasureAllocation(() =>
                helper.FillData3(ref appendVerts, ref appendColors, ref appendUvs, ref appendTriangles, 3, 4, 3, 11));

            TestContext.Out.WriteLine(
                $"PERF-01 environment=Unity {Application.unityVersion}, " +
                $"fixture=4 vertices/6 indices, warmup={warmupIterations}, " +
                $"samples={sampleCount}, iterationsPerSample={iterationsPerSample}, " +
                $"frequency={Stopwatch.Frequency}");
            WriteBenchmark("FillData2", fill2);
            WriteBenchmark("FillData3", fill3);
            WriteBenchmark("VertexChannels", vertexChannels);
            WriteBenchmark("Indices", indices);
            TestContext.Out.WriteLine($"PERF-04 calibratedAllocationEvents FillData2={fill2Allocated}, FillData3={fill3Allocated}");

            Assert.AreEqual(0, fill2Allocated, "FillData2 allocated managed memory after warm-up.");
            Assert.AreEqual(0, fill3Allocated, "FillData3 allocated managed memory after warm-up.");
        }

        UnityEngine.Object.DestroyImmediate(mesh);
    }

    private static long[] MeasureSamples(int sampleCount, int iterationsPerSample, Action operation)
    {
        var samples = new long[sampleCount];
        var stopwatch = new Stopwatch();
        for (int sample = 0; sample < sampleCount; sample++)
        {
            stopwatch.Restart();
            for (int i = 0; i < iterationsPerSample; i++)
            {
                operation();
            }
            stopwatch.Stop();
            samples[sample] = stopwatch.ElapsedTicks;
        }
        return samples;
    }

    private static long MeasureAllocation(Action operation)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        // Unity's thread-byte API can return constant zero. Count real GC.Alloc events,
        // using an explicit positive control before permitting a zero-allocation assertion.
        // No SumAllSamplesInFrame: each allocation is an individual sample in this scope.
        using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 65536,
            ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
        {
            var positiveControl = new byte[4096];
            GC.KeepAlive(positiveControl);
            recorder.Stop();
            Assert.IsTrue(recorder.Valid, "GC.Alloc recorder unavailable.");
            Assert.Greater(recorder.Count, 0, "GC.Alloc positive allocation control was not observed.");
            recorder.Reset();
            recorder.Start();
            operation();
            recorder.Stop();
            int count = recorder.Count;
            Assert.Less(count, recorder.Capacity, "Allocation recorder saturated; result is incomplete.");
            return count;
        }
    }

    private static void MeasureVertexChannelRead(VertexHelper helper, Vector3[] verts, Color32[] colors, Vector4[] uvs)
    {
        var vertex = default(UIVertex);
        for (int i = 0; i < helper.currentVertCount; i++)
        {
            helper.PopulateUIVertex(ref vertex, i);
            verts[i] = vertex.position;
            colors[i] = vertex.color;
            uvs[i] = vertex.uv0;
        }
    }

    private static void MeasureIndexRead(VertexHelper helper, Mesh mesh, List<int> indices)
    {
        helper.FillMesh(mesh);
        indices.Clear();
        mesh.GetTriangles(indices, 0);
    }

    private static void WriteBenchmark(string name, long[] samples)
    {
        var ordered = (long[])samples.Clone();
        Array.Sort(ordered);
        double scale = 1_000_000d / Stopwatch.Frequency / 500d;
        double median = ordered[ordered.Length / 2] * scale;
        double p95 = ordered[Math.Min(ordered.Length - 1, (int)Math.Ceiling(ordered.Length * 0.95d) - 1)] * scale;
        double minimum = ordered[0] * scale;
        double maximum = ordered[ordered.Length - 1] * scale;
        TestContext.Out.WriteLine(
            $"PERF-01 {name} us/call min={minimum:F3}, median={median:F3}, p95={p95:F3}, max={maximum:F3}");
    }

    private static VertexHelper CreateQuad()
    {
        var helper = new VertexHelper();
        helper.AddVert(new Vector3(10, 0, 0), Color.white, Vector2.zero);
        helper.AddVert(new Vector3(10, 10, 0), Color.white, Vector2.up);
        helper.AddVert(new Vector3(20, 10, 0), Color.white, Vector2.one);
        helper.AddVert(new Vector3(20, 0, 0), Color.white, Vector2.right);
        helper.AddTriangle(0, 1, 2);
        helper.AddTriangle(2, 3, 0);
        return helper;
    }

    private static VertexHelper CreateDetailedQuad()
    {
        var helper = new VertexHelper();
        AddVertex(helper, new Vector3(1, 2, 0), new Color32(10, 20, 30, 40), new Vector4(0.1f, 0.2f, 0, 0));
        AddVertex(helper, new Vector3(3, 5, 0), new Color32(50, 60, 70, 80), new Vector4(0.3f, 0.4f, 0, 0));
        AddVertex(helper, new Vector3(8, 13, 0), new Color32(90, 100, 110, 120), new Vector4(0.5f, 0.6f, 0, 0));
        AddVertex(helper, new Vector3(21, 34, 0), new Color32(130, 140, 150, 160), new Vector4(0.7f, 0.8f, 0, 0));
        helper.AddTriangle(0, 1, 2);
        helper.AddTriangle(2, 3, 0);
        return helper;
    }

    private static void AddVertex(VertexHelper helper, Vector3 position, Color32 color, Vector4 uv)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertex.uv0 = uv;
        helper.AddVert(vertex);
    }
}
