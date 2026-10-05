using System.Diagnostics;

namespace BFGo
{
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public class BFVar
    {
        internal readonly BFMemView Space;
        public readonly int Size;
        internal readonly int Addr;

        internal string DebugDisplay
        {
            get
            {
                if (Space.Context.ir is not BFIRDebugger debugger)
                    return "Debug mode disabled";

                var vals = debugger.GetVarValues(this);
                return $"Size: {Size}, Values: ({string.Join(", ", vals)})";
            }
        }

        internal BFVar(BFMemView mem, int addr, int size = 1)
        {
            if (size <= 0) throw new ArgumentException("Size must be > 0");
            Addr = addr;
            Size = size;
            Space = mem;
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

            Addr = other.Addr + offset;
            Size = size;
            Space = other.Space;
        }

        /// <summary>
        /// Returns a new descriptor for a slice at the given offset with the specified size.
        /// </summary>
        internal BFVar Offset(int offset, int size = 0) =>
            new(this, offset, size);

        // ---- cell access ----
        public BFVar this[int index]
        {
            get
            {
                if (index < 0 || index >= Size)
                    throw new ArgumentOutOfRangeException(nameof(index), index,
                        $"Structural Memory Violation: Requested index is out of bounds for the variable descriptor. " +
                        $"Valid cell range is [0, {Size - 1}]. For safe boundary checks, use '{nameof(TryGet)}()' instead.");

                return new BFVar(this, index, 1);
            }
        }

        public BFVar? TryGet(int index = 0)
        {
            if (index >= Size || index < 0) return null;
            return new BFVar(this, index, 1);
        }

        internal BFCell GetCell(int index = 0) => new BFCell(this, index);
        internal BFCell? TryGetCell(int index = 0)
        {
            if (index >= Size || index < 0) return null;
            return new BFCell(this, index);
        }

        internal int Resolve(int cellAddr) => Space.Resolve(Addr + cellAddr);

        // ---- block operations ----
        /// <summary>Copies this block to one or more root descriptors.</summary>
        public void CopyTo(params BFVar[] to)
        {
            for (int i = 0; i < Size; i++)
            {
                var srcCell = GetCell(i);
                foreach (var targetDesc in to)
                {
                    if (i < targetDesc.Size)
                    {
                        var tgtCell = targetDesc.GetCell(i);
                        if (tgtCell != null)
                            srcCell.CopyTo(tgtCell);
                    }
                }
            }
            // Zero-fill remaining cells in targets larger than source
            int maxSize = 0;
            foreach (var t in to) 
                if (t.Size > maxSize) 
                    maxSize = t.Size;

            for (int i = Size; i < maxSize; i++)
                foreach (var targetDesc in to)
                    targetDesc.TryGetCell(i)?.Set();
        }

        /// <summary>Moves this block (clearing source) to root descriptors.</summary>
        public void MoveTo(params BFVar[] to)
        {
            int maxSize = 0;
            foreach (var t in to) if (t.Size > maxSize) maxSize = t.Size;

            for (int i = 0; i < Math.Max(Size, maxSize); i++)
            {
                var srcCell = TryGetCell(i);
                foreach (var targetDesc in to)
                {
                    if (i < targetDesc.Size)
                    {
                        var tgtCell = targetDesc.GetCell(i);
                        if (srcCell != null)
                            srcCell.MoveTo(tgtCell);
                        else
                            tgtCell.Set();
                    }
                }
            }

            // Clear remaining source cells that were not moved
            if (Size > maxSize)
            {
                for (int i = maxSize; i < Size; i++)
                    TryGetCell(i)?.Set();
            }
        }

        private BFVar ApplyToAll(Action<int, BFCell> op)
        {
            for (int i = 0; i < Size; i++)
                op(i, GetCell(i));
            return this;
        }

        public BFVar While(Action code) => ApplyToAll((_, c) => c.While(code));
        public BFVar Not() => ApplyToAll((_, c) => c.Not());
        public BFVar Read() => ApplyToAll((_, c) => c.Read());
        public BFVar Print() => ApplyToAll((_, c) => c.Print());
        public BFVar If(Action code) => ApplyToAll((_, c) => c.If(code));

        public BFVar Change(params int[] values)
            => ApplyToAll((i, c) => c.Change(i < values.Length ? values[i] : 0));
        public BFVar Set(params int[] values)
            => ApplyToAll((i, c) => c.Set(i < values.Length ? values[i] : 0));

        /// <summary>
        /// Destructive for both sides. Subtracts the smaller value from both,
        /// digit-wise for multi-cell variables. See BFCell.Compare for semantics.
        /// </summary>
        public static void Compare(BFVar left, BFVar right)
        {
            int n = Math.Min(left.Size, right.Size);
            for (int i = 0; i < n; i++)
                BFCell.Compare(left.GetCell(i), right.GetCell(i));
        }

        /// <summary>Converts to array of cells.</summary>
        public BFVar[] ToArray()
        {
            var array = new BFVar[Size];
            for (int i = 0; i < Size; i++)
                array[i] = this[i];
            return array;
        }

        public override bool Equals(object? obj) =>
            obj is BFVar other &&
            Space.Resolve(Addr) == other.Space.Resolve(other.Addr) &&
            Addr == other.Addr &&
            Size == other.Size;

        public override int GetHashCode() => HashCode.Combine(typeof(BFVar), Space.Resolve(Addr), Addr, Size);
    }
}