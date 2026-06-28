namespace BFGenerator
{
    public abstract class BFContext
    {
        public readonly BFAllocator stack, data, address;

        public BFContext()
        {
            stack = new BFAllocator();
            data = new BFAllocator();
            address = new BFAllocator();
        }

        public int Resolve(BFAllocator alloc, int ind)
        {
            if (stack.Equals(alloc))
                return ResolveStack(ind);
            if (data.Equals(alloc))
                return ResolveData(ind);
            if (address.Equals(alloc))
                return ResolveAddress(ind);

            throw new Exception("undefined allocator");
        }

        public abstract int ResolveStack(int ind);
        public abstract int ResolveData(int ind);
        public abstract int ResolveAddress(int ind);
    }

    public class BFAllocator
    {
        private int size;
        private readonly SortedDictionary<int, int> activeBlocks = new SortedDictionary<int, int>();
        private readonly SortedDictionary<int, int> pendingFree = new SortedDictionary<int, int>();

        public int Size => size;
        public int ActiveBlockCount => activeBlocks.Count;

        public int Alloc(int allocSize)
        {
            if (pendingFree.Count > 0)
                throw new InvalidOperationException(
                    "Cannot allocate: there are pending frees. Ensure all freed blocks are properly released before allocating.");

            int index = size;
            size += allocSize;
            activeBlocks[index] = allocSize;
            return index;
        }

        public void Free(int logicalIndex, int blockSize)
        {
            if (!activeBlocks.TryGetValue(logicalIndex, out int actualSize))
                throw new InvalidOperationException(
                    $"Cannot free block at index {logicalIndex}: no such active block.");

            if (actualSize != blockSize)
                throw new InvalidOperationException(
                    $"Size mismatch: block at {logicalIndex} has size {actualSize}, but tried to free with size {blockSize}.");

            activeBlocks.Remove(logicalIndex);

            int expectedTopIndex = size - blockSize;
            if (logicalIndex == expectedTopIndex)
                size -= blockSize;
            else
                pendingFree[logicalIndex] = blockSize;

            while (pendingFree.Count > 0)
            {
                var last = pendingFree.Last();
                int pendingIndex = last.Key;
                int pendingSize = last.Value;

                int newExpectedTop = size - pendingSize;
                if (pendingIndex == newExpectedTop)
                {
                    pendingFree.Remove(pendingIndex);
                    size -= pendingSize;
                }
                else
                    break;
            }
        }
    }
}
