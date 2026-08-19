namespace BFGo
{
    /// <summary>
    /// Main compiler facade. Manages static and dynamic contexts, pointer-based
    /// transitions and compilation of IR into Brainfuck code.
    /// </summary>
    internal class BFG
    {
        internal readonly BFIR ir;

        public bool Debugging => ir.Debuggable;
        public BFGCfg cfg => ir.cfg;

        internal readonly BFStaticContext staticCxt;

        private readonly BFAddrComparerType movementType;
        private readonly BFAddrType addrType;
        private BFVar? addr;
        private BFVar? movement;
        private BFVar? flags;

        internal BFG(BFIR ir)
        {
            this.ir = ir;

            staticCxt = (BFStaticContext)ir.ActiveContext;

            movementType = new BFAddrComparerType(cfg.addrSize);
            addrType = new BFAddrType(cfg.addrSize);

            addr = null;
        }

        /// <summary>
        /// Initial transition from the root static context into a dynamic context.
        /// Must be called exactly once before any subsequent dynamic-to-dynamic transitions.
        /// </summary>
        /// <param name="addrDest">Descriptor containing the target address (the value of this cell determines which dynamic context to enter).</param>
        /// <param name="move">Optional variables to carry over into the new dynamic context. Their values are preserved and they become bound to the new context.</param>
        /// <exception cref="InvalidOperationException">Thrown if the current context is not the static root.</exception>
        internal void GoFromStatic(BFVar addrDest, BFVar[]? move = null)
        {
            if (ir.ActiveContext != staticCxt)
                throw new InvalidOperationException(
                    "GoFromStatic can only be invoked from the root static context. " +
                    "For subsequent transitions between dynamic contexts, use Go().");

            move ??= [];
            var moveFrom = move.OrderBy(x => x.BaseIndex).ToList();

            var zeroCxt = ir.CreateCxt();
            addr = zeroCxt.Alloc(cfg.addrSize);
            flags = zeroCxt.Alloc(2);
            movement = zeroCxt.Alloc(movementType.Size);

            foreach (var desc in moveFrom)
                desc.Rebind(desc.ApplyShift(() => -staticCxt.MaxSize));

            ir.Add(new ShiftContext(() => staticCxt.MaxSize, zeroCxt));
            addr.Init();

            GoByPtr(addrDest.ApplyShift(() => -staticCxt.MaxSize), moveFrom, true);
        }

        /// <summary>
        /// Transition between dynamic contexts using an address stored in a cell.
        /// </summary>
        /// <param name="addrDest">Descriptor whose current value is the target context address.</param>
        /// <param name="move">Variables to carry forward into the destination dynamic context. They are rebound to the new context without losing their values.</param>
        /// <exception cref="InvalidOperationException">Thrown if called from the static root context (use GoFromStatic instead).</exception>
        internal void Go(BFVar addrDest, BFVar[]? move = null)
        {
            if (ir.ActiveContext == staticCxt)
                throw new InvalidOperationException(
                    "Go cannot be called from the static context. " +
                    "For the initial transition from static to dynamic, use GoFromStatic().");

            move ??= [];
            var moveFrom = move.OrderBy(x => x.BaseIndex).ToList();

            GoByPtr(addrDest, moveFrom);
        }

        /// <summary>
        /// Returns from the current dynamic context back to the static root context.
        /// </summary>
        /// <param name="move">Variables to bring back to the static context. They are rebound to fresh allocations in the static root.</param>
        /// <exception cref="InvalidOperationException">Thrown if already in the static context, or if invoked without having a dynamic frame.</exception>
        internal void GoStatic(BFVar[]? move = null)
        {
            if (ir.ActiveContext == staticCxt)
                throw new InvalidOperationException(
                    "GoStatic is only valid from a dynamic context. " +
                    "Use GoFromStatic() or Go() to navigate between dynamic contexts.");

            move ??= [];
            var moveFrom = move.OrderBy(x => x.BaseIndex).ToList();

            GoByPtr(null, moveFrom);

            foreach (BFVar desc in moveFrom)
            {
                BFVar tmp = staticCxt.Alloc(desc.Size);
                desc.MoveTo(tmp.ApplyShift(() => -staticCxt.MaxSize));
                desc.Rebind(tmp);
            }
            ir.Add(new ShiftContext(() => -staticCxt.MaxSize, staticCxt));

            addr = movement = flags = null;
        }

        private void GoByPtr(BFVar? addrDest,
                             List<BFVar> moveFrom, bool moveFromZero = false)
        {
            if (addr == null || movement == null || flags == null)
                throw new InvalidOperationException("Compiler Bug: Attempted a dynamic context transition before the runtime registers were bootstrapped by GoDynamic.");

            if (addrDest != null)
            {
                addrDest.CopyTo(movementType.Pos.From(movement));
                if (!moveFromZero)
                {
                    addr.CopyTo(movementType.Neg.From(movement));
                    movementType.Normalize(movement);
                }
            }

            var isFirstStep = flags[0];
            var isFollowUp = flags[1];
            isFirstStep.Init(1);
            isFollowUp.Init(0);

            var nextCxt = ir.CreateCxt();
            var nextAddr = nextCxt.Alloc(cfg.addrSize);
            var nextFlags = nextCxt.Alloc(2);
            var nextMovement = nextCxt.Alloc(movementType.Size);

            var _moveFrom = new List<BFVar> { addr, flags };
            if (addrDest != null)
                _moveFrom.Add(!moveFromZero ? movement : movementType.Pos.From(movement));
            _moveFrom.AddRange(moveFrom);
            moveFrom = _moveFrom;

            var moveTo = new List<BFVar> { nextAddr, nextFlags };
            if (addrDest != null)
                moveTo.Add(!moveFromZero ? nextMovement : movementType.Pos.From(nextMovement));
            for (int i = moveTo.Count; i < moveFrom.Count; i++)
                moveTo.Add(nextCxt.Alloc(moveFrom[i].Size));

            for (int i = 0; i < addrType.Size; i++)
            {
                var step = (int)Math.Pow(cfg.cellSize, i) * cfg.BlockSize;

                void doStep(int _step)
                {
                    addr[i].Change(_step > 0 ? 1 : -1);

                    isFollowUp.Init(1);

                    isFirstStep.If(() =>
                    {
                        isFirstStep.Init();
                        MoveData(moveFrom, moveTo, _step);
                        ir.Add(new ShiftContext(_step));
                        isFollowUp.Init(0);
                    });

                    isFollowUp.If(() =>
                    {
                        MoveData([.. moveTo.Select(x => x.ApplyShift(0))], moveTo, _step);
                        ir.Add(new ShiftContext(_step));
                    });
                }

                if (addrDest == null)
                {
                    addr[i].While(() => { doStep(-step); });
                }
                else
                {
                    if (!moveFromZero)
                    {
                        var left = movementType.Neg.From(movement)[i];
                        left.While(() =>
                        {
                            left.Minus();
                            doStep(-step);
                        });
                    }
                    var right = movementType.Pos.From(movement)[i];
                    right.While(() =>
                    {
                        right.Minus();
                        doStep(step);
                    });
                }
            }

            isFirstStep.If(() =>
            {
                ir.env.Error(BFGProgram.ErrCode.ERR_SAME_PTR);
            });

            foreach (var desc in moveFrom)
                (desc.Parent ?? desc).Dispose();

            ir.Add(new ShiftContext(0, nextCxt));

            addr.Rebind(nextAddr);
            movement.Rebind(nextMovement);
            flags.Rebind(nextFlags);
            for (int i = 0; i < moveFrom.Count; i++)
                moveFrom[i].Rebind(moveTo[i]);
        }

        private static void MoveData(List<BFVar> sources, List<BFVar> targets, int shift)
        {
            bool forward = shift > 0;
            int start = forward ? sources.Count - 1 : 0;
            int end = forward ? -1 : sources.Count;
            int stepDelta = forward ? -1 : 1;

            for (int j = start; j != end; j += stepDelta)
            {
                var dstShifted = targets[j].ApplyShift(shift);
                if (shift > 0)
                    for (int i = sources[j].Size - 1; i >= 0; i--)
                        sources[j][i].MoveTo(dstShifted[i]);
                else
                    for (int i = 0; i < sources[j].Size; i++)
                        sources[j][i].MoveTo(dstShifted[i]);
            }
        }
    }
}