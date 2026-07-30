using System.Diagnostics;

namespace BFGo
{
    /// <summary>
    /// Main compiler facade. Manages static and dynamic contexts, pointer-based
    /// transitions and compilation of IR into Brainfuck code.
    /// </summary>
    public class BFG
    {
        internal readonly BFIR ir;

        public bool Debugging => ir.Debuggable;
        internal BFGCfg cfg => ir.cfg;

        internal readonly BFContext staticCxt;

        private BFAddrComparerType movementType;
        private BFAddrType addrType;
        private BFVar? addr;
        private BFVar? movement;
        private BFVar? flags;

        internal BFG(BFIR ir)
        {
            this.ir = ir;

            staticCxt = ir.ActiveContext;

            movementType = new BFAddrComparerType(cfg.addrSize);
            addrType = new BFAddrType(cfg.addrSize);

            addr = null;
        }

        public BFVar Alloc(AllocatorKind kind, int size = 1) => ir.ActiveContext.Alloc(kind, size);

        [DebuggerHidden]
        public void Break() 
        {
            if (Debugging && Debugger.IsAttached)
                Debugger.Break();
        }

        /// <summary>
        /// Unified transition method.
        /// If <paramref name="addrDesc"/> is null – return to static context,
        /// otherwise – jump to the dynamic context whose address is stored in the descriptor.
        /// </summary>
        public void Go(BFVar? addrDesc = null,
                       params BFVar[] move)
        {
            var moveFrom = move.OrderBy(x => x.BaseIndex).ToList();
            if (addrDesc != null)
            {
                if (ir.ActiveContext == staticCxt)
                {
                    GoDynamic(moveFrom);
                    GoByPtr(addrDesc.ApplyShift(() => -staticCxt.MaxSize), moveFrom, true);
                }
                else
                    GoByPtr(addrDesc, moveFrom);
            }
            else
            {
                if (ir.ActiveContext == staticCxt)
                    throw new InvalidOperationException("Execution State Violation: Attempted to perform a static rollback transition (Go(null)), " +
                        "but the compiler is already operating inside the root static context. " +
                        "Ensure you only return to static from an active dynamic frame.");

                GoByPtr(addrDesc, moveFrom);
                GoStatic(moveFrom);
            }
        }

        private void GoStatic(List<BFVar> moveFrom)
        {
            foreach (BFVar desc in moveFrom)
            {
                BFVar tmp = staticCxt.Alloc(AllocatorKind.Stack, desc.Size);
                desc.MoveTo(tmp.ApplyShift(() => -staticCxt.MaxSize));
                desc.Rebind(tmp);
            }
            ir.Add(new ShiftContext(() => -staticCxt.MaxSize, staticCxt));

            addr = movement = flags = null;
        }

        private void GoDynamic(List<BFVar> moveFrom)
        {
            var zeroCxt = ir.CreateCxt();
            addr = zeroCxt.Alloc(AllocatorKind.Stack, cfg.addrSize);
            flags = zeroCxt.Alloc(AllocatorKind.Stack, 2);
            movement = zeroCxt.Alloc(AllocatorKind.Stack, movementType.Size);

            foreach (var desc in moveFrom)
                desc.Rebind(desc.ApplyShift(() => -staticCxt.MaxSize));

            ir.Add(new ShiftContext(() => staticCxt.MaxSize, zeroCxt));

            addr.Init();
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
            var nextAddr = nextCxt.Alloc(AllocatorKind.Stack, cfg.addrSize);
            var nextFlags = nextCxt.Alloc(AllocatorKind.Stack, 2);
            var nextMovement = nextCxt.Alloc(AllocatorKind.Stack, movementType.Size);

            var _moveFrom = new List<BFVar> { addr, flags };
            if (addrDest != null)
                _moveFrom.Add(!moveFromZero ? movement : movementType.Pos.From(movement));
            _moveFrom.AddRange(moveFrom);
            moveFrom = _moveFrom;

            var moveTo = new List<BFVar> { nextAddr, nextFlags };
            if (addrDest != null)
                moveTo.Add(!moveFromZero ? nextMovement : movementType.Pos.From(nextMovement));
            for (int i = moveTo.Count; i < moveFrom.Count; i++)
                moveTo.Add(nextCxt.Alloc(AllocatorKind.Stack, moveFrom[i].Size));

            for (int i = 0; i < addrType.Size; i++)
            {
                var step = (int)Math.Pow(Math.Pow(2, cfg.cellSize), i) * cfg.BlockSize;

                var doStep = (int _step) =>
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
                        MoveData(moveTo.Select(x => x.ApplyShift(0)).ToList(), moveTo, _step);
                        ir.Add(new ShiftContext(_step));
                    });
                };

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
                BFRuntimeError bFRuntimeError = new BFRuntimeError(ir.ActiveContext, BFRuntimeError.ErrCode.ERR_SAME_PTR);
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

        private void MoveData(List<BFVar> sources, List<BFVar> targets, int shift)
        {
            bool forward = shift > 0;
            int start = forward ? sources.Count - 1 : 0;
            int end = forward ? -1 : sources.Count;
            int stepDelta = forward ? -1 : 1;

            for (int j = start; j != end; j += stepDelta)
                MoveBlock(sources[j], targets[j].ApplyShift(shift), shift);
        }

        private void MoveBlock(BFVar src, BFVar dstShifted, int shift)
        {
            if (shift > 0)
            {
                for (int i = src.Size - 1; i >= 0; i--)
                    src[i].MoveTo(dstShifted[i]);
            }
            else
            {
                for (int i = 0; i < src.Size; i++)
                    src[i].MoveTo(dstShifted[i]);
            }
        }
    }
}