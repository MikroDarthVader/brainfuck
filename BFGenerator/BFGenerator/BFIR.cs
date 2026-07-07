using System.Text;

namespace BFGen
{
    internal class BFIR
    {
        public BFContext ActiveContext { get; private set; }

        private List<IRInst> insts = new List<IRInst>();

        public BFIR() => ActiveContext = new BFContext(this) { };

        public string Compile()
        {
            BFBuilder bFBuilder = new BFBuilder();
            foreach (var inst in insts)
                inst.Compile(bFBuilder);
            return bFBuilder.ToString();
        }

        public void Add(IRInst inst)
        {
            insts.Add(inst);
            if (inst is ShiftContext)
                ActiveContext = (inst as ShiftContext)!.newContext ?? ActiveContext;
        }

        public string Dump()
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== CONTEXT METRICS ===");
            // Находим все уникальные контексты, которые упоминаются в инструкциях или активны
            var contexts = insts.OfType<MoveTo>().Select(m => m.descriptor.Context)
                .Concat(insts.OfType<ShiftContext>().Select(s => s.newContext).Where(c => c != null))
                .Concat(new[] { ActiveContext })
                .DistinctBy(c => c!.ID)
                .ToList();

            foreach (var cxt in contexts)
            {
                if (cxt == null) continue;
                sb.AppendLine($"Context ID: {cxt.ID} | MaxSize: {cxt.MaxSize} | BlockSize: {cxt.BlockSize} (Data: {cxt.dataDens}, Stack: {cxt.stackDens})");
            }
            sb.AppendLine("=== BFIR DUMP (with generated BF) ===");
            sb.AppendLine($"{"Idx",-6} {"Instruction",-25} {"Details",-45} {"Generated BF"}");
            int simulatedPos = 0;

            for (int i = 0; i < insts.Count; i++)
            {
                var inst = insts[i];
                string detail = "";
                string bf = "";

                // Генерируем BF для инструкции
                var builder = new BFBuilder();
                inst.Compile(builder);
                bf = builder.ToString();

                if (inst is MoveTo moveTo)
                {
                    var desc = moveTo.descriptor;
                    int raw = desc.Context.Resolve(desc, moveTo.pos);
                    int shiftAmt = moveTo.shiftFromParentCxt?.Invoke() ?? 0;
                    int target = raw + shiftAmt;
                    int delta = target - simulatedPos;
                    detail = $"MoveTo addr={target} (raw={raw}, shift={shiftAmt}) delta={delta:+0;-#;0}";
                    simulatedPos = target;
                }
                else if (inst is ShiftContext shiftCxt)
                {
                    int shiftAmt = shiftCxt.shiftFromParentCxt();
                    detail = $"ShiftContext by {shiftAmt:+0;-#;0}, newCtx={shiftCxt.newContext?.ID ?? -1}";
                    // ShiftContext не меняет позицию в BFBuilder
                }
                else if (inst is Plus plus)
                {
                    detail = $"Plus {plus.val}";
                }
                else if (inst is Minus minus)
                {
                    detail = $"Minus {minus.val}";
                }
                else
                {
                    detail = inst.GetType().Name;
                }

                sb.AppendLine($"{i,-6} {inst.GetType().Name,-25} {detail,-45} {bf}");
            }
            sb.AppendLine("========================================\n");

            return sb.ToString();
        }


    }

    internal interface IRInst { public void Compile(BFBuilder host); }
    internal class Print : IRInst { public void Compile(BFBuilder host) => host.BFPut('.'); }
    internal class Read : IRInst { public void Compile(BFBuilder host) => host.BFPut(','); }
    internal class LoopStart : IRInst { public void Compile(BFBuilder host) => host.BFPut('['); }
    internal class LoopEnd : IRInst { public void Compile(BFBuilder host) => host.BFPut(']'); }

    internal class Plus : IRInst
    {
        internal int val;
        public Plus(int val = 1) { this.val = val; }
        public void Compile(BFBuilder host) => host.BFPut('+', val);
    }

    internal class Minus : IRInst
    {
        internal int val;
        public Minus(int val = 1) { this.val = val; }
        public void Compile(BFBuilder host) => host.BFPut('-', val);
    }

    internal class MoveTo : IRInst
    {
        public readonly BFVar descriptor;
        public readonly int pos;
        public readonly Func<int>? shiftFromParentCxt;

        public MoveTo(BFVar descriptor, int pos, Func<int>? shiftFromParentCxt)
        {
            var activeContext = descriptor.Context.IR.ActiveContext;
            if (descriptor.Context != activeContext && shiftFromParentCxt == null)
                throw new InvalidOperationException("Cannot generate MoveTo for a foreign descriptor without a context shift function.");

            this.descriptor = new BFVar(descriptor);
            this.pos = pos;
            this.shiftFromParentCxt = shiftFromParentCxt;
        }

        public void Compile(BFBuilder host)
        {
            int physicalAddress = descriptor.Context.Resolve(descriptor, pos) + (shiftFromParentCxt != null ? shiftFromParentCxt() : 0);
            host.BFMoveTo(physicalAddress);
        }
    }


    internal class ShiftContext : IRInst
    {
        public readonly Func<int> shiftFromParentCxt;
        public readonly BFContext? newContext;

        public ShiftContext(int shift, BFContext? newContext = null)
        {
            shiftFromParentCxt = () => shift;
            this.newContext = newContext;
        }

        public ShiftContext(Func<int> shift, BFContext? newContext = null)
        {
            shiftFromParentCxt = shift;
            this.newContext = newContext;
        }

        public void Compile(BFBuilder host) => host.BFShiftContext(shiftFromParentCxt());
    }
}