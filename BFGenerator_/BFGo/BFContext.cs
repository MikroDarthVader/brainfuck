namespace BFGo
{
    public enum AllocatorKind
    {
        Stack,
        Data
    }

    /// <summary>
    /// Memory context. Allocators are organised in repeating blocks
    /// Logical indices are mapped linearly into this repeating layout.
    /// </summary>
    public class BFContext
    {
        private class BFAllocator
        {
            private readonly SortedDictionary<int, int> activeBlocks = [];
            private readonly SortedDictionary<int, int> pendingFree = [];

            private int _size;
            public int MaxSize { get; private set; }
            public int Size
            {
                get => _size;
                private set
                {
                    _size = value;
                    if (_size > MaxSize)
                        MaxSize = _size;
                }
            }

            public int Alloc(int allocSize)
            {
                if (allocSize <= 0)
                    throw new ArgumentException("Allocation size must be > 0");

                foreach (var kvp in pendingFree)
                {
                    int freeIndex = kvp.Key;
                    int freeSize = kvp.Value;

                    if (freeSize >= allocSize)
                    {
                        pendingFree.Remove(freeIndex);

                        if (freeSize > allocSize)
                        {
                            int remainderIndex = freeIndex + allocSize;
                            int remainderSize = freeSize - allocSize;
                            pendingFree[remainderIndex] = remainderSize;
                        }

                        activeBlocks[freeIndex] = allocSize;
                        return freeIndex;
                    }
                }

                int index = Size;
                Size += allocSize;
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

                pendingFree[logicalIndex] = blockSize;

                var keys = pendingFree.Keys.ToList();
                for (int i = 0; i < keys.Count - 1; i++)
                {
                    int currentIdx = keys[i];
                    int currentSize = pendingFree[currentIdx];
                    int nextIdx = keys[i + 1];

                    if (currentIdx + currentSize == nextIdx)
                    {
                        pendingFree[currentIdx] = currentSize + pendingFree[nextIdx];
                        pendingFree.Remove(nextIdx);
                        keys.RemoveAt(i + 1);
                        i--;
                    }
                }

                while (pendingFree.Count > 0)
                {
                    var last = pendingFree.Last();
                    int pendingIndex = last.Key;
                    int pendingSize = last.Value;

                    if (pendingIndex + pendingSize == Size)
                    {
                        pendingFree.Remove(pendingIndex);
                        Size -= pendingSize;
                    }
                    else
                        break;
                }
            }
        }

        public readonly BFG env;

        private readonly BFAllocator[] allocators;

        public readonly int ID;

        /// <summary>Current maximum sizes of the allocators .</summary>
        public int MaxSize => env.BlockSize * allocators.Max(x => x.MaxSize);

        internal BFContext(BFG env, int ID)
        {
            int count = Enum.GetValues<AllocatorKind>().Length;
            allocators = new BFAllocator[count];
            allocators[(int)AllocatorKind.Stack] = new BFAllocator();
            allocators[(int)AllocatorKind.Data] = new BFAllocator();

            this.env = env;
            this.ID = ID;
        }

        /// <summary>
        /// Resolves a root (or transitional) descriptor to an absolute tape address.
        /// </summary>
        internal int Resolve(BFVar descriptor, int logicalIndex)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            if (descriptor.Context != this)
                return descriptor.Context.Resolve(descriptor, logicalIndex);
            return ResolveAddr(descriptor.Allocator, descriptor.BaseIndex + logicalIndex);
        }

        private int ResolveAddr(AllocatorKind kind, int logicalIndex)
        {
            if (kind == AllocatorKind.Stack)
                return (logicalIndex / env.stackDens) * env.BlockSize + logicalIndex % env.stackDens + env.dataDens;
            else
                return (logicalIndex / env.dataDens) * env.BlockSize + logicalIndex % env.dataDens;
        }

        /// <summary>
        /// Allocates a block in the given allocator and returns a root descriptor.
        /// </summary>
        public BFVar Alloc(AllocatorKind kind, int size = 1)
        {
            var allocator = allocators[(int)kind];
            int index = allocator.Alloc(size);
            return new BFVar(this, kind, index, size);
        }

        /// <summary>
        /// Frees a previously allocated root descriptor.
        /// </summary>
        public void Free(BFVar descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Context != this)
                throw new InvalidOperationException("Descriptor belongs to a different context.");
            allocators[(int)descriptor.Allocator].Free(descriptor.BaseIndex, descriptor.Size);
        }
    }
}