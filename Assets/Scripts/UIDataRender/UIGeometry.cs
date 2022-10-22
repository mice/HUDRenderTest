using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// 存放所有的信息,
/// UIMesh实际使用的vertexOffset,vertexCount,indexOffset,indexCount;
/// /// </summary>
public class UIGeometry
{
    public struct VertexSlice
    {
        public int start;
        public int count;

        public VertexSlice(int start,int count)
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

    /// <summary>
    /// 总的Vertex大小;
    /// </summary>
    public int totalVertex;
    /// <summary>
    /// 总的indices大小
    /// </summary>
    public int totalIndices;

    private static int Next = 0;

    public UIGeometry()
    {
        openVertexList.AddFirst(new VertexSlice(0, vertex.Count()));
        openIndicesList.AddFirst(new VertexSlice(0, indices.Count()));
    }

    private bool GetAvailableVertex(LinkedList<VertexSlice> data, int vertexCount,out int vertexOffset)
    {
        var linkNodePerson = data.First;
       
        while (linkNodePerson != null)
        {
            if (linkNodePerson.Value.count == vertexCount)
            {
                vertexOffset = linkNodePerson.Value.start;
                data.Remove(linkNodePerson);
                return true;

            }
            else if (linkNodePerson.Value.count > vertexCount)
            {
                vertexOffset = linkNodePerson.Value.start;
                linkNodePerson.Value = new VertexSlice(vertexOffset + vertexCount, linkNodePerson.Value.count - vertexCount);
                return true;
            }
            else
            {

            }
            linkNodePerson = linkNodePerson.Next;
        }
        vertexOffset = 0;
        //TODO当没有找到的时候,需要处理.
        return false;
    }


    /// <summary>
    /// 情况1:在头只前,一种直接插入
    /// 情况2:在头之前,可以和头合并
    /// 情况3:
    /// 和前面的节点连续和后面的不连续, 和前面的节点合并
    /// 和前面的节点不连续,和后面的节点也不连续, 插入到前面的节点之后
    /// 和前面的节点不连续和后面的节点连续,  和后面的节点合并
    /// 和前面的节点连续和后面节点连续.   移除后面的节点,合并到前面的节点
    ///
    /// </summary>
    /// <param name="data"></param>
    /// <param name="start"></param>
    /// <param name="vertexCount"></param>
    private void ReleaseSlice(LinkedList<VertexSlice> data, int start, int vertexCount)
    {
        var linkNodePerson = data.First;

        //首先去确定是否要加在头部
        if(linkNodePerson != null)
        {
            if((start + vertexCount) == linkNodePerson.Value.start) //case 2
            {
                linkNodePerson.Value = new VertexSlice(start, vertexCount + linkNodePerson.Value.count);
            }else if ((start + vertexCount) < linkNodePerson.Value.start)//case 1
            {
                data.AddBefore(linkNodePerson, new VertexSlice(start, vertexCount));
            }
            else
            {
                //他有个循环处理机制.
                while (linkNodePerson != null)
                {
                    var nextNode = linkNodePerson.Next;
                    bool connectbefore = start == linkNodePerson.Value.start + linkNodePerson.Value.count;
                    bool connectAfter = nextNode != null && (start + vertexCount) == nextNode.Value.start;

                    if(connectbefore && connectAfter)
                    {
                        linkNodePerson.Value = new VertexSlice(linkNodePerson.Value.start, vertexCount + linkNodePerson.Value.count + nextNode.Value.count);
                        data.Remove(nextNode);
                        break;
                    }else if(connectbefore && !connectAfter)
                    {
                        linkNodePerson.Value = new VertexSlice(linkNodePerson.Value.start, vertexCount + linkNodePerson.Value.count);
                        break;
                    }else if(!connectbefore && connectAfter)
                    {
                        nextNode.Value = new VertexSlice(start,vertexCount + nextNode.Value.count);
                        break;
                    }else if (start>linkNodePerson.Value.start+ linkNodePerson.Value.count && 
                        (nextNode==null || (start + vertexCount)>nextNode.Value.start))
                    {
                        data.AddAfter(linkNodePerson,new VertexSlice(start,vertexCount));
                        break;
                    }
                    linkNodePerson = linkNodePerson.Next;
                }
            }
        }

    }

    public MeshSlim Alloc(int vertexCount, int indiceCount)
    {
        int VertexOffset;
        if(!GetAvailableVertex(openVertexList, vertexCount,out VertexOffset))
        {
            var oldCount = vertex.Length;
            Array.Resize(ref vertex, oldCount + GROWTH);

            VertexOffset = oldCount;
            //oldCount[VertexCount|]
            openVertexList.AddLast(new VertexSlice()
            {
                start = oldCount + vertexCount,
                count = GROWTH - vertexCount,
            });
        }

        int IndicesOfffset;
        if(!GetAvailableVertex(openIndicesList, indiceCount,out IndicesOfffset))
        {
            var oldCount = indices.Length;
            Array.Resize(ref indices, oldCount + GROWTH );
            IndicesOfffset = oldCount;

            openIndicesList.AddLast(new VertexSlice() { 
                start = oldCount + indiceCount,
                count = GROWTH - indiceCount,
            }
            );
        }

        var mesh = new MeshSlim()
        {
            VertexOffset = VertexOffset,
            VertexCount = vertexCount,
            IndicesOffset = IndicesOfffset,
            IndicesCount = indiceCount,
            Index = Next++
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
