using NUnit.Framework;

public class TestMeshSlim
{
    [Test]
    public void Dispose_Resets_AllFields()
    {
        var mesh = new MeshSlim
        {
            VertexOffset = 1,
            VertexCount = 2,
            IndicesOffset = 3,
            IndicesCount = 4,
            Index = 5
        };

        mesh.Dispose();

        Assert.AreEqual(-1, mesh.Index);
        Assert.AreEqual(0, mesh.VertexOffset);
        Assert.AreEqual(0, mesh.VertexCount);
        Assert.AreEqual(0, mesh.IndicesOffset);
        Assert.AreEqual(0, mesh.IndicesCount);
    }

    [Test]
    public void Equals_AndComparer_Ignore_Index_But_UseGeometryFields()
    {
        var left = new MeshSlim
        {
            VertexOffset = 10,
            VertexCount = 4,
            IndicesOffset = 20,
            IndicesCount = 6,
            Index = 1
        };
        var sameGeometryDifferentIndex = new MeshSlim
        {
            VertexOffset = 10,
            VertexCount = 4,
            IndicesOffset = 20,
            IndicesCount = 6,
            Index = 99
        };
        var differentGeometry = new MeshSlim
        {
            VertexOffset = 11,
            VertexCount = 4,
            IndicesOffset = 20,
            IndicesCount = 6,
            Index = 1
        };

        Assert.IsTrue(left.Equals(sameGeometryDifferentIndex));
        Assert.IsFalse(left.Equals(differentGeometry));
        Assert.IsTrue(left.Equals(left, sameGeometryDifferentIndex));
        Assert.IsFalse(left.Equals(left, differentGeometry));
        Assert.AreEqual(left.GetHashCode(left), left.GetHashCode(sameGeometryDifferentIndex));
    }
}
