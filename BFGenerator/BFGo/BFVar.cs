using System.Diagnostics;

namespace BFGo
{
    /// <summary>
    /// Has context and optional context shift.
    /// Provides cell access and block operations.
    /// </summary>
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public class BFVar : IDisposable
    {
        private BFContextMem? _owningMem;
        internal BFContextMem owningMem
        {
            get => Parent != null ? Parent.owningMem! : _owningMem!;
            private set => _owningMem = value;
        }

        /// <summary>Number of cells.</summary>
        public int Size { get; private protected set; }

        /// <summary>Root variable descriptor that owns the allocated memory block.</summary>
        public readonly BFVar? Parent;

        /// <summary>True if this descriptor directly owns the allocated context block.</summary>
        public bool IsOwner => Parent == null;

        /// <summary>True if the memory block is active and has not been disposed.</summary>
        public bool IsAlive { get; private set; }

        /// <summary>Owning environment.</summary>
        public BFGProgram env => owningMem.owningCxt.ir.env;

        /// <summary>Owning context.</summary>
        internal BFContext Context => owningMem.owningCxt;

        /// <summary>Start index inside the allocator.</summary>
        internal int BaseIndex { get; private protected set; }

        /// <summary>Optional context shift for cross-context access.</summary>
        internal ShiftDescriptor? cxtShift;

        /// <summary>True if this variable belongs to a foreign context and requires translation.</summary>
        internal bool isTransitional => cxtShift != null;

        internal string DebugDisplay
        {
            get
            {
                if (Context.ir is not BFIRDebugger debugger)
                    return "Debug mode disabled";

                var vals = new int[Size];
                for (int i = 0; i < Size; i++)
                    vals[i] = debugger.GetValue(debugger.ActiveContext.Resolve(this, i));
                return $"Size: {Size}, Values: ({string.Join(", ", vals)})";
            }
        }

        /// <summary>
        /// Creates a root descriptor from an allocator.
        /// </summary>
        internal BFVar(BFContextMem memMap, int baseIndex, int size, bool isMemOwner)
        {
            if (size <= 0) throw new ArgumentException("Size must be > 0");
            BaseIndex = baseIndex;
            Size = size;
            owningMem = memMap;

            cxtShift = null;
            Parent = null;
            IsAlive = isMemOwner;
        }

        /// <summary>
        /// Copy constructor with offset and optional size.
        /// </summary>
        internal BFVar(BFVar other, int offset = 0, int size = 0)
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
            owningMem = other.owningMem;
            cxtShift = other.cxtShift;

            Parent = other.Parent ?? other;
            IsAlive = false;
        }

        /// <summary>
        /// Private constructor for applying a context shift function.
        /// </summary>
        private BFVar(BFVar other, ShiftDescriptor cxtShift)
        {
            BaseIndex = other.BaseIndex;
            Size = other.Size;
            owningMem = other.owningMem;

            IsAlive = other.IsAlive;
            other.IsAlive = false;

            this.cxtShift = cxtShift;
        }

        /// <summary>Returns a new descriptor with an additional context shift function.</summary>
        internal BFVar ApplyShift(Func<int> shift) => new(this, new ShiftDescriptor(shift));
        /// <summary>Returns a new descriptor with an additional constant context shift.</summary>
        internal BFVar ApplyShift(int shift) => new(this, new ShiftDescriptor(shift));

        /// <summary>
        /// Returns a new root descriptor for a slice at the given offset with the specified size.
        /// </summary>
        internal BFVar Offset(int offset, int size = 0) =>
            new(this, offset, size);

        // ---- cell access ----
        public BFCell this[int index]
        {
            get
            {
                if (index < 0 || index >= Size)
                    throw new ArgumentOutOfRangeException(nameof(index), index, $"Structural Memory Violation: Requested index is out of bounds for the variable descriptor. Valid cell range is [0, {Size - 1}]. For safe boundary checks, use '{nameof(TryGet)}()' instead.");

                return new BFCell(this, index);
            }
        }

        public BFCell? TryGet(int index)
        {
            if (index >= Size || index < 0) return null;
            return new BFCell(this, index);
        }

        // ---- block operations ----
        /// <summary>Copies this block to one or more root descriptors.</summary>
        public void CopyTo(params BFVar[] to)
        {
            for (int i = 0; i < Size; i++)
            {
                var srcCell = this[i];
                foreach (var targetDesc in to)
                {
                    if (i < targetDesc.Size)
                    {
                        var tgtCell = targetDesc[i];
                        if (tgtCell != null)
                            srcCell.CopyTo(tgtCell);
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
                        targetDesc.TryGet(i)?.Init();
                }
            }
        }

        /// <summary>Moves this block (clearing source) to root descriptors.</summary>
        public void MoveTo(params BFVar[] to)
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
                    TryGet(i)?.Init();
            }
        }

        /// <summary>Sets every cell to zero.</summary>
        public BFVar Init(params int[] values)
        {
            for (int i = 0; i < Size; i++)
                this[i].Init(i < values.Length ? values[i] : 0);

            return this;
        }

        public BFVar While(Action code)
        {
            for (int i = 0; i < Size; i++)
                this[i].While(code);

            return this;
        }

        public BFVar If(Action code)
        {
            for (int i = 0; i < Size; i++)
                this[i].If(code);

            return this;

        }

        public BFVar Not()
        {
            for (int i = 0; i < Size; i++)
                this[i].Not();

            return this;
        }

        public BFVar Read()
        {
            for (int i = 0; i < Size; i++)
                this[i].Read();

            return this;
        }

        public BFVar Print()
        {
            for (int i = 0; i < Size; i++)
                this[i].Print();

            return this;
        }

        /// <summary>Converts to array of cells.</summary>
        public BFCell[] ToArray()
        {
            var array = new BFCell[Size];
            for (int i = 0; i < Size; i++)
                array[i] = this[i];
            return array;
        }

        /// <summary>
        /// Replaces the contents of this descriptor with those of <paramref name="to"/>.
        /// Used to rebind user descriptors after a context switch.
        /// </summary>
        public void Rebind(BFVar to)
        {
            Dispose();

            BaseIndex = to.BaseIndex;
            Size = to.Size;
            owningMem = to.owningMem;
            cxtShift = to.cxtShift;
            IsAlive = to.IsAlive;

            to.IsAlive = false;
        }

        public void Dispose()
        {
            if (!IsAlive)
                return;

            Context.Free(this);
            IsAlive = false;
            GC.SuppressFinalize(this);
        }

        public override bool Equals(object? obj) =>
            obj is BFVar other &&
            owningMem.GetType() == other.owningMem.GetType() &&
            BaseIndex == other.BaseIndex &&
            Size == other.Size &&
            Equals(other.cxtShift, cxtShift);

        public override int GetHashCode() => HashCode.Combine(typeof(BFVar), owningMem.GetType(), BaseIndex, Size, cxtShift);
    }
}