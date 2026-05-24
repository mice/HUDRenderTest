using System;
using System.Collections.Generic;

public struct MeshSlim : IEquatable<MeshSlim>, IEqualityComparer<MeshSlim>
{
    public int VertexOffset;
    public int VertexCount;
    public int IndicesOffset;
    public int IndicesCount;
    public int Index;

    public void Dispose()
    {
        Index = -1;
        VertexOffset = 0;
        VertexCount = 0;
        IndicesOffset = 0;
        IndicesCount = 0;
    }

    public bool Equals(MeshSlim x, MeshSlim y)
    {
        return x.Equals(y);
    }

    public bool Equals(MeshSlim other)
    {
        return other.VertexOffset == VertexOffset
            && other.VertexCount == VertexCount
            && other.IndicesOffset == IndicesOffset
            && other.IndicesCount == IndicesCount;
    }

    public int GetHashCode(MeshSlim obj)
    {
        return (obj.VertexOffset, obj.VertexCount, obj.IndicesOffset, obj.IndicesCount).GetHashCode();
    }
}
