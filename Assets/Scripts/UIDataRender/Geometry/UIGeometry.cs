using System.Collections.Generic;
using UnityEngine;

public class UIGeometry
{
    public struct VertexSlice
    {
        public int start;
        public int count;

        public VertexSlice(int start, int count)
        {
            this.start = start;
            this.count = count;
        }
    }

    public const int GROWTH = 1024 * 4;
    public Vector4[] uvs = new Vector4[GROWTH];
    public Vector3[] vertex = new Vector3[GROWTH];
    public Vector3[] drawVertex = new Vector3[GROWTH];
    public Color32[] colors = new Color32[GROWTH];
    public int[] indices = new int[GROWTH * 2];

    public int totalVertex;
    public int totalIndices;

    private static int Next = 0;
    private readonly GeometryBuffer buffer;
    private FreeListAllocator vertexAllocator;
    private FreeListAllocator indicesAllocator;

    public UIGeometry()
    {
        buffer = new GeometryBuffer(GROWTH, GROWTH * 2, GROWTH);
        Reset();
    }

    public MeshSlim Alloc(int vertexCount, int indiceCount)
    {
        if (!vertexAllocator.Alloc(vertexCount, out var vertexOffset))
        {
            vertexOffset = vertex.Length;
            buffer.EnsureVertexCapacity(vertexOffset + vertexCount);
            SyncBufferFields();
            vertexAllocator.AddFreeSlice(vertexOffset + vertexCount, vertex.Length - vertexOffset - vertexCount);
        }

        if (!indicesAllocator.Alloc(indiceCount, out var indicesOffset))
        {
            indicesOffset = indices.Length;
            buffer.EnsureIndexCapacity(indicesOffset + indiceCount);
            SyncBufferFields();
            indicesAllocator.AddFreeSlice(indicesOffset + indiceCount, indices.Length - indicesOffset - indiceCount);
        }

        return new MeshSlim
        {
            VertexOffset = vertexOffset,
            VertexCount = vertexCount,
            IndicesOffset = indicesOffset,
            IndicesCount = indiceCount,
            Index = Next++
        };
    }

    public void ReAlloc(int vertexCount, int indiceCount, ref MeshSlim mesh)
    {
        vertexAllocator.Release(mesh.VertexOffset, mesh.VertexCount);
        indicesAllocator.Release(mesh.IndicesOffset, mesh.IndicesCount);
        mesh = Alloc(vertexCount, indiceCount);
    }

    public void Release(MeshSlim mesh)
    {
        vertexAllocator.Release(mesh.VertexOffset, mesh.VertexCount);
        indicesAllocator.Release(mesh.IndicesOffset, mesh.IndicesCount);
    }

    public void Reset()
    {
        buffer.Reset();
        SyncBufferFields();
        vertexAllocator = new FreeListAllocator(vertex.Length);
        indicesAllocator = new FreeListAllocator(indices.Length);
        totalVertex = 0;
        totalIndices = 0;
        Next = 0;
    }

    private void SyncBufferFields()
    {
        uvs = buffer.uvs;
        vertex = buffer.vertex;
        drawVertex = buffer.drawVertex;
        colors = buffer.colors;
        indices = buffer.indices;
    }

    public LinkedList<VertexSlice> openVertexList => vertexAllocator.FreeList;
    public LinkedList<VertexSlice> openIndicesList => indicesAllocator.FreeList;
}
