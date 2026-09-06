namespace BFGo
{
    internal class BFContextMem(BFContext owningCxt)
    {
        public readonly BFContext owningCxt = owningCxt;
    }

    /// <summary>
    /// Best‑fit memory allocator for linear address space.
    /// Supports arbitrary free order; coalesces adjacent free blocks.
    /// </summary>
    internal class BFAllocator : BFContextMem
    {
        private readonly SortedDictionary<int, int> freeBlocks = []; // key = start, value = size
        private int _size;
        internal int MaxSize { get; private set; }
        internal int Size => _size;

        // ---- Public API ----

        internal BFAllocator(BFContext owner) : base(owner) { }

        /// <summary>Allocates a contiguous block of at least <paramref name="requestSize"/> cells.</summary>
        /// <returns>Start address of the allocated block.</returns>
        internal int Alloc(int requestSize)
        {
            if (requestSize <= 0)
                throw new ArgumentException("Request size must be positive.", nameof(requestSize));

            var best = FindBestFit(requestSize);
            if (best.HasValue)
                return AllocateFromBlock(best.Value.start, best.Value.size, requestSize);

            // No free block – extend the area
            int newStart = _size;
            _size += requestSize;
            if (_size > MaxSize) MaxSize = _size;
            return newStart;
        }

        /// <summary>
        /// Returns a block to the free pool.
        /// Validates bounds and detects overlaps (e.g. double free).
        /// Merges with adjacent free blocks if any.
        /// </summary>
        internal void Free(int start, int size)
        {
            if (size <= 0)
                throw new ArgumentException("Block size must be positive.", nameof(size));

            ValidateFreeBlock(start, size);
            freeBlocks.Add(start, size);
            CoalesceFreeBlocks();
            ShrinkIfPossible();
        }

        // ---- Private helpers ----

        /// <summary>Finds the smallest free block that can accommodate the request.</summary>
        private (int start, int size)? FindBestFit(int requestSize)
        {
            int bestStart = -1;
            int bestSize = int.MaxValue;

            foreach (var kvp in freeBlocks)
            {
                int start = kvp.Key;
                int size = kvp.Value;
                if (size >= requestSize && size < bestSize)
                {
                    bestSize = size;
                    bestStart = start;
                    if (size == requestSize) // perfect match
                        break;
                }
            }

            return bestStart != -1 ? (bestStart, bestSize) : null;
        }

        /// <summary>Removes the chosen block and returns the remainder if any.</summary>
        private int AllocateFromBlock(int start, int size, int requestSize)
        {
            freeBlocks.Remove(start);
            int remainder = size - requestSize;
            if (remainder > 0)
                freeBlocks.Add(start + requestSize, remainder);
            return start;
        }

        /// <summary>Performs bounds and overlap checks before inserting a free block.</summary>
        private void ValidateFreeBlock(int start, int size)
        {
            // Bounds check
            if (start < 0 || start + size > _size)
                throw new InvalidOperationException(
                    $"Block [{start}, {start + size}) outside allocated area [0, {_size}).");

            // Find neighbouring free blocks
            var (prevKey, nextKey) = FindNeighbourKeys(start);

            // Overlap with previous block
            if (prevKey.HasValue)
            {
                int prevStart = prevKey.Value;
                int prevSize = freeBlocks[prevStart];
                if (start < prevStart + prevSize)
                    throw new InvalidOperationException(
                        $"Block [{start}, {start + size}) overlaps with free block [{prevStart}, {prevStart + prevSize}).");
            }

            // Overlap with next block
            if (nextKey.HasValue)
            {
                int nextStart = nextKey.Value;
                int nextSize = freeBlocks[nextStart];
                if (start + size > nextStart)
                    throw new InvalidOperationException(
                        $"Block [{start}, {start + size}) overlaps with free block [{nextStart}, {nextStart + nextSize}).");
            }
        }

        /// <summary>Finds the keys of the free blocks immediately before and after the given start.</summary>
        private (int? prev, int? next) FindNeighbourKeys(int start)
        {
            int? prev = null;
            int? next = null;

            // Since SortedDictionary is ordered, we can iterate once.
            foreach (int key in freeBlocks.Keys)
            {
                if (key < start)
                    prev = key;
                else if (key > start)
                {
                    next = key;
                    break;
                }
                // if key == start, it will be caught by overlap check later
            }

            return (prev, next);
        }

        /// <summary>Coalesces adjacent free blocks by rebuilding the dictionary.</summary>
        private void CoalesceFreeBlocks()
        {
            if (freeBlocks.Count == 0) return;

            var merged = new List<(int start, int size)>(freeBlocks.Count);
            foreach (var kvp in freeBlocks)
            {
                int curStart = kvp.Key;
                int curSize = kvp.Value;
                if (merged.Count > 0 && merged[^1].start + merged[^1].size == curStart)
                {
                    var (start, size) = merged[^1];
                    merged[^1] = (start, size + curSize);
                }
                else
                {
                    merged.Add((curStart, curSize));
                }
            }

            freeBlocks.Clear();
            foreach (var (start, size) in merged)
                freeBlocks.Add(start, size);
        }

        /// <summary>Shrinks the allocation size if the last block ends at _size.</summary>
        private void ShrinkIfPossible()
        {
            if (freeBlocks.Count == 0) return;

            int lastStart = freeBlocks.Keys.Last();
            int lastSize = freeBlocks[lastStart];
            if (lastStart + lastSize == _size)
            {
                _size = lastStart;
                freeBlocks.Remove(lastStart);
            }
        }
    }

    /// <summary>
    /// Memory context. Allocators are organised in repeating blocks
    /// Logical indices are mapped linearly into this repeating layout.
    /// </summary>
    internal abstract class BFContext
    {
        internal readonly BFIR ir;
        internal BFGCfg cfg => ir.cfg;
        internal readonly int ID;

        protected readonly BFAllocator allocator;

        internal BFContext(BFIR ir, int ID)
        {
            this.ir = ir;
            this.ID = ID;
            allocator = new BFAllocator(this);
        }

        /// <summary>
        /// Resolves a root (or transitional) descriptor to an absolute tape address.
        /// </summary>
        internal int Resolve(BFVar descriptor, int logicalIndex)
        {
            if (descriptor.owningMem.owningCxt != this)
            {
                if (descriptor.isTransitional)
                    return descriptor.owningMem.owningCxt.Resolve(descriptor, logicalIndex);
                else
                    throw new InvalidOperationException($"Cannot access descriptor " +
                        $"from context '{descriptor.owningMem.owningCxt.ID}' in current context '{ID}'.");
            }

            return MemMap(descriptor.owningMem, descriptor.BaseIndex + logicalIndex);
        }

        protected abstract int MemMap(BFContextMem memMap, int logicalIndex);

        /// <summary>
        /// Allocates a block in the given allocator and returns a root descriptor.
        /// </summary>
        internal BFVar Alloc(int size = 1)
        {
            int index = allocator.Alloc(size);
            return new BFVar(allocator, index, size, true);
        }

        /// <summary>
        /// Frees a previously allocated root descriptor.
        /// </summary>
        internal void Free(BFVar descriptor)
        {
            if (descriptor.owningMem != allocator)
                throw new InvalidOperationException("Descriptor belongs to a different context.");
            allocator.Free(descriptor.BaseIndex, descriptor.Size);
        }
    }

    internal class BFStaticContext : BFContext
    {
        internal BFStaticContext(BFIR ir, int ID) : base(ir, ID) { }

        /// <summary>Current maximum memory size</summary>
        public int MaxSize => allocator.MaxSize;

        protected override int MemMap(BFContextMem memMap, int logicalIndex)
        {
            if (memMap != allocator)
                throw new InvalidOperationException("Descriptor belongs to a different context.");
            return logicalIndex;
        }
    }

    internal class BFDynamicContext : BFContext
    {
        private readonly BFContextMem data;

        internal BFDynamicContext(BFIR ir, int ID) : base(ir, ID)
        {
            data = new BFContextMem(this);
        }

        protected override int MemMap(BFContextMem memMap, int pos)
        {
            if (memMap == allocator)
                return (pos / cfg.stackDens) * cfg.BlockSize + pos % cfg.stackDens + cfg.dataDens;
            else if (memMap == data)
                return (pos / cfg.dataDens) * cfg.BlockSize + pos % cfg.dataDens;

            throw new InvalidOperationException("Descriptor belongs to a different context.");
        }

        internal BFVar GetData(int size = 1, int pos = 0)
        {
            return new BFVar(data, pos, size, false);
        }
    }
}