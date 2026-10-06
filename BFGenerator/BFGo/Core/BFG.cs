namespace BFGo
{
    /// <summary>
    /// Manages context transitions (Go) and the shared service registers
    /// (addr, movement) at the bottom of the stack scope.
    /// </summary>
    internal class BFG
    {
        internal readonly BFIR ir;
        public BFGCfg cfg => ir.env.cfg;
        private BFContext ctx => ir.Context;

        /// <summary>
        /// Movement register layout: Pos and Neg fields of addrSize digits each.
        /// Optimize() normalizes the difference so only one direction remains per digit.
        /// Future step-count minimization (carry across digits) belongs here.
        /// </summary>
        private class BFAddrComparerType : BFType
        {
            internal BFType Pos { get; }
            internal BFType Neg { get; }

            public BFAddrComparerType(int addrSize) : base()
            {
                Pos = RegisterField(new BFType(addrSize));
                Neg = RegisterField(new BFType(addrSize));
            }

            /// <summary>
            /// Digit-wise subtractive compare. After call, each digit pair
            /// (pos[i], neg[i]) has the smaller value subtracted from both.
            /// </summary>
            public void Optimize(BFVar desc)
            {
                var pos = Pos.From(desc);
                var neg = Neg.From(desc);
                int n = Math.Min(pos.Size, neg.Size);
                for (int i = 0; i < n; i++)
                    CompareDigit(pos[i], neg[i]);
            }

            /// <summary>
            /// Destructive for both cells.
            /// Subtracts the smaller value from both.
            /// After call:
            ///   if left >= right : left = left - right, right = 0
            ///   if left &lt;  right : left = 0, right = right - left
            /// O(n2).
            /// </summary>
            private static void CompareDigit(BFVar left, BFVar right)
            {
                using var scope = left.Space.Context.ir.env.CreateScope();

                // flagB: 1 if right may still be non-zero in the current iteration.
                // counterB: counts how many times right was successfully decremented.
                // flagSelfGt: remembers whether left was ever strictly greater than right.
                var flagB = scope.Alloc();
                var counterB = scope.Alloc();
                var flagSelfGt = scope.Alloc();

                // Invariant: each iteration reduces both sides by 1.
                // flagSelfGt records whether left was ever strictly greater than right,
                // which needs a correction pass at the end.
                left.While(() =>
                {
                    // ---- Try to decrement right once, if it is non-zero ----
                    flagB.Change(1);
                    right.While(() =>
                    {
                        flagB.Zero();
                        counterB.Change(1);
                        right.Change(-1);
                    });

                    // ---- Accumulate results ----
                    DigitAddTo(flagB, flagSelfGt);
                    DigitAddTo(counterB, right);

                    // ---- Decrement both cells ----
                    left.Change(-1);
                    right.Change(-1);
                });

                // ---- Correction for the case left > right ----
                flagSelfGt.If(() =>
                {
                    right.While(() =>
                    {
                        left.Change(1);
                        right.Change(1);
                    });
                });
            }

            /// <summary>
            /// Destructive. src => 0, dst += src.
            /// O(n).
            /// </summary>
            private static void DigitAddTo(BFVar src, BFVar dst)
            {
                src.While(() =>
                {
                    src.Change(-1);
                    dst.Change(1);
                });
            }
        }

        private readonly BFStackScope service;
        private readonly BFAddrComparerType movementType;
        private readonly BFType addrType;
        private readonly BFVar addr;
        private readonly BFVar movement;

        internal BFG(BFIR ir)
        {
            this.ir = ir;

            movementType = new BFAddrComparerType(cfg.addrSize);
            addrType = new BFType(cfg.addrSize);

            service = ctx.CreateStackScope();
            addr = service.Alloc(addrType.Size);
            movement = service.Alloc(movementType.Size);
        }

        /// Moves the frame to the context at dest (root if null), carrying the stack.
        ///
        /// Digit-wise: computes pos/neg from (dest - addr), then steps each digit
        /// independently by cellSize^i * BlockSize. Each step moves the stack
        /// and shifts the frame origin.
        internal void Go(BFVar? dest)
        {
            if (dest != null)
                dest.CopyTo(movementType.Pos.From(movement));
            else
                movementType.Pos.From(movement).Set();
            addr.CopyTo(movementType.Neg.From(movement));

            movementType.Optimize(movement);

            for (int i = 0; i < addrType.Size; i++)
            {
                int step = (int)Math.Pow(cfg.cellSize, i) * cfg.BlockSize;
                var p = movementType.Pos.From(movement)[i];
                var n = movementType.Neg.From(movement)[i];

                p.While(() => { p.Change(-1); addr[i].Change(1); Step(step); });
                n.While(() => { n.Change(-1); addr[i].Change(-1); Step(-step); });
            }

            ctx.SetAtRoot(dest == null);
        }

        /// <summary>
        /// Single per-digit step: update addr, move the stack, shift the coordinate system.
        /// </summary>
        private void Step(int step)
        {
            if (step == 0) throw new InvalidOperationException("MoveStack requires a non-zero step.");
            MoveStack(step);
            ir.Add(new ShiftContext(step));
        }

        /// Moves every cell of the current stack window by <paramref name="step"/>.
        /// Iterates from the far side towards the near side so that a source cell
        /// is read before its destination overwrites another source.
        ///
        /// Per cell: clear dst; while src > 0: src--, dst++.
        private void MoveStack(int step)
        {
            int top = ctx.StackSize;
            bool forward = step > 0;
            int start = forward ? top - 1 : 0;
            int end = forward ? -1 : top;
            int delta = forward ? -1 : 1;

            for (int i = start; i != end; i += delta)
            {
                int src = ctx.ResolveStack(i);

                // Clear destination cell.
                ir.Add(new MoveTo(src, step));
                ir.While(() => ir.Add(new Change(-1)));

                // Transfer src -> src + step.
                ir.Add(new MoveTo(src, 0));
                ir.While(() =>
                {
                    ir.Add(new Change(-1));
                    ir.Add(new MoveTo(src, step));
                    ir.Add(new Change(+1));
                    ir.Add(new MoveTo(src, 0));
                });
            }
        }
    }
}