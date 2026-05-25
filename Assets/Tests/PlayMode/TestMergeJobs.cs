using System.Collections;
using Unity.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UIData;

/// <summary>
/// TC-JOB-02: BigXMergeJob output matches the expected single-thread merge result.
/// </summary>
public class TestMergeJobs
{
    private static UIMeshData MakeQuad(Vector3 offset)
    {
        var m = new UIMeshData();
        m.vertList = new Vector3[]
        {
            new Vector3(0, 0, 0) + offset,
            new Vector3(1, 0, 0) + offset,
            new Vector3(1, 1, 0) + offset,
            new Vector3(0, 1, 0) + offset,
        };
        m.uvs = new Vector4[]
        {
            new Vector4(0, 0, 0, 0),
            new Vector4(0, 1, 0, 0),
            new Vector4(1, 1, 0, 0),
            new Vector4(1, 0, 0, 0),
        };
        m.colors = System.Array.Empty<Color32>();
        m.triangles = new int[] { 0, 1, 2, 2, 3, 0 };
        m.mesh = new MeshSlim { VertexCount = 4, IndicesCount = 6 };
        return m;
    }

    // TC-JOB-02
    [UnityTest]
    public IEnumerator Merge_EquivalentToSingleThread()
    {
        const int meshCount = 2;
        var meshA = MakeQuad(Vector3.zero);
        var meshB = MakeQuad(new Vector3(2, 0, 0));

        int totalVerts   = meshCount * 4;
        int totalIndices = meshCount * 6;

        var pos           = new NativeArray<Vector3>(meshCount, Allocator.TempJob);
        var resultPos     = new NativeArray<Vector3>(totalVerts,   Allocator.TempJob);
        var resultUv      = new NativeArray<Vector4>(totalVerts,   Allocator.TempJob);
        var resultColors  = new NativeArray<Color32>(totalVerts,   Allocator.TempJob);
        var resultTriangle= new NativeArray<int>    (totalIndices, Allocator.TempJob);
        var resultCount   = new NativeArray<int>    (2,            Allocator.TempJob);

        // Zero positions so output equals input (simplifies assertions)
        pos[0] = Vector3.zero;
        pos[1] = Vector3.zero;

        try
        {
            var go      = new GameObject("MergeBridge");
            var handler = go.AddComponent<ManagedCodeInJob>();

            var job = new BigXMergeJob
            {
                arr         = new[] { meshA, meshB },
                UIMeshCount = meshCount,
                pos         = pos,
                result_pos  = resultPos,
                result_colors   = resultColors,
                result_uv       = resultUv,
                result_triangle = resultTriangle,
                result_count    = resultCount,
            };

            handler.ScheduleTask(job);
            yield return null; // LateUpdate completes job

            // Verify total counts
            Assert.AreEqual(totalVerts,   resultCount[0], "vertCountTotal must equal total input verts");
            Assert.AreEqual(totalIndices, resultCount[1], "indexCountTotal must equal total input indices");

            // Verify vertices: first 4 from meshA, next 4 from meshB
            Color32 white = Color.white;
            for (int i = 0; i < 4; i++)
            {
                Assert.That(resultPos[i].x, Is.EqualTo(meshA.vertList[i].x).Within(1e-4f));
                Assert.That(resultPos[i].y, Is.EqualTo(meshA.vertList[i].y).Within(1e-4f));
                Assert.That(resultPos[i + 4].x, Is.EqualTo(meshB.vertList[i].x).Within(1e-4f));
                Assert.AreEqual(white.r, resultColors[i].r);
            }

            // Verify triangle indices: meshB indices must be offset by 4 (meshA vertex count)
            for (int k = 0; k < 6; k++)
            {
                Assert.AreEqual(meshA.triangles[k],        resultTriangle[k],     $"triangle[{k}] for meshA");
                Assert.AreEqual(meshB.triangles[k] + 4,    resultTriangle[6 + k], $"triangle[{k}] for meshB (offset +4)");
            }

            Object.DestroyImmediate(go);
        }
        finally
        {
            pos.Dispose();
            resultPos.Dispose();
            resultUv.Dispose();
            resultColors.Dispose();
            resultTriangle.Dispose();
            resultCount.Dispose();
        }
    }
}
