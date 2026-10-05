namespace BFGo
{
    /// <summary>
    /// A single logical cell inside a memory block.
    /// Operations emit IR instructions and automatically insert MoveTo before each access.
    /// Methods that modify this cell are marked "Destructive".
    /// </summary>
    internal class BFCell
    {
        public readonly BFVar owner;
        public readonly int offset;

        /// <summary>
        /// Creates a cell for the given root descriptor at the specified offset.
        /// </summary>
        internal BFCell(BFVar descriptor, int offset = 0)
        {
            owner = descriptor;
            this.offset = offset;
        }

        private BFIR ir => owner.Space.Context.ir;
        private BFGProgram env => ir.env;
        private BFGCfg cfg => env.cfg;

        /// <summary>Emits a MoveTo that positions the tape on this cell.</summary>
        private void MoveToThis() => ir.Add(new MoveTo(owner.Resolve(offset)));

        // ─── Elementary operations ───────────────────────────────
        /// <summary>
        /// Changes this cell's value by the given delta.
        /// O(abs(delta)).
        /// </summary>
        public BFCell Change(int delta)
        {
            if (delta == 0) return this;

            MoveToThis();
            ir.Add(new Change(delta % cfg.cellSize));
            return this;
        }

        /// <summary>
        /// Outputs this cell's value.
        /// </summary>
        public BFCell Print()
        {
            MoveToThis();
            ir.Add(new Print());
            return this;
        }

        /// <summary>
        /// Reads a character into this cell.
        /// </summary>
        public BFCell Read()
        {
            MoveToThis();
            ir.Add(new Read());
            return this;
        }

        // ─── Control flow ────────────────────────────────────────

        /// <summary>
        /// Brainfuck loop anchored to this cell.
        /// Tape is repositioned to this cell before each iteration check.
        /// </summary>
        public BFCell While(Action code)
        {
            MoveToThis();
            ir.While(() => { code(); MoveToThis(); });

            return this;
        }

        /// <summary>
        /// Destructive.
        /// Clears this cell, then executes <paramref name="code"/> once if non‑zero.
        /// self => 0.
        /// O(n).
        /// </summary>
        public BFCell If(Action code)
        {
            return While(() =>
            {
                code();
                Set();
            });
        }


        /// <summary>
        /// Destructive. 
        /// Replaces value with logical negation: non-zero -> 0, zero -> 1.
        /// O(n)
        /// </summary>
        public BFCell Not()
        {
            using var scope = env.CreateScope();
            var tmp = scope.Alloc().GetCell();
            tmp.Set(1);
            If(() =>
            {
                tmp.Set();
            });
            tmp.MoveTo(this);
            return this;
        }

        // ─── Initialisation ──────────────────────────────────────

        /// <summary>
        /// Sets this cell to zero, then adds <paramref name="val"/>.
        /// O(n + val).
        /// </summary>
        public BFCell Set(int val = 0)
        {
            While(() => { Change(-1); });
            return Change(val);
        }

        // ─── Data movement ───────────────────────────────────────

        /// <summary>
        /// Destructive.
        /// to => to + self;  self => 0.
        /// O(n).
        /// </summary>
        public BFCell AddTo(params BFCell?[] to)
        {
            return While(() =>
            {
                Change(-1);                 // decrement self
                foreach (var dest in to)
                    dest?.Change(1);         // increment each target
            });
        }

        /// <summary>
        /// Destructive.
        /// Moves this cell's value into <paramref name="to"/>.
        /// this => 0;  each dest => original self.
        /// O(n).
        /// </summary>
        public BFCell MoveTo(params BFCell?[] to)
        {
            foreach (var dest in to)
                dest?.Set();             // clear targets
            return AddTo(to);                  // move value
        }

        /// <summary>
        /// Copies this cell's value to <paramref name="to"/> without destroying self. Destructive for targets.
        /// O(n).
        /// </summary>
        public BFCell CopyTo(params BFCell?[] to)
        {
            // Drop nulls.
            to = [.. to.Where(dest => dest != null)];
            using var scope = env.CreateScope();
            var tmp = scope.Alloc().GetCell();

            to = [.. to, tmp];   // include temp in targets
            MoveTo(to);                      // self -> (to + temp)
            tmp.MoveTo(this);                // temp -> self, restoring original value

            return this;
        }

        // ─── Comparison ──────────────────────────────────────────

        /// <summary>
        /// Destructive for both cells.
        /// Subtracts the smaller value from both.
        /// After call:
        ///   if self >= right : self = self - right, right = 0
        ///   if self &lt; right : self = 0, right = right - self
        /// O(n2).
        /// </summary>
        public static void Compare(BFCell left, BFCell right)
        {
            // Three temporary cells for the algorithm
            var env = left.env;
            using var scope = env.CreateScope();

            var flagB = scope.Alloc().GetCell().Set();   // 1 if other may be non‑zero in current iteration
            var counterB = scope.Alloc().GetCell().Set();   // counts successful decrements of other
            var flagSelfGt = scope.Alloc().GetCell().Set();   // remembers that self was strictly greater at some point

            // Invariant: each iteration reduces both sides by 1.
            // flagSelfGt records whether left was ever strictly greater than right,
            // which needs a correction pass at the end.
            left.While(() =>
            {
                // ---- Try to decrement other once, if it is non‑zero ----
                flagB.Change(1);                      // assume other is non‑zero
                right.While(() =>
                {
                    flagB.Set();                   // other was non‑zero → clear flag
                    counterB.Change(1);                // count successful decrement
                    right.Change(-1);
                });

                // ---- Accumulate results ----
                flagB.AddTo(flagSelfGt);            // if flagB remained set, other was zero → remember self > other
                counterB.AddTo(right);              // return all temporarily removed decrements back to other

                // ---- Decrement both cells ----
                left.Change(-1);
                right.Change(-1);
            });

            // ---- Correction for the case self > other ----
            flagSelfGt.If(() =>
            {
                right.While(() =>
                {
                    left.Change(1);
                    right.Change(1);
                });
            });
        }

        // ─── Equality ────────────────────────────────────────────

        public override bool Equals(object? obj)
            => obj is BFCell other
            && offset == other.offset
            && owner.Resolve(offset) == other.owner.Resolve(other.offset);

        public override int GetHashCode()
            => HashCode.Combine(owner.Resolve(offset), offset);
    }
}