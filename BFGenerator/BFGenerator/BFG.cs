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
                    GoByPtr(addrDesc.ApplyShift(()=> -staticCxt.MaxSize), moveFrom, true);
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

        private void GoStatic(List<BFVar> moveFrom)
        {
            var shift = () => -staticCxt.MaxSize;
            foreach (BFVar desc in moveFrom)
            {
                BFVar tmp = staticCxt.Alloc(AllocatorKind.Stack, desc.Size);
                desc.MoveTo(tmp.ApplyShift(shift));
                desc.Rebind(tmp);
            }
            ir.Add(new ShiftContext(shift, staticCxt));
            addr = movement = flags = null;
        }

        private void GoDynamic(List<BFVar> moveFrom)
        {
            var zeroCxt = new BFContext(ir, stackDens, dataDens);
            addr = zeroCxt.Alloc(AllocatorKind.Stack, addrSize);
            movement = zeroCxt.Alloc(AllocatorKind.Stack, movementType.Size);
            flags = zeroCxt.Alloc(AllocatorKind.Stack, 2);

            foreach (var desc in moveFrom)
                desc.Rebind(zeroCxt.Alloc(AllocatorKind.Stack, desc.Size).ApplyShift(() => -staticCxt.MaxSize));

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
            var nextMovement = nextCxt.Alloc(AllocatorKind.Stack, movementType.Size);
            var nextFlag = nextCxt.Alloc(AllocatorKind.Stack, 2);

            var moveTo = addrDest != null ? new List<BFVar>() { nextAddr, !moveFromZero ? nextMovement : movementType.Pos.From(nextMovement), nextFlag } :
                                            new List<BFVar>() { nextAddr, nextFlag };

            foreach (var desc in moveFrom)
                moveTo.Add(nextCxt.Alloc(AllocatorKind.Stack, desc.Size));

            var _moveFrom = addrDest != null ? new List<BFVar>() { addr, !moveFromZero ? movement : movementType.Pos.From(movement), flags } :
                                            new List<BFVar>() { addr, flags };
            _moveFrom.AddRange(moveFrom);
            moveFrom = _moveFrom;

            for (int i = 0; i < addrType.Size; i++)
            {
                var step = (int)Math.Pow(Math.Pow(2, cellBits), i) * Context.BlockSize;

                var doStep = (int dir) =>
                {
                    bool moveForward = dir < 0;

                    int start = moveForward ? 0 : moveFrom.Count - 1;
                    int end = moveForward ? moveFrom.Count : -1;
                    int stepDelta = moveForward ? 1 : -1;

                    addr[i]!.Change(dir);

                    isFollowUp.Init(1);
                    isFirstStep.If(() =>
                    {
                        for (int j = start; j != end; j += stepDelta)
                            moveFrom[j].MoveTo(moveTo[j].ApplyShift(step * dir));
                        isFollowUp.Init(0);
                    });
                    isFollowUp.If(() =>
                    {
                        for (int j = start; j != end; j += stepDelta)
                            moveTo[j].ApplyShift(0).MoveTo(moveTo[j].ApplyShift(step * dir));
                    });

                    ir.Add(new ShiftContext(step * dir));
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
                        doStep(-1);
                    });
                }
                var right = movementType.Pos.From(movement)[i]!;
                right.While(() =>
                {
                    right.Minus();
                    doStep(1);
                });
            }

            isFirstStep.If(() =>
            {
                new BFRuntimeError(Context, "ERR_SAME_PTR");
            });

            ir.Add(new ShiftContext(0, nextCxt));
            for (int i = 0; i < moveFrom.Count; i++)
                moveFrom[i].Rebind(moveTo[i]);
            if (addrDest == null || moveFromZero)
                movement.Rebind(nextMovement);
        }

        /// <summary>
        /// Finalises IR construction and generates the resulting Brainfuck code.
        /// </summary>
        public string Compile()
        {
            return ir.Compile();
        }
    }
}