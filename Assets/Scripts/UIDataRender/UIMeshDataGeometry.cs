using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// 存放所有的信息,
/// UIMesh实际使用的vertexOffset,vertexCount,indexOffset,indexCount;
/// /// </summary>
public class UIMeshDataGeometry
{
    public struct VertexSlice
    {
        public int start;
        public int count;
    }

    public Vector4[] uvs = new Vector4[1024 * 256];
    public Vector3[] vertList = new Vector3[1024 * 256];
    public Color32[] colors = new Color32[1024 * 256];
    public int[] triangles = new int[1024 * 3 * 256];
    /// <summary>
    /// 总的Vertex大小;
    /// </summary>
    public int totalVertex;
    /// <summary>
    /// 总的indices大小
    /// </summary>
    public int totalIndices;

    private static int Next = 0;

    public UIMeshDataGeometry()
    {
        openVertexList.AddFirst(new LinkedListNode<VertexSlice>(new VertexSlice { start = 0, count = vertList.Count() }));
        openIndicesList.AddFirst(new LinkedListNode<VertexSlice>(new VertexSlice { start = 0, count = triangles.Count() }));
    }

    private int GetAvailableVertex(LinkedList<VertexSlice> data, int vertexCount)
    {
        var linkNodePerson = data.First;
        int vertexOffset = 0;
        while (linkNodePerson != null)
        {
            if (linkNodePerson.Value.count == vertexCount)
            {
                vertexOffset = linkNodePerson.Value.start;
                openVertexList.Remove(linkNodePerson);
                return vertexOffset;

            }
            else if (linkNodePerson.Value.count > vertexCount)
            {
                vertexOffset = linkNodePerson.Value.start;
                var tmpValue = new VertexSlice()
                {
                    start = vertexOffset + vertexCount,
                    count = linkNodePerson.Value.count - vertexCount,
                };
                linkNodePerson.Value = tmpValue;
                return vertexOffset;
            }
            else
            {

            }
            linkNodePerson = linkNodePerson.Next;
        }
        Assert.IsFalse(true, "not here");
        //TODO当没有找到的时候,需要处理.
        return vertexOffset;
    }

    private void ReleaseSlice(LinkedList<VertexSlice> data, int start, int vertexCount)
    {
        var linkNodePerson = data.First;

        while (linkNodePerson != null)
        {
            if (start < linkNodePerson.Value.start)
            {
                //merge
                if (start + vertexCount == linkNodePerson.Value.start)
                {
                    linkNodePerson.Value = new VertexSlice()
                    {
                        start = start,
                        count = vertexCount + linkNodePerson.Value.count
                    };
                }
                else
                {
                    data.AddBefore(linkNodePerson, new VertexSlice()
                    {
                        start = start,
                        count = vertexCount,
                    });
                }

                return;
            }
        }
        linkNodePerson = data.Last;
        //合并
        if (linkNodePerson.Value.start + linkNodePerson.Value.count == start)
        {
            linkNodePerson.Value = new VertexSlice()
            {
                start = linkNodePerson.Value.start,
                count = vertexCount + linkNodePerson.Value.count
            };
        }
        else
        {
            data.AddAfter(linkNodePerson, new VertexSlice()
            {
                start = start,
                count = vertexCount,
            });
        }
    }

    public MeshSlim Alloc(int vertexCount, int indiceCount)
    {
        int VertexOffset = GetAvailableVertex(openVertexList, vertexCount);
        int IndicesOfffset = GetAvailableVertex(openIndicesList, vertexCount);
        var mesh = new MeshSlim()
        {
#if DEBUG
            Index = Next++,
#endif
            VertexOffset = VertexOffset,
            VertexCount = vertexCount,
            IndicesOffset = IndicesOfffset,
            IndicesCount = indiceCount
        };

        return mesh;
    }

    /// <summary>
    /// 当进行重新分配的时候.
    /// </summary>
    /// <param name="vertexCount"></param>
    /// <param name="indiceCount"></param>
    /// <param name="mesh"></param>
    public void ReAlloc(int vertexCount, int indiceCount, ref MeshSlim mesh)
    {
        ReleaseSlice(openVertexList, mesh.VertexOffset, mesh.VertexCount);
        ReleaseSlice(openIndicesList, mesh.IndicesOffset, mesh.IndicesCount);
        mesh = Alloc(vertexCount, indiceCount);
    }

    /// <summary>
    /// 怎么处理.重复回收.
    /// </summary>
    /// <param name="mesh"></param>
    public void Release(MeshSlim mesh)
    {
        ReleaseSlice(openVertexList, mesh.VertexOffset, mesh.VertexCount);
        ReleaseSlice(openIndicesList, mesh.IndicesOffset, mesh.IndicesCount);
    }

    public LinkedList<VertexSlice> openVertexList = new LinkedList<VertexSlice>();
    public LinkedList<VertexSlice> openIndicesList = new LinkedList<VertexSlice>();
}
