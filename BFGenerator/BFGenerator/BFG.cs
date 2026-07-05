//namespace BFG
//{
//    /// <summary>
//    /// Main compiler facade. Manages static and dynamic contexts, pointer-based
//    /// transitions and compilation of IR into Brainfuck code.
//    /// </summary>
//    public class BFG
//    {
//        private readonly BFIR ir;
//        private readonly BFContext staticCtx;
//        private readonly int addrSize;
//        private readonly int stackDens;
//        private readonly int dataDens;
//        private readonly int cellBits;

//        private BFIntType movementType;
//        private BFUIntType addrType;
//        private BFRootDescriptor addr;
//        private BFRootDescriptor movement;
//        private BFRootDescriptor firstIterFlag;

//        public BFContext Context => ir.ActiveContext!;

//        public BFG(int addrSize, int stackDens, int dataDens, int cellBits = 8)
//        {
//            this.addrSize = addrSize;
//            this.stackDens = stackDens;
//            this.dataDens = dataDens;
//            this.cellBits = cellBits; //TODO: проверка cellBits > 0 с ошибкой при false

//            ir = new BFIR(null);
//            staticCtx = new BFContext(ir);
//            ir.ActiveContext = staticCtx; // start in static context

//            movementType = new BFIntType(addrSize);
//            addrType = new BFUIntType(addrSize);
//        }

//        /// <summary>
//        /// Unified transition method.
//        /// If <paramref name="addrDesc"/> is null – return to static context,
//        /// otherwise – jump to the dynamic context whose address is stored in the descriptor.
//        /// </summary>
//        public void Go(BFRootDescriptor? addrDesc = null,
//                       List<BFRootDescriptor>? copy = null,
//                       List<BFRootDescriptor>? move = null)
//        {

//        }

//        // ── Return to static context ─────────────────────────────
//        private void GoZero(List<BFRootDescriptor>? copy, List<BFRootDescriptor>? move)
//        {

//        }

//        private void GoByPtr(BFRootDescriptor addrDesc,
//                             List<BFRootDescriptor>? copy = null)//между динамиками
//        {
//            addr.CopyTo(movementType.Neg.From(movement));
//            addrDesc.CopyTo(movementType.Pos.From(movement));

//            movementType.Normalize(movement);

//            var nextCxt = new BFContext(ir, stackDens, dataDens);
//            var nextAddr = nextCxt.Alloc(AllocatorKind.Stack, addr.Size);
//            var nextMovement = nextCxt.Alloc(AllocatorKind.Stack, movement.Size);
//            var nextFlag = nextCxt.Alloc(AllocatorKind.Stack, firstIterFlag.Size);
//            nextAddr.isTransitional = true;

//            for (int i = 0; i < addrType.Size; i++)
//            {
//                While(leftMovement, () =>
//                {
//                    Minus(leftMovement);
//                    MoveVal(leftMovement, leftMovement - BlockSize);
//                    MoveWindowLeft();
//                    Zero(RightMovement);
//                });
//                While(RightMovement, () =>
//                {
//                    Minus(RightMovement);
//                    MoveVal(RightMovement, RightMovement + BlockSize);
//                    MoveWindowRight();
//                });
//            }
//            addr = Context.Alloc(AllocatorKind.Stack, addrType.Size);
//            addr.IsTransitional = true;
//            movement = Context.Alloc(AllocatorKind.Stack, movementType.Size);
//            movement.IsTransitional = true;
//        }

//        /// <summary>
//        /// Finalises IR construction and generates the resulting Brainfuck code.
//        /// </summary>
//        public string Compile()
//        {
//            return ir.Compile();
//        }
//    }
//}