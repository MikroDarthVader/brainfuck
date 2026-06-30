namespace BFGenerator
{
    public enum AllocatorKind
    {
        Stack,
        Data,
        Address
    }

    public abstract class BFContext
    {
        private class BFAllocator
        {
            private readonly SortedDictionary<int, int> activeBlocks = new SortedDictionary<int, int>();
            private readonly SortedDictionary<int, int> pendingFree = new SortedDictionary<int, int>();

            public int Size { get; private set; }

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

        private readonly BFAllocator[] allocators;
        public readonly BFIR IR;

        public BFContext(BFIR iR)
        {
            int count = Enum.GetValues<AllocatorKind>().Length;
            allocators = new BFAllocator[count];
            allocators[(int)AllocatorKind.Stack] = new BFAllocator();
            allocators[(int)AllocatorKind.Data] = new BFAllocator();
            allocators[(int)AllocatorKind.Address] = new BFAllocator();
            IR = iR;
        }

        public int Resolve(BFMemoryDescriptor descriptor, int logicalIndex)
        {
            if (descriptor.Context != this)
                throw new InvalidOperationException("Descriptor belongs to a different context.");
            return ResolveAddr(descriptor.Allocator, descriptor.BaseIndex + logicalIndex);
        }

        protected abstract int ResolveAddr(AllocatorKind kind, int logicalIndex);

        public BFMemoryDescriptor Alloc(AllocatorKind kind, int size = 1)
        {
            var allocator = allocators[(int)kind];
            int index = allocator.Alloc(size);
            return new BFMemoryDescriptor(this, kind, index, size);
        }

        public void Free(BFMemoryDescriptor descriptor)
        {
            if (descriptor.Context != this)
                throw new InvalidOperationException("Descriptor belongs to a different context.");
            allocators[(int)descriptor.Allocator].Free(descriptor.BaseIndex, descriptor.Size);
        }
    }

    public class BFMemoryDescriptor : IDisposable
    {
        public BFContext Context { get; }
        public AllocatorKind Allocator { get; }
        public int BaseIndex { get; }
        public int Size { get; }

        public BFMemoryDescriptor(BFContext context, AllocatorKind allocator, int baseIndex, int size)
        {
            Context = context;
            Allocator = allocator;
            BaseIndex = baseIndex;
            Size = size;
        }

        public BFCell this[int ind] => new BFCell(this, ind);

        public override bool Equals(object? obj)
        {
            if (obj == null || !(obj is BFMemoryDescriptor)) return false;
            BFMemoryDescriptor other = (BFMemoryDescriptor)obj;
            return Context.Equals(other.Context) &&
                (Allocator == other.Allocator) &&
                (BaseIndex == other.BaseIndex) &&
                (Size == other.Size);
        }

        public override int GetHashCode() => base.GetHashCode();

        public void Dispose() { Context.Free(this); }

        public BFCell[] ToArray()
        {
            BFCell[] array = new BFCell[Size];
            for(int i = 0; i < Size; i++)
                array[i] = this[i];
            return array;
        }
    }

}