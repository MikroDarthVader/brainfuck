namespace BFGen
{
    /// <summary>
    /// Main compiler facade. Manages static and dynamic contexts, pointer-based
    /// transitions and compilation of IR into Brainfuck code.
    /// </summary>
    public class BFG
    {
        private readonly BFIR ir;
        private readonly BFContext staticCxt;
        private readonly int addrSize;
        private readonly int stackDens;
        private readonly int dataDens;
        private readonly int cellBits;

        private BFIntType movementType;
        private BFUIntType addrType;
        private BFVar? addr;
        private BFVar? movement;
        private BFVar? flags;

        public BFContext Context => ir.ActiveContext;

        public BFG(int addrSize, int stackDens, int dataDens, int cellBits = 8)
        {
            if (cellBits <= 0 || cellBits > 32)
                throw new ArgumentException("Cell bits must be in range [1, 32] to prevent integer overflow during step calculation.");

            this.addrSize = addrSize;
            this.stackDens = stackDens;
            this.dataDens = dataDens;
            this.cellBits = cellBits;

            ir = new BFIR();
            staticCxt = ir.ActiveContext;

            movementType = new BFIntType(addrSize);
            addrType = new BFUIntType(addrSize);

            addr = null!;
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
                    throw new Exception("TODO: description");
                GoByPtr(addrDesc, moveFrom);
                GoStatic(moveFrom);
            }
        }

        public void GoStatic(List<BFVar> moveFrom)
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
            var zeroCxt = new BFContext(ir, stackDens, dataDens);
            addr = zeroCxt.Alloc(AllocatorKind.Stack, addrSize);
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

            var isFirstStep = flags[0]!;
            var isFollowUp = flags[1]!;
            isFirstStep.Init(1);
            isFollowUp.Init(0);

            var nextCxt = new BFContext(ir, stackDens, dataDens);
            var nextAddr = nextCxt.Alloc(AllocatorKind.Stack, addrSize);
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
                var step = (int)Math.Pow(Math.Pow(2, cellBits), i) * Context.BlockSize;

                var doStep = (int _step) =>
                {
                    addr[i]!.Change(_step > 0 ? 1 : -1);

                    isFollowUp.Init(1);

                    isFirstStep.If(() =>
                    {
                        /*isFirstStep.Plus(80);
                        isFirstStep.Print();
                        isFirstStep.Minus(80);*/
                        MoveData(moveFrom, moveTo, _step);

                        ir.Add(new ShiftContext(_step));
                        
                        isFollowUp.Init(0);
                    });

                    isFollowUp.If(() =>
                    {
                        MoveData(moveTo.Select(x => x.ApplyShift(0)).ToList(), moveTo, _step);

                        /*isFollowUp.Plus(92);
                        isFollowUp.Print();
                        isFollowUp.Minus(92);*/

                        ir.Add(new ShiftContext(_step));
                    });

                    /*isFirstStep.Plus(85);
                    isFirstStep.Print();
                    isFirstStep.Minus(85);*/
                };

                if (addrDest == null)
                {
                    addr[i]!.While(() => { doStep(-1); });
                    continue;
                }

                if (!moveFromZero)
                {
                    var left = movementType.Neg.From(movement)[i]!;
                    left.While(() =>
                    {
                        left.Minus();
                        /*left.Plus(10);
                        left.Print();
                        left.Minus(10);*/
                        doStep(-step);
                    });
                }
                var right = movementType.Pos.From(movement)[i]!;
                right.While(() =>
                {
                    right.Minus();
                    /*right.Plus(20);
                    right.Print();
                    right.Minus(20);*/
                    doStep(step);
                });
            }

            isFirstStep.If(() =>
            {
                new BFRuntimeError(Context, BFRuntimeError.ErrCode.ERR_SAME_PTR);
            });

            //foreach (var desc in moveFrom)
            //    desc.Dispose();

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

            for (int j = 1; j < sources.Count; j++)
            {
                int addrPrev = sources[j - 1].Context.Resolve(sources[j - 1], 0) + (sources[j - 1].cxtShift?.Invoke() ?? 0);
                int addrCurr = sources[j].Context.Resolve(sources[j], 0) + (sources[j].cxtShift?.Invoke() ?? 0);
                /*if (addrPrev + sources[j - 1].Size > addrCurr)
                    throw new InvalidOperationException($"MoveData: overlapping or unsorted blocks at indices {j - 1} and {j}");*/
            }

            for (int j = start; j != end; j += stepDelta)
                MoveBlock(sources[j], targets[j].ApplyShift(shift), shift);
        }

        private void MoveBlock(BFVar src, BFVar dstShifted, int shift)
        {
            if (shift > 0)
            {
                for (int i = src.Size - 1; i >= 0; i--)
                    src[i]!.MoveTo(dstShifted[i]!);
            }
            else
            {
                for (int i = 0; i < src.Size; i++)
                    src[i]!.MoveTo(dstShifted[i]!);
            }
        }

        /// <summary>
        /// Finalises IR construction and generates the resulting Brainfuck code.
        /// </summary>
        public string Compile()
        {
            //new BFRuntimeError(Context, BFRuntimeError.ErrCode.OK);
            return ir.Compile();
        }

        public string Dump()
        {
            return ir.Dump();
        }
    }
}