namespace BFGo
{
    /// <summary>
    /// A single logical cell inside a memory block.
    /// Operations emit IR instructions and automatically insert MoveTo before each access.
    /// Methods that modify this cell are marked "Not self safe" or "Destructive for both".
    /// </summary>
    public class BFCell
    {
        public readonly BFVar owner;
        public readonly int offset;

        //TODO: добавить вывод значения в студии при отладке дебаггером.

        /// <summary>
        /// Creates a cell for the given root descriptor at the specified offset.
        /// </summary>
        internal BFCell(BFVar descriptor, int offset = 0)
        {
            this.owner = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            this.offset = offset;
        }

        private BFG env => owner.Context.env;

        /// <summary>Emits a MoveTo that positions the tape on this cell.</summary>
        private void MoveToThis() => env.AddInst(new MoveTo(owner, offset, owner.cxtShift));

        // ─── Elementary operations ───────────────────────────────

        /// <summary>
        /// Adds <paramref name="val"/> to this cell.
        /// O(val).
        /// </summary>
        public void Plus(int val = 1)
        {
            MoveToThis();
            env.AddInst(new Plus(val));
        }

        /// <summary>
        /// Subtracts <paramref name="val"/> from this cell.
        /// O(val).
        /// </summary>
        public void Minus(int val = 1)
        {
            MoveToThis();
            env.AddInst(new Minus(val));
        }

        /// <summary>
        /// Changes this cell's value by the given delta.
        /// O(abs(delta)).
        /// </summary>
        public void Change(int delta)
        {
            if (delta == 0) return;

            MoveToThis();
            if (delta > 0)
                env.AddInst(new Plus(delta));
            else
                env.AddInst(new Minus(-delta));
        }

        /// <summary>
        /// Outputs this cell's value.
        /// </summary>
        public void Print() { MoveToThis(); env.AddInst(new Print()); }

        /// <summary>
        /// Reads a character into this cell.
        /// </summary>
        public void Read() { MoveToThis(); env.AddInst(new Read()); }

        // ─── Control flow ────────────────────────────────────────

        /// <summary>
        /// Brainfuck loop anchored to this cell.
        /// Tape is repositioned to this cell before each iteration check.
        /// </summary>
        public void While(Action code)
        {
            MoveToThis();
            env.While(() => { code(); MoveToThis(); });
        }

        /// <summary>
        /// Not self safe.
        /// Clears this cell, then executes <paramref name="code"/> once if non‑zero.
        /// self => 0.
        /// O(n).
        /// </summary>
        public void If(Action code)
        {
            While(() =>
            {
                code();
                Init();
            });
        }


        /// <summary>
        /// Not self Safe
        /// O(n)
        /// </summary>
        /// <param name="s1"></param>
        /// <param name="codeIf"></param>
        /// <param name="codeElse"></param>
        public void IfElse(Action codeIf, Action codeElse)
        {
            var tmp = env.ActiveContext.Alloc(AllocatorKind.Stack, 1);
            tmp[0].Init(1);
            If(() =>
            {
                codeIf();
                tmp.Init();
            });
            tmp[0].If(() =>
            {
                codeElse();
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
        public void AddTo(params BFCell?[] to)
        {
            While(() =>
            {
                Minus();                 // decrement self
                foreach (var dest in to)
                    dest?.Plus();         // increment each target
            });
        }

        /// <summary>
        /// Not self safe.
        /// Moves this cell's value into <paramref name="to"/>.
        /// this => 0;  each dest => original self.
        /// O(n).
        /// </summary>
        public void MoveTo(params BFCell?[] to)
        {
            foreach (var dest in to)
                dest?.Init();             // clear targets
            AddTo(to);                  // move value
        }

        /// <summary>
        /// Copies this cell's value to <paramref name="to"/> without destroying self.
        /// O(n).
        /// </summary>
        public void CopyTo(params BFCell?[] to)
        {
            to = to.Where(dest => dest != null && !Equals(dest)).ToArray();
            // Allocate a temporary cell that will hold the value during copy
            using var tempDesc = env.ActiveContext.Alloc(AllocatorKind.Stack);
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
            using var locals = env.ActiveContext.Alloc(AllocatorKind.Stack, 3);
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
            => obj is BFCell other && owner.Equals(other.owner) && offset == other.offset;

        public override int GetHashCode() => HashCode.Combine(owner, offset);
    }
}