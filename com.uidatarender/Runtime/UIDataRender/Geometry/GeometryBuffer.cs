using System;
using UnityEngine;

public sealed class GeometryBuffer
{
    private readonly int growth;
    private readonly int initialVertexCapacity;
    private readonly int initialIndexCapacity;

    public Vector4[] uvs;
    public Vector3[] vertex;
    public Vector3[] drawVertex;
    public Color32[] colors;
    public int[] indices;

    public GeometryBuffer(int vertexCapacity, int indexCapacity, int growth)
    {
        this.growth = growth;
        initialVertexCapacity = vertexCapacity;
        initialIndexCapacity = indexCapacity;
        Reset();
    }

    public void Reset()
    {
        uvs = new Vector4[initialVertexCapacity];
        vertex = new Vector3[initialVertexCapacity];
        drawVertex = new Vector3[initialVertexCapacity];
        colors = new Color32[initialVertexCapacity];
        indices = new int[initialIndexCapacity];
    }

    public bool EnsureVertexCapacity(int minCapacity)
    {
        if (minCapacity <= vertex.Length)
        {
            return false;
        }

        var newCapacity = GrowCapacity(vertex.Length, minCapacity);
        Array.Resize(ref uvs, newCapacity);
        Array.Resize(ref vertex, newCapacity);
        Array.Resize(ref drawVertex, newCapacity);
        Array.Resize(ref colors, newCapacity);
        return true;
    }

    public bool EnsureIndexCapacity(int minCapacity)
    {
        if (minCapacity <= indices.Length)
        {
            return false;
        }

        Array.Resize(ref indices, GrowCapacity(indices.Length, minCapacity));
        return true;
    }

    private int GrowCapacity(int currentCapacity, int minCapacity)
    {
        var delta = minCapacity - currentCapacity;
        var steps = (delta + growth - 1) / growth;
        return currentCapacity + steps * growth;
    }
}
