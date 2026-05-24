using System.Collections.Generic;

public sealed class FreeListAllocator
{
    public LinkedList<UIGeometry.VertexSlice> FreeList { get; } = new LinkedList<UIGeometry.VertexSlice>();

    public FreeListAllocator(int capacity)
    {
        if (capacity > 0)
        {
            FreeList.AddFirst(new UIGeometry.VertexSlice(0, capacity));
        }
    }

    public bool Alloc(int count, out int offset)
    {
        var node = FreeList.First;
        while (node != null)
        {
            if (node.Value.count == count)
            {
                offset = node.Value.start;
                FreeList.Remove(node);
                return true;
            }

            if (node.Value.count > count)
            {
                offset = node.Value.start;
                node.Value = new UIGeometry.VertexSlice(offset + count, node.Value.count - count);
                return true;
            }

            node = node.Next;
        }

        offset = 0;
        return false;
    }

    public void AddFreeSlice(int start, int count)
    {
        if (count <= 0)
        {
            return;
        }

        Release(start, count);
    }

    public void Release(int start, int count)
    {
        if (count <= 0)
        {
            return;
        }

        var node = FreeList.First;
        if (node == null)
        {
            FreeList.AddFirst(new UIGeometry.VertexSlice(start, count));
            return;
        }

        if (start + count == node.Value.start)
        {
            node.Value = new UIGeometry.VertexSlice(start, count + node.Value.count);
            return;
        }

        if (start + count < node.Value.start)
        {
            FreeList.AddBefore(node, new UIGeometry.VertexSlice(start, count));
            return;
        }

        while (node != null)
        {
            var nextNode = node.Next;
            var connectsBefore = start == node.Value.start + node.Value.count;
            var connectsAfter = nextNode != null && start + count == nextNode.Value.start;

            if (connectsBefore && connectsAfter)
            {
                node.Value = new UIGeometry.VertexSlice(node.Value.start, count + node.Value.count + nextNode.Value.count);
                FreeList.Remove(nextNode);
                return;
            }

            if (connectsBefore)
            {
                node.Value = new UIGeometry.VertexSlice(node.Value.start, count + node.Value.count);
                return;
            }

            if (connectsAfter)
            {
                nextNode.Value = new UIGeometry.VertexSlice(start, count + nextNode.Value.count);
                return;
            }

            if (start > node.Value.start + node.Value.count && (nextNode == null || start + count < nextNode.Value.start))
            {
                FreeList.AddAfter(node, new UIGeometry.VertexSlice(start, count));
                return;
            }

            node = node.Next;
        }
    }
}
