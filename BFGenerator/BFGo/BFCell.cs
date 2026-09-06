using System.Diagnostics;

namespace BFGo
{
    /// <summary>
    /// A single logical cell inside a memory block.
    /// Operations emit IR instructions and automatically insert MoveTo before each access.
    /// Methods that modify this cell are marked "Not self safe" or "Destructive for both".
    /// </summary>
    [DebuggerDisplay("{DebugValue}")]
    public class BFCell
    {
        public readonly BFVar owner;
        public readonly int offset;
        public BFGProgram env => owner.env;

        private int DebugValue
        {
            get
            {
                if (owner.Context.ir is not BFIRDebugger debugger)
                    return -1;

                return debugger.GetValue(debugger.ActiveContext.Resolve(owner, offset));
            }
        }

        /// <summary>
        /// Creates a cell for the given root descriptor at the specified offset.
        /// </summary>
        internal BFCell(BFVar descriptor, int offset = 0)
        {
            owner = descriptor;
            this.offset = offset;
        }
        
        private BFIR ir => owner.Context.ir;

        /// <summary>Emits a MoveTo that positions the tape on this cell.</summary>
        private void MoveToThis() => ir.Add(new MoveTo(owner, offset));

        // ─── Elementary operations ───────────────────────────────

        /// <summary>
        /// Adds <paramref name="val"/> to this cell.
        /// O(val).
        /// </summary>
        public BFCell Plus(int val = 1) => Change(val);

        /// <summary>
        /// Subtracts <paramref name="val"/> from this cell.
        /// O(val).
        /// </summary>
        public BFCell Minus(int val = 1) => Change(-val);

        /// <summary>
        /// Changes this cell's value by the given delta.
        /// O(abs(delta)).
        /// </summary>
        public BFCell Change(int delta)
        {
            if (delta == 0) return this;

            MoveToThis();
            ir.Add(new Change(delta % ir.cfg.cellSize));
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
        /// Not self safe.
        /// Clears this cell, then executes <paramref name="code"/> once if non‑zero.
        /// self => 0.
        /// O(n).
        /// </summary>
        public BFCell If(Action code)
        {
            return While(() =>
            {
                code();
                Init();
            });
        }


        /// <summary>
        /// Not self Safe
        /// O(n)
        /// </summary>
        public BFCell Not()
        {
            using var tmp = env.Alloc();
            tmp.Init(1);
            If(() =>
            {
                tmp.Init();
            });
            tmp[0].MoveTo(this);
            return this;
        }

        // ─── Initialisation ──────────────────────────────────────

        /// <summary>
        /// Sets this cell to zero, then adds <paramref name="val"/>.
        /// O(n + val).
        /// </summary>
        public BFCell Init(int val = 0)
        {
            While(() => { Minus(); });
            return Plus(val);
        }

        // ─── Data movement ───────────────────────────────────────

        /// <summary>
        /// Not self safe.
        /// to => to + self;  self => 0.
        /// O(n).
        /// </summary>
        public BFCell AddTo(params BFCell?[] to)
        {
            return While(() =>
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
        public BFCell MoveTo(params BFCell?[] to)
        {
            foreach (var dest in to)
                dest?.Init();             // clear targets
            return AddTo(to);                  // move value
        }

        /// <summary>
        /// Copies this cell's value to <paramref name="to"/> without destroying self.
        /// O(n).
        /// </summary>
        public BFCell CopyTo(params BFCell?[] to)
        {
            to = [.. to.Where(dest => dest != null /*&&
                !(dest.offset == offset &&
                  dest.owner.Equals(owner) &&
                  dest.owner.Context.ID == owner.Context.ID)*/
            )];
            using var tempDesc = env.Alloc();
            var tmp = tempDesc[0];

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
            using var flagB = env.Alloc().Init();   // 1 if other may be non‑zero in current iteration
            using var counterB = env.Alloc().Init();   // counts successful decrements of other
            using var flagSelfGt = env.Alloc().Init();   // remembers that self was strictly greater at some point

            left.While(() =>
            {
                // ---- Try to decrement other once, if it is non‑zero ----
                flagB[0].Plus();                       // assume other is non‑zero
                right.While(() =>
                {
                    flagB.Init();                   // other was non‑zero → clear flag
                    counterB[0].Plus();                // count successful decrement
                    right.Minus();
                });

                // ---- Accumulate results ----
                flagB[0].AddTo(flagSelfGt[0]);            // if flagB remained set, other was zero → remember self > other
                counterB[0].AddTo(right);              // return all temporarily removed decrements back to other

                // ---- Decrement both cells ----
                left.Minus();
                right.Minus();
            });

            // ---- Correction for the case self > other ----
            flagSelfGt.If(() =>
            {
                right.While(() =>
                {
                    left.Plus();
                    right.Plus();
                });
            });
        }

        // ─── Equality ────────────────────────────────────────────

        public override bool Equals(object? obj)
            => obj is BFCell other && offset == other.offset;

        public override int GetHashCode() => HashCode.Combine(typeof(BFCell), offset);
    }
}