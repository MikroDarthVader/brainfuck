namespace BFG
{
    /// <summary>
    /// Root descriptor – has context and optional context shift.
    /// Provides cell access and block operations.
    /// </summary>
    public class BFRootDescriptor : IDisposable
    {
        /// <summary>Owning context.</summary>
        public BFContext Context { get; private set; }
        /// <summary>Allocator kind (Stack or Data).</summary>
        public AllocatorKind Allocator { get; private set; }
        /// <summary>Start index inside the allocator.</summary>
        internal int BaseIndex { get; private protected set; }
        /// <summary>Number of cells.</summary>
        public int Size { get; private protected set; }
        /// <summary>Optional context shift for cross-context access.</summary>
        internal Func<int>? cxtShift;

        internal bool isTransitional
        {
            get => cxtShift != null;
            set => cxtShift = value ? (cxtShift ?? (() => 0)) : null;
        }

        /// <summary>
        /// Creates a root descriptor from an allocator.
        /// </summary>
        internal BFRootDescriptor(BFContext context, AllocatorKind allocator, int baseIndex, int size)
        {
            if (size <= 0) throw new ArgumentException("Size must be > 0");
            BaseIndex = baseIndex;
            Size = size;
            Context = context;
            Allocator = allocator;
            cxtShift = null;
        }

        /// <summary>
        /// Copy constructor with offset and optional size.
        /// </summary>
        internal BFRootDescriptor(BFRootDescriptor other, int offset = 0, int size = 0)
        {
            if (size < 0) throw new ArgumentException("Size must be >= 0");

            int maxAvailable = other.Size - offset;
            if (maxAvailable <= 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset exceeds source descriptor size.");

            size = size == 0 ? maxAvailable : size;
            if (size <= 0 || size > maxAvailable)
                throw new ArgumentOutOfRangeException(nameof(size),
                    $"Requested size {size} is out of available range [1, {maxAvailable}].");

            BaseIndex = other.BaseIndex + offset;
            Size = size;
            Context = other.Context;
            Allocator = other.Allocator;
            cxtShift = other.cxtShift;
        }

        /// <summary>
        /// Private constructor for applying a context shift function.
        /// </summary>
        private BFRootDescriptor(BFRootDescriptor other, Func<int> cxtShift)
        {
            BaseIndex = other.BaseIndex;
            Size = other.Size;
            Context = other.Context;
            Allocator = other.Allocator;
            this.cxtShift = cxtShift;
        }

        /// <summary>Returns a new descriptor with an additional context shift function.</summary>
        internal BFRootDescriptor ApplyShift(Func<int> shift) => new BFRootDescriptor(this, shift);
        /// <summary>Returns a new descriptor with an additional constant context shift.</summary>
        internal BFRootDescriptor ApplyShift(int shift) => new BFRootDescriptor(this, () => shift);

        /// <summary>
        /// Returns a new root descriptor for a slice at the given offset with the specified size.
        /// </summary>
        internal BFRootDescriptor Offset(int offset, int size) =>
            new BFRootDescriptor(this, offset, size);

        // ---- cell access ----
        public BFCell? this[int index]
        {
            get
            {
                if (index >= Size) return null;
                return new BFCell(this, index);
            }
        }

        // ---- block operations ----
        /// <summary>Copies this block to one or more root descriptors.</summary>
        public void CopyTo(params BFRootDescriptor[] to)
        {
            for (int i = 0; i < Size; i++)
            {
                var srcCell = this[i];
                if (srcCell == null) continue;
                foreach (var targetDesc in to)
                {
                    if (i < targetDesc.Size)
                    {
                        var tgtCell = targetDesc[i];
                        if (tgtCell != null) srcCell.CopyTo(tgtCell);
                    }
                }
            }
            // Zero-fill remaining cells in targets larger than source
            int maxSize = 0;
            foreach (var t in to) if (t.Size > maxSize) maxSize = t.Size;
            for (int i = Size; i < maxSize; i++)
            {
                foreach (var targetDesc in to)
                {
                    if (i < targetDesc.Size)
                        targetDesc[i]?.Init();
                }
            }
        }

        /// <summary>Moves this block (clearing source) to root descriptors.</summary>
        public void MoveTo(params BFRootDescriptor[] to)
        {
            int maxSize = 0;
            foreach (var t in to) if (t.Size > maxSize) maxSize = t.Size;

            for (int i = 0; i < Math.Max(Size, maxSize); i++)
            {
                var srcCell = i < Size ? this[i] : null;
                foreach (var targetDesc in to)
                {
                    if (i < targetDesc.Size)
                    {
                        var tgtCell = targetDesc[i];
                        if (tgtCell == null) continue;
                        if (srcCell != null)
                            srcCell.MoveTo(tgtCell);
                        else
                            tgtCell.Init();
                    }
                }
            }
            // Clear remaining source cells that were not moved
            if (Size > maxSize)
            {
                for (int i = maxSize; i < Size; i++)
                    this[i]?.Init();
            }
        }

        /// <summary>Sets every cell to zero.</summary>
        public void Init()
        {
            for (int i = 0; i < Size; i++)
                this[i]?.Init();
        }

        /// <summary>Converts to array of cells.</summary>
        public BFCell[] ToArray()
        {
            var array = new BFCell[Size];
            for (int i = 0; i < Size; i++)
                array[i] = this[i]!;
            return array;
        }

        /// <summary>
        /// Replaces the contents of this descriptor with those of <paramref name="source"/>.
        /// Used to rebind user descriptors after a context switch.
        /// </summary>
        internal void Rebind(BFRootDescriptor source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            BaseIndex = source.BaseIndex;
            Size = source.Size;
            Context = source.Context;
            Allocator = source.Allocator;
            cxtShift = source.cxtShift;
        }

        public void Dispose()
        {
            Context.Free(this);
        }

        public override bool Equals(object? obj) =>
            obj is BFRootDescriptor other &&
            Context == other.Context &&
            Allocator == other.Allocator &&
            BaseIndex == other.BaseIndex &&
            Size == other.Size;

        public override int GetHashCode() => HashCode.Combine(Context, Allocator, BaseIndex, Size);
    }
}