using NUnit.Framework;
using System;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// TC-MTX-01: CalcMatrix for subNode directly under root with identity local transform returns Identity.
/// TC-MTX-02: CalcMatrix for multi-level nested transform equals root.worldToLocal * sub.localToWorld.
/// </summary>
public class TestCalcMatrix
{
    // TestRecord: Documentation~/Testing/Unit/Matrix/UT_MTX_003.md
    [Test]
    [Category("UT_MTX_003")]
    public void CalcMatrix_OptimizedMatchesLegacy_AndReportsCost()
    {
        var rootGO = new GameObject("root");
        var midGO = new GameObject("mid");
        var subGO = new GameObject("sub");
        midGO.transform.SetParent(rootGO.transform);
        subGO.transform.SetParent(midGO.transform);
        rootGO.transform.localPosition = new Vector3(3, -2, 1);
        rootGO.transform.localRotation = Quaternion.Euler(10, 20, 30);
        rootGO.transform.localScale = new Vector3(1.2f, 0.9f, 1.1f);
        midGO.transform.localPosition = new Vector3(4, 5, 0);
        midGO.transform.localRotation = Quaternion.Euler(0, 0, 25);
        midGO.transform.localScale = new Vector3(0.8f, 1.3f, 1);
        subGO.transform.localPosition = new Vector3(-2, 7, 0);
        subGO.transform.localRotation = Quaternion.Euler(0, 0, -15);
        subGO.transform.localScale = new Vector3(1.4f, 0.7f, 1);

        try
        {
            var expected = LegacyCalcMatrix(rootGO.transform, subGO.transform);
            var actual = Utility.CalcMatrix(rootGO.transform, subGO.transform);
            AssertMatrixApproximatelyEqual(expected, actual);

            const int iterations = 10000;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < iterations; i++)
                LegacyCalcMatrix(rootGO.transform, subGO.transform);
            stopwatch.Stop();
            var legacyTicks = stopwatch.ElapsedTicks;

            stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
                Utility.CalcMatrix(rootGO.transform, subGO.transform);
            stopwatch.Stop();
            var optimizedTicks = stopwatch.ElapsedTicks;

            TestContext.Out.WriteLine(
                $"CalcMatrix comparison: iterations={iterations}, " +
                $"frequency={Stopwatch.Frequency}, legacyTicks={legacyTicks}, optimizedTicks={optimizedTicks}, " +
                $"legacyUs={legacyTicks * 1_000_000d / Stopwatch.Frequency:F3}, " +
                $"optimizedUs={optimizedTicks * 1_000_000d / Stopwatch.Frequency:F3}, " +
                $"speedup={(double)legacyTicks / Math.Max(1, optimizedTicks):F2}x");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(subGO);
            UnityEngine.Object.DestroyImmediate(midGO);
            UnityEngine.Object.DestroyImmediate(rootGO);
        }
    }

    // TC-MTX-01
    // Note: CalcMatrix walks up from subNode until parent==root.
    // When subNode is a direct child of root with identity local transform,
    // the single TRS(zero, identity, one) == Matrix4x4.identity.
    // TestRecord: Documentation~/Testing/Unit/Matrix/UT_MTX_002.md
    [Test]
    [Category("UT_MTX_002")]
    public void SameLevel_IdentityLocal_ReturnsIdentity()
    {
        var rootGO  = new GameObject("root");
        var childGO = new GameObject("child");
        childGO.transform.SetParent(rootGO.transform);
        // Identity local transform
        childGO.transform.localPosition = Vector3.zero;
        childGO.transform.localRotation = Quaternion.identity;
        childGO.transform.localScale    = Vector3.one;

        try
        {
            Matrix4x4 result = Utility.CalcMatrix(rootGO.transform, childGO.transform);
            Assert.That(result, Is.EqualTo(Matrix4x4.identity).Using(Matrix4x4ApproxComparer.Default),
                "Identity local transform should produce Matrix4x4.identity");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(childGO);
            UnityEngine.Object.DestroyImmediate(rootGO);
        }
    }

    // TC-MTX-02
    // TestRecord: Documentation~/Testing/Unit/Matrix/UT_MTX_001.md
    [Test]
    [Category("UT_MTX_001")]
    public void Nested_ReturnsRelativeTRS()
    {
        var rootGO = new GameObject("root");
        var midGO  = new GameObject("mid");
        var subGO  = new GameObject("sub");

        midGO.transform.SetParent(rootGO.transform);
        subGO.transform.SetParent(midGO.transform);

        midGO.transform.localPosition = new Vector3(1, 0, 0);
        midGO.transform.localRotation = Quaternion.identity;
        midGO.transform.localScale    = Vector3.one;

        subGO.transform.localPosition = new Vector3(0, 2, 0);
        subGO.transform.localRotation = Quaternion.Euler(0, 0, 30f);
        subGO.transform.localScale    = new Vector3(2, 2, 2);

        try
        {
            Matrix4x4 expected = rootGO.transform.worldToLocalMatrix * subGO.transform.localToWorldMatrix;
            Matrix4x4 actual   = Utility.CalcMatrix(rootGO.transform, subGO.transform);

            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    Assert.That(actual[r, c], Is.EqualTo(expected[r, c]).Within(1e-4f),
                        $"matrix[{r},{c}] mismatch");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(subGO);
            UnityEngine.Object.DestroyImmediate(midGO);
            UnityEngine.Object.DestroyImmediate(rootGO);
        }
    }

    private static Matrix4x4 LegacyCalcMatrix(Transform root, Transform subNode)
    {
        var tmpNode = subNode;
        var matrix = Matrix4x4.TRS(tmpNode.localPosition, tmpNode.localRotation, tmpNode.localScale);
        while (tmpNode.parent != root)
        {
            tmpNode = tmpNode.parent;
            matrix = Matrix4x4.TRS(tmpNode.localPosition, tmpNode.localRotation, tmpNode.localScale) * matrix;
        }
        return matrix;
    }

    private static void AssertMatrixApproximatelyEqual(Matrix4x4 expected, Matrix4x4 actual)
    {
        for (int row = 0; row < 4; row++)
            for (int column = 0; column < 4; column++)
                Assert.That(actual[row, column], Is.EqualTo(expected[row, column]).Within(1e-4f),
                    $"matrix[{row},{column}] mismatch");
    }

    // Helper: approximate Matrix4x4 equality comparer
    private class Matrix4x4ApproxComparer : System.Collections.IComparer
    {
        public static readonly Matrix4x4ApproxComparer Default = new Matrix4x4ApproxComparer();

        public int Compare(object x, object y)
        {
            if (x is Matrix4x4 a && y is Matrix4x4 b)
            {
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        if (Mathf.Abs(a[r, c] - b[r, c]) > 1e-4f) return 1;
                return 0;
            }
            return 1;
        }
    }
}
