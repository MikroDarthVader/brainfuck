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

        internal BFVar Offset(int offset, int size = 0) => new(this, offset, size);

        // ─── Cell access ─────────────────────────────────────────

        /// <summary>
        /// Returns a size-1 descriptor for the cell at <paramref name="index"/>.
        /// Throws if index is out of [0, Size).
        /// </summary>
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

        /// <summary>
        /// Returns a size-1 descriptor for the cell at <paramref name="index"/>,
        /// or null if index is out of [0, Size).
        /// </summary>
        public BFVar? TryGet(int index = 0)
        {
            if (index >= Size || index < 0) return null;
            return new BFVar(this, index, 1);
        }

        /// <summary>Returns an array of size-1 descriptors, one per cell.</summary>
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

        public override int GetHashCode() =>
            HashCode.Combine(typeof(BFVar), Space.Resolve(Addr), Addr, Size);

        internal int Resolve(int cellAddr) => Space.Resolve(Addr + cellAddr);

        // ─── Internal cell primitives ────────────────────────────

        private BFIR ir => Space.Context.ir;
        private int AbsOf(int i) => Space.Resolve(Addr + i);

        /// <summary>Emits MoveTo placing the head on cell i.</summary>
        private void MoveToCell(int i) => ir.Add(new MoveTo(AbsOf(i)));

        /// <summary>
        /// Emits MoveTo + Change. Delta is reduced modulo cellSize;
        /// zero deltas emit nothing. Sign is preserved so that decrement
        /// loops (like SetCell's clear) compile to single '-'.
        /// </summary>
        private void ChangeCell(int i, int delta)
        {
            int cs = ir.env.cfg.cellSize;
            int d = delta % cs;
            if (d == 0) return;

            MoveToCell(i);
            ir.Add(new Change(d));
        }

        /// <summary>Destructive. cell[i] => 0, then += val.</summary>
        private void SetCell(int i, int val = 0)
        {
            WhileCell(i, () => ChangeCell(i, -1));
            ChangeCell(i, val);
        }

        /// <summary>Emits MoveTo + Print on cell i. Cell unchanged.</summary>
        private void PrintCell(int i)
        {
            MoveToCell(i);
            ir.Add(new Print());
        }

        /// <summary>Emits MoveTo + Read on cell i. Cell overwritten.</summary>
        private void ReadCell(int i)
        {
            MoveToCell(i);
            ir.Add(new Read());
        }

        /// <summary>
        /// BF loop anchored to cell i. Repositions head to cell i before
        /// each iteration check. Cell must reach 0 for the loop to exit.
        /// </summary>
        private void WhileCell(int i, Action code)
        {
            MoveToCell(i);
            ir.While(() => { code(); MoveToCell(i); });
        }

        // ─── Logical operations (non-destructive) ────────────────

        /// <summary>
        /// Non-destructive.
        /// While this variable is non-zero: run code.
        /// Single-cell: BF loop on that cell.
        /// Multi-cell: condition = OR over cells; re-evaluated each iteration.
        /// Terminates only if code eventually zeroes all cells.
        /// O(n * Size) per condition check.
        /// </summary>
        public BFVar While(Action code)
        {
            if (Size == 1)
            {
                WhileCell(0, code);
                return this;
            }

            using var scope = ir.env.CreateScope();
            var any = scope.Alloc();

            IsNonZero(any);
            any.While(() => { code(); IsNonZero(any); });
            return this;
        }

        /// <summary>
        /// Non-destructive.
        /// If any cell is non-zero: run code once.
        /// self => unchanged.
        /// O(n * Size) + cost of code.
        /// </summary>
        public BFVar If(Action code)
        {
            using var scope = ir.env.CreateScope();
            var tmp = scope.Alloc(Size);
            CopyTo(tmp);
            tmp.While(() => { code(); tmp.Zero(); });
            return this;
        }

        /// <summary>
        /// Non-destructive for self; destructive for dst.
        /// dst => 1 if all cells of self are zero, else 0.
        /// Throws if dst aliases self.
        /// O(n * Size).
        /// </summary>
        public BFVar IsZero(BFVar dst)
        {
            EnsureNoAlias(dst, nameof(IsZero));

            dst.Set(1);
            using var scope = ir.env.CreateScope();
            var temps = new BFVar[Size];
            for (int i = 0; i < Size; i++) temps[i] = scope.Alloc();

            for (int i = 0; i < Size; i++) this[i].CopyTo(temps[i]);
            for (int i = 0; i < Size; i++) temps[i].If(() => dst.Set(0));
            return this;
        }

        /// <summary>
        /// Non-destructive for self; destructive for dst.
        /// dst => 1 if any cell of self is non-zero, else 0.
        /// Throws if dst aliases self.
        /// O(n * Size).
        /// </summary>
        public BFVar IsNonZero(BFVar dst)
        {
            EnsureNoAlias(dst, nameof(IsNonZero));

            dst.Set(0);
            using var scope = ir.env.CreateScope();
            var temps = new BFVar[Size];
            for (int i = 0; i < Size; i++) temps[i] = scope.Alloc();

            for (int i = 0; i < Size; i++) this[i].CopyTo(temps[i]);
            for (int i = 0; i < Size; i++) temps[i].If(() => dst.Set(1));
            return this;
        }

        // ─── Data movement ───────────────────────────────────────

        /// <summary>
        /// Non-destructive.
        /// to[i] => self[i]  for i in [0, min(Size, to.Size))
        /// to[i] => 0        for i in [Size, to.Size)
        /// self   => unchanged
        /// Throws if source and target ranges overlap.
        /// O(n * min(Size, to.Size)).
        /// </summary>
        public BFVar CopyTo(BFVar to)
        {
            if (ReferenceEquals(Space, to.Space) &&
                Addr < to.Addr + to.Size && to.Addr < Addr + Size)
                throw new InvalidOperationException("CopyTo: source and target ranges overlap.");

            int n = Math.Min(Size, to.Size);
            if (n > 0)
            {
                using var scope = ir.env.CreateScope();
                var tmp = scope.Alloc();
                int tmpAbs = tmp.AbsOf(0);

                for (int i = 0; i < n; i++)
                    CopyCellAt(AbsOf(i), tmpAbs, to.AbsOf(i));
            }

            for (int i = Size; i < to.Size; i++)
                to.SetCell(i, 0);

            return this;
        }

        /// <summary>
        /// Emits srcAbs -> dstAbs + tmpAbs, then tmpAbs -> srcAbs.
        /// dstAbs cleared first. srcAbs restored to original value.
        /// O(n).
        /// </summary>
        private void CopyCellAt(int srcAbs, int tmpAbs, int dstAbs)
        {
            // Clear destination.
            ir.Add(new MoveTo(dstAbs));
            ir.While(() => ir.Add(new Change(-1)));

            // src -> (dst + tmp)
            ir.Add(new MoveTo(srcAbs));
            ir.While(() =>
            {
                ir.Add(new Change(-1));
                ir.Add(new MoveTo(tmpAbs)); ir.Add(new Change(1));
                ir.Add(new MoveTo(dstAbs)); ir.Add(new Change(1));
                ir.Add(new MoveTo(srcAbs));
            });

            // tmp -> src (restore source)
            ir.Add(new MoveTo(tmpAbs));
            ir.While(() =>
            {
                ir.Add(new Change(-1));
                ir.Add(new MoveTo(srcAbs)); ir.Add(new Change(1));
                ir.Add(new MoveTo(tmpAbs));
            });
        }

        // ─── Value operations (destructive by semantics) ────────

        /// <summary>
        /// Destructive.
        /// cell[i] => values[i], or 0 if i >= values.Length.
        /// O(Size * n).
        /// </summary>
        public BFVar Set(params int[] values) =>
            ApplyToAll((i, v) => v.SetCell(0, i < values.Length ? values[i] : 0));

        /// <summary>Destructive. Every cell => value. O(Size * n).</summary>
        public BFVar Set(char value) =>
            ApplyToAll((_, v) => v.SetCell(0, value));

        /// <summary>
        /// Destructive.
        /// cell[i] => values[i], or 0 if i >= values.Length.
        /// O(Size * n).
        /// </summary>
        public BFVar Set(string values) =>
            ApplyToAll((i, v) => v.SetCell(0, i < values.Length ? values[i] : 0));

        /// <summary>
        /// Destructive.
        /// cell[i] += values[i], or += 0 if i >= values.Length.
        /// O(Size * n).
        /// </summary>
        public BFVar Change(params int[] values) =>
            ApplyToAll((i, v) => v.ChangeCell(0, i < values.Length ? values[i] : 0));

        /// <summary>Destructive. Every cell => 0. O(Size * n).</summary>
        public BFVar Zero() => Set();

        /// <summary>Destructive. Reads one character into every cell. O(Size).</summary>
        public BFVar Read() => ApplyToAll((_, v) => v.ReadCell(0));

        /// <summary>Non-destructive. Prints every cell as a character. O(Size).</summary>
        public BFVar Print() => ApplyToAll((_, v) => v.PrintCell(0));

        // ─── Helpers ────────────────────────────────────────────

        /// <summary>Applies op to each cell index. Returns this.</summary>
        private BFVar ApplyToAll(Action<int, BFVar> op)
        {
            for (int i = 0; i < Size; i++) op(i, this[i]);
            return this;
        }

        /// <summary>Throws if self and other share any cell address.</summary>
        private void EnsureNoAlias(BFVar other, string op)
        {
            if (ReferenceEquals(Space, other.Space) &&
                Addr < other.Addr + other.Size && other.Addr < Addr + Size)
                throw new InvalidOperationException($"{op}: dst must not alias self.");
        }
    }
}