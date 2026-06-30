using System.Linq;

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
        private readonly BFAllocator[] allocators;

        public BFContext()
        {
            int count = Enum.GetValues<AllocatorKind>().Length;
            allocators = new BFAllocator[count];
            allocators[(int)AllocatorKind.Stack] = new BFAllocator();
            allocators[(int)AllocatorKind.Data] = new BFAllocator();
            allocators[(int)AllocatorKind.Address] = new BFAllocator();
        }

        public BFAllocator this[AllocatorKind kind] => allocators[(int)kind];

        public int Resolve(BFMemoryDescriptor descriptor, int logicalIndex)
        {
            if (descriptor.Context != this)
                throw new InvalidOperationException("Descriptor belongs to a different context.");
            if(!allocators.Contains(descriptor.Allocator))
                throw new InvalidOperationException("Descriptor belongs to invalid allocator.");
            return ResolvePhysical((AllocatorKind)Array.IndexOf(allocators, descriptor.Allocator), descriptor.BaseIndex + logicalIndex);
        }

        protected abstract int ResolvePhysical(AllocatorKind kind, int logicalIndex);

        public BFMemoryDescriptor Alloc(AllocatorKind kind, int size)
        {
            var allocator = this[kind];
            int index = allocator.Alloc(size);
            return new BFMemoryDescriptor(this, allocator, index, size);
        }

        public void Free(BFMemoryDescriptor descriptor)
        {
            if (descriptor.Context != this)
                throw new InvalidOperationException("Descriptor belongs to a different context.");
            descriptor.Allocator.Free(descriptor.BaseIndex, descriptor.Size);
        }
    }

    public class BFAllocator
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

    public class BFMemoryDescriptor
    {
        public BFContext Context { get; }
        public BFAllocator Allocator { get; }
        public int BaseIndex { get; }
        public int Size { get; }

        public BFMemoryDescriptor(BFContext context, BFAllocator allocator, int baseIndex, int size)
        {
            Context = context;
            Allocator = allocator;
            BaseIndex = baseIndex;
            Size = size;
        }
    }
}
