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

            // ANSI Escape Constants (TrueColor RGB)
            const string RESET = "\u001b[0m";
            const string CYAN = "\u001b[36m";
            const string SKY_BLUE = "\u001b[38;2;135;206;250m"; // Небесно-голубой RGB(135,206,250)
            const string PASTEL_PINK = "\u001b[38;2;255;182;193m"; // Пастельно-розовый RGB(255,182,193)
            const string GREEN = "\u001b[32m";
            const string YELLOW = "\u001b[33m";
            const string GRAY = "\u001b[90m";

            sb.AppendLine($"\n{CYAN}=== BFIR DUMP (with generated BF) ==={RESET}");

            // Сетка таблицы без лишнего левого столбца
            sb.AppendLine($"{"Idx",-6} {"Details",-55} {"Generated BF"}");
            int simulatedPos = 0;

            var builder = new BFBuilder();

            for (int i = 0; i < insts.Count; i++)
            {
                var inst = insts[i];
                string detail = "";
                string color = GRAY;

                // Генерируем BF для инструкции
                var bfCode = inst.Compile(builder);

                if (inst is MoveTo moveTo)
                {
                    color = SKY_BLUE;
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
                    color = PASTEL_PINK;
                    int shiftAmt = shiftCxt.shiftFromParentCxt();
                    detail = $"ShiftContext by {shiftAmt:+0;-#;0}, newCtx={shiftCxt.newContext?.ID ?? -1}";
                }
                else if (inst is Plus plus)
                {
                    color = GREEN;
                    detail = $"Plus {plus.val}";
                }
                else if (inst is Minus minus)
                {
                    color = GREEN;
                    detail = $"Minus {minus.val}";
                }
                else
                {
                    color = YELLOW;
                    detail = inst.GetType().Name;
                }

                // Форматированный вывод строки
                sb.Append($"{i,-6} ");
                sb.Append($"{color}{detail,-55}{RESET}");
                sb.AppendLine($"{YELLOW}{bfCode}{RESET}");
            }
            sb.AppendLine($"{CYAN}========================================{RESET}\n");

            sb.AppendLine($"{CYAN}=== CONTEXT METRICS ==={RESET}");
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
            sb.AppendLine($"{CYAN}========================================{RESET}");

            return sb.ToString();
        }
    }


    internal interface IRInst { public string Compile(BFBuilder host); }
    internal class Print : IRInst { public string Compile(BFBuilder host) => host.BFPut('.'); }
    internal class Read : IRInst { public string Compile(BFBuilder host) => host.BFPut(','); }
    internal class LoopStart : IRInst { public string Compile(BFBuilder host) => host.BFPut('['); }
    internal class LoopEnd : IRInst { public string Compile(BFBuilder host) => host.BFPut(']'); }

    internal class Plus : IRInst
    {
        internal int val;
        public Plus(int val = 1) { this.val = val; }
        public string Compile(BFBuilder host) => host.BFPut('+', val);
    }

    internal class Minus : IRInst
    {
        internal int val;
        public Minus(int val = 1) { this.val = val; }
        public string Compile(BFBuilder host) => host.BFPut('-', val);
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

        public string Compile(BFBuilder host)
        {
            int physicalAddress = descriptor.Context.Resolve(descriptor, pos) + (shiftFromParentCxt != null ? shiftFromParentCxt() : 0);
            return host.BFMoveTo(physicalAddress);
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

        public string Compile(BFBuilder host) => host.BFShiftContext(shiftFromParentCxt());
    }
}
