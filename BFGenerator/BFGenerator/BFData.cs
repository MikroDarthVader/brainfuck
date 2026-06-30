
namespace BFGenerator
{
    /// <summary>
    /// A single logical cell inside a memory block.
    /// Operations emit IR instructions and automatically insert MoveTo before each access.
    /// Methods that modify this cell are marked "Not self safe" or "Destructive for both".
    /// </summary>
    public class BFCell
    {
        private readonly BFMemoryDescriptor descriptor;
        private readonly int offset;

        /// <summary>
        /// Creates a cell for the given descriptor at the specified offset.
        /// </summary>
        public BFCell(BFMemoryDescriptor descriptor, int offset = 0)
        {
            this.descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            this.offset = offset;
        }

        private BFContext Context => descriptor.Context;
        private BFIR IR => Context.IR;

        /// <summary>Emits a MoveTo that positions the tape on this cell.</summary>
        private void MoveToThis() => IR.Add(new MoveTo(descriptor, offset));

        // ─── Elementary operations ───────────────────────────────

        /// <summary>
        /// Adds <paramref name="val"/> to this cell.
        /// O(val).
        /// </summary>
        public void Plus(int val = 1)
        {
            MoveToThis();
            IR.Add(new Plus(val));
        }

        /// <summary>
        /// Subtracts <paramref name="val"/> from this cell.
        /// O(val).
        /// </summary>
        public void Minus(int val = 1)
        {
            MoveToThis();
            IR.Add(new Minus(val));
        }

        /// <summary>
        /// Outputs this cell's value.
        /// </summary>
        public void Print() { MoveToThis(); IR.Add(new Print()); }

        /// <summary>
        /// Reads a character into this cell.
        /// </summary>
        public void Input() { MoveToThis(); IR.Add(new Read()); }

        // ─── Control flow ────────────────────────────────────────

        /// <summary>
        /// Brainfuck loop anchored to this cell.
        /// Tape is repositioned to this cell before each iteration check.
        /// </summary>
        public void While(Action body)
        {
            MoveToThis();
            IR.Add(new LoopStart());
            body();
            MoveToThis();
            IR.Add(new LoopEnd());
        }

        /// <summary>
        /// Not self safe.
        /// Executes <paramref name="code"/> once if non‑zero, then clears this cell.
        /// self => 0.
        /// O(n).
        /// </summary>
        public void If(Action code)
        {
            While(() =>
            {
                code();
                Init();   // clear after body – loop exits after one iteration
            });
        }

        // ─── Initialisation ──────────────────────────────────────

        /// <summary>
        /// Sets this cell to zero, then adds <paramref name="val"/>.
        /// O(n + val).
        /// </summary>
        public void Init(byte val = 0)
        {
            While(() => { Minus(); });   // [-]
            if (val > 0) Plus(val);
        }

        // ─── Data movement ───────────────────────────────────────

        /// <summary>
        /// Not self safe.
        /// to => to + self;  self => 0.
        /// O(n).
        /// </summary>
        public void AddTo(params BFCell[] to)
        {
            While(() =>
            {
                Minus();                 // decrement self
                foreach (var dest in to)
                    dest.Plus();         // increment each target
            });
        }

        /// <summary>
        /// Not self safe.
        /// Moves this cell's value into <paramref name="to"/>.
        /// this => 0;  each dest => original self.
        /// O(n).
        /// </summary>
        public void MoveTo(params BFCell[] to)
        {
            foreach (var dest in to)
                dest.Init();             // clear targets
            AddTo(to);                  // move value
        }

        /// <summary>
        /// Copies this cell's value to <paramref name="to"/> without destroying self.
        /// O(n).
        /// </summary>
        public void CopyTo(params BFCell[] to)
        {
            // Allocate a temporary cell that will hold the value during copy
            using var tempDesc = Context.Alloc(AllocatorKind.Stack);
            var tmp = tempDesc[0];

            to = to.Append(tmp).ToArray();   // include temp in targets
            MoveTo(to);                      // self -> (to + temp)
            tmp.MoveTo(this);                // temp -> self, restoring original value
        }

        // ─── Comparison ──────────────────────────────────────────

        /// <summary>
        /// Destructive for both cells.
        /// Subtracts the smaller value from both.
        /// After call:
        ///   if self >= other : self = self - other,  other = 0
        ///   if self &lt; other  : self = 0,            other = other - self
        /// O(n2).
        /// </summary>
        public void CompareTo(BFCell other)
        {
            // Three temporary cells for the algorithm
            using var locals = Context.Alloc(AllocatorKind.Stack, 3);
            BFCell[] tmp = locals.ToArray();
            var flagB = tmp[0];   // 1 if other may be non‑zero in current iteration
            var counterB = tmp[1];   // counts successful decrements of other
            var flagSelfGt = tmp[2];   // remembers that self was strictly greater at some point

            foreach (var cell in tmp)
                cell.Init();

            While(() =>
            {
                // ---- Try to decrement other once, if it is non‑zero ----
                flagB.Plus();                       // assume other is non‑zero
                other.While(() =>
                {
                    flagB.Init();                   // other was non‑zero → clear flag
                    counterB.Plus();                // count successful decrement
                    other.Minus();
                });

                // ---- Accumulate results ----
                flagB.AddTo(flagSelfGt);            // if flagB remained set, other was zero → remember self > other
                counterB.AddTo(other);              // return all temporarily removed decrements back to other

                // ---- Decrement both cells ----
                Minus();
                other.Minus();
            });

            // ---- Correction for the case self > other ----
            flagSelfGt.If(() =>
            {
                other.While(() =>
                {
                    Plus();
                    other.Plus();
                });
            });
        }

        // ─── Equality ────────────────────────────────────────────

        public override bool Equals(object? obj)
            => obj is BFCell other && descriptor.Equals(other.descriptor) && offset == other.offset;

        public override int GetHashCode() => HashCode.Combine(descriptor, offset);
    }


    /// <summary>
    /// Abstract base for typed data blocks that occupy a contiguous slice of logical memory.
    /// Described by a <see cref="BFMemoryDescriptor"/> and a fixed <see cref="Size"/>.
    /// Provides block‑level copy, move, clear and indexed cell access.
    /// Derived classes define the semantics of the data
    /// All operations emit IR instructions through the owning context.
    /// </summary>
    public abstract class BFData
    {
        protected readonly BFMemoryDescriptor descriptor;

        /// <summary>Logical start index of this slice inside the descriptor.</summary>
        protected readonly int baseIndex;

        /// <summary>Fixed number of logical cells in this slice.</summary>
        public readonly int Size;

        /// <summary>
        /// Initializes a new data slice.
        /// </summary>
        /// <param name="descriptor">Memory block descriptor.</param>
        /// <param name="size">Number of cells. Must be positive and fit inside the descriptor.</param>
        /// <param name="baseIndex">Logical index inside the descriptor where this slice starts (default 0).</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="baseIndex"/> is out of range, or <paramref name="size"/> is non‑positive
        /// or exceeds the available space after <paramref name="baseIndex"/>.
        /// </exception>
        protected BFData(BFMemoryDescriptor descriptor, int size, int baseIndex = 0)
        {
            this.descriptor = descriptor;

            if (baseIndex < 0 || baseIndex >= descriptor.Size)
                throw new ArgumentException(
                    $"Base index {baseIndex} is out of descriptor bounds [0, {descriptor.Size}).",
                    nameof(baseIndex));

            if (size <= 0 || size > descriptor.Size - baseIndex)
                throw new ArgumentException(
                    $"Size {size} is invalid or exceeds available space " +
                    $"(descriptor size {descriptor.Size}, base index {baseIndex}).",
                    nameof(size));

            this.baseIndex = baseIndex;
            this.Size = size;
        }

        /// <summary>
        /// Returns a <see cref="BFCell"/> that provides single‑cell access to the
        /// logical index <paramref name="index"/> inside this slice.
        /// </summary>
        public BFCell this[int index]
        {
            get
            {
                if (index < 0 || index >= Size)
                    throw new IndexOutOfRangeException(
                        $"Cell index {index} is outside the slice bounds [0, {Size}).");
                return descriptor[baseIndex + index];
            }
        }

        // ─── Block‑level copy / move ─────────────────────────────

        /// <summary>
        /// Copies the whole contents of this slice into every destination in <paramref name="to"/>.
        /// All destinations must have exactly the same <see cref="Size"/> as this slice.
        /// This slice is left unchanged.
        /// O(Size * max_cell_value).  Uses efficient multi‑target copy at the cell level.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Any destination has a different size.
        /// </exception>
        public void CopyTo(params BFData[] to)
        {
            for (int d = 0; d < to.Length; d++)
            {
                if (to[d].Size != Size)
                    throw new ArgumentException(
                        $"Destination at index {d} has size {to[d].Size}, but source size is {Size}.",
                        nameof(to));
            }

            // Copy cell by cell, each cell value is distributed to all destinations at once.
            for (int i = 0; i < Size; i++)
                this[i].CopyTo(to.Select(dest => dest[i]).ToArray());
        }

        /// <summary>
        /// Moves the whole contents of this slice into every destination in <paramref name="to"/>.
        /// All destinations must have exactly the same <see cref="Size"/> as this slice.
        /// Destructive for this slice: all its cells become zero.
        /// O(Size * max_cell_value).
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Any destination has a different size.
        /// </exception>
        public void MoveTo(params BFData[] to)
        {
            for (int d = 0; d < to.Length; d++)
            {
                if (to[d].Size != Size)
                    throw new ArgumentException(
                        $"Destination at index {d} has size {to[d].Size}, but source size is {Size}.",
                        nameof(to));
            }

            for (int i = 0; i < Size; i++)
                this[i].MoveTo(to.Select(dest => dest[i]).ToArray());
        }

        /// <summary>
        /// Sets every cell of this slice to zero.
        /// O(Size * max_cell_value).
        /// </summary>
        public void Init()
        {
            for (int i = 0; i < Size; i++)
                this[i].Init();
        }

        // ─── Equality (by descriptor and slice) ──────────────────

        public override bool Equals(object? obj)
            => obj is BFData other &&
               descriptor.Equals(other.descriptor) &&
               baseIndex == other.baseIndex &&
               Size == other.Size;

        public override int GetHashCode() => HashCode.Combine(descriptor, baseIndex, Size);
    }
}