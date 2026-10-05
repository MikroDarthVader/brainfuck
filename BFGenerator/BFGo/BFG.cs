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

            public void Optimize(BFVar desc)
            {
                BFVar.Compare(Pos.From(desc), Neg.From(desc));
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

            addr.Set();
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

            ctx.SetAtRoot(dest == null);
            movementType.Optimize(movement);

            for (int i = 0; i < addrType.Size; i++)
            {
                int step = (int)Math.Pow(cfg.cellSize, i) * cfg.BlockSize;
                var p = movementType.Pos.From(movement)[i];
                var n = movementType.Neg.From(movement)[i];
                var a = addr[i];

                p.While(() => { p.Change(-1); addr[i].Change(1); Step(step); });
                n.While(() => { n.Change(-1); addr[i].Change(-1); Step(-step); });
            }
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