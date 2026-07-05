namespace BFG
{
    public enum AllocatorKind
    {
        Stack,
        Data
    }

    /// <summary>
    /// Dynamic memory context. Allocators are organised in repeating blocks
    /// of (<see cref="dataDens"/> Data + <see cref="stackDens"/> Stack).
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
                if (pendingFree.Count > 0)
                    throw new InvalidOperationException(
                        "Cannot allocate: there are pending frees. Ensure all freed blocks are properly released before allocating.");

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

                int expectedTopIndex = Size - blockSize;
                if (logicalIndex == expectedTopIndex)
                    Size -= blockSize;
                else
                    pendingFree[logicalIndex] = blockSize;

                while (pendingFree.Count > 0)
                {
                    var last = pendingFree.Last();
                    int pendingIndex = last.Key;
                    int pendingSize = last.Value;

                    int newExpectedTop = Size - pendingSize;
                    if (pendingIndex == newExpectedTop)
                    {
                        pendingFree.Remove(pendingIndex);
                        Size -= pendingSize;
                    }
                    else
                        break;
                }
            }
        }

        /// <summary>Stack cells per block.</summary>
        public readonly int stackDens;
        /// <summary>Data cells per block.</summary>
        public readonly int dataDens;
        /// <summary>Total cells per block.</summary>
        public int BlockSize => stackDens + dataDens;

        private readonly BFAllocator[] allocators;
        /// <summary>Owning IR module.</summary>
        internal readonly BFIR IR;

        /// <summary>Current maximum sizes of the allocators (used by <see cref="BFRootDescriptor"/>).</summary>
        public int[] MaxSize => allocators.Select(x => x.MaxSize).ToArray();

        internal BFContext(BFIR ir, int stackDens = 1, int dataDens = 1)
        {
            int count = Enum.GetValues<AllocatorKind>().Length;
            allocators = new BFAllocator[count];
            allocators[(int)AllocatorKind.Stack] = new BFAllocator();
            allocators[(int)AllocatorKind.Data] = new BFAllocator();

            IR = ir;
            this.stackDens = stackDens;
            this.dataDens = dataDens;
        }

        /// <summary>
        /// Resolves a root (or transitional) descriptor to an absolute tape address.
        /// </summary>
        internal int Resolve(BFRootDescriptor descriptor, int logicalIndex)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));

            if (descriptor.Context != this)
                return descriptor.Context.Resolve(descriptor, logicalIndex);
            return ResolveAddr(descriptor.Allocator, descriptor.BaseIndex + logicalIndex);
        }


        private int ResolveAddr(AllocatorKind kind, int logicalIndex)
        {
            if (kind == AllocatorKind.Stack)
                return (logicalIndex / stackDens) * BlockSize + logicalIndex % stackDens + dataDens;
            else
                return (logicalIndex / dataDens) * BlockSize + logicalIndex % dataDens;
        }

        /// <summary>
        /// Allocates a block in the given allocator and returns a root descriptor.
        /// </summary>
        public BFRootDescriptor Alloc(AllocatorKind kind, int size = 1)
        {
            var allocator = allocators[(int)kind];
            int index = allocator.Alloc(size);
            return new BFRootDescriptor(this, kind, index, size);
        }

        /// <summary>
        /// Frees a previously allocated root descriptor.
        /// </summary>
        public void Free(BFRootDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (descriptor.Context != this)
                throw new InvalidOperationException("Descriptor belongs to a different context.");
            allocators[(int)descriptor.Allocator].Free(descriptor.BaseIndex, descriptor.Size);
        }
    }
}