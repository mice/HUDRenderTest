using NUnit.Framework;
using UnityEngine;

/// <summary>
/// TC-MTX-01: CalcMatrix for subNode directly under root with identity local transform returns Identity.
/// TC-MTX-02: CalcMatrix for multi-level nested transform equals root.worldToLocal * sub.localToWorld.
/// </summary>
public class TestCalcMatrix
{
    // TC-MTX-01
    // Note: CalcMatrix walks up from subNode until parent==root.
    // When subNode is a direct child of root with identity local transform,
    // the single TRS(zero, identity, one) == Matrix4x4.identity.
    [Test]
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
            Object.DestroyImmediate(childGO);
            Object.DestroyImmediate(rootGO);
        }
    }

    // TC-MTX-02
    [Test]
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
            Object.DestroyImmediate(subGO);
            Object.DestroyImmediate(midGO);
            Object.DestroyImmediate(rootGO);
        }
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
