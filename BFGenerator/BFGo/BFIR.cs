namespace BFGo
{
    internal interface BFIR
    {
        public abstract void Add(IRInst inst);
        public abstract void While(Action code);
    }

    public enum BFIOFormat
    {
        ASCII,
        Numeric,
        DetaledNumeric
    }

    internal class BFIRDebugger : BFIR
    {
        private readonly BFIOFormat IOFormat;
        private readonly BFG env;

        private readonly Dictionary<int, int> memStatic = new();
        private readonly Dictionary<int, int> memDynamic = new();

        private int dynCxtPos = 0, cellPos = 0;

        public BFIRDebugger(BFG env, BFIOFormat IOFormat)
        {
            this.IOFormat = IOFormat;
            this.env = env;
        }

        internal int GetValue(int addr)
        {
            if (env.ActiveContext == env.staticCxt)
            {
                if (addr > env.staticCxt.MaxSize)
                    return memDynamic.GetValueOrDefault(addr - env.staticCxt.MaxSize);
                else
                    return memStatic.GetValueOrDefault(addr);
            }
            else
            {
                if (addr + dynCxtPos < 0)
                    return memStatic.GetValueOrDefault(env.staticCxt.MaxSize + addr);
                else
                    return memDynamic.GetValueOrDefault(addr + dynCxtPos);
            }
        }

        private void SetValue(int val, int addr)
        {
            if (env.ActiveContext == env.staticCxt)
            {
                if (addr > env.staticCxt.MaxSize)
                    memDynamic[addr - env.staticCxt.MaxSize] = val;
                else
                    memStatic[addr] = val;
            }
            else
            {
                if (addr + dynCxtPos < 0)
                    memStatic[env.staticCxt.MaxSize + addr] = val;
                else
                    memDynamic[addr + dynCxtPos] = val;
            }
        }

        private int GetCurrValue() => GetValue(cellPos);
        private void SetCurrValue(int val) => SetValue(val, cellPos);

        public void While(Action code)
        {
            while (GetCurrValue() > 0)
                code(); //TODO: добавить проверку инвариантности команд итераций
        }

        public void Add(IRInst inst)
        {
            switch (inst)
            {
                case Plus p:
                    {
                        var val = GetCurrValue();
                        val += p.val;
                        val %= env.cellSize;
                        if (val < 0)
                            val += env.cellSize;
                        SetCurrValue(val);
                    }
                    break;

                case Minus m:
                    {
                        var val = GetCurrValue();
                        val -= m.val;
                        val %= env.cellSize;
                        if (val < 0)
                            val += env.cellSize;
                        SetCurrValue(val);
                    }
                    break;

                case MoveTo mv:
                    cellPos = mv.relativePos;
                    break;

                case ShiftContext sc:
                    if (sc.newContext != env.staticCxt && env.ActiveContext != env.staticCxt)
                        dynCxtPos += sc.shiftFromParentCxt();
                    break;

                case Print:
                    {
                        var val = GetCurrValue();
                        if (IOFormat == BFIOFormat.ASCII)
                            Console.Write((char)val);
                        else if (IOFormat == BFIOFormat.Numeric)
                            Console.WriteLine($"Print: {val}");
                        else
                            Console.WriteLine($"Print cell {cellPos}, context {env.ActiveContext.ID}: {val}");
                    }
                    break;

                case Read:
                    if (IOFormat == BFIOFormat.ASCII)
                        SetCurrValue(Console.Read());
                    else if (IOFormat == BFIOFormat.Numeric)
                    {
                        Console.Write("Read: ");
                        if (int.TryParse(Console.ReadLine(), out int val))
                            SetCurrValue(val);
                        else
                            SetCurrValue(0);
                    }
                    else
                    {
                        Console.Write($"Read cell {cellPos}, context {env.ActiveContext.ID}: ");
                        if (int.TryParse(Console.ReadLine(), out int val))
                            SetCurrValue(val);
                        else
                            SetCurrValue(0);
                    }
                    break;

                default:
                    break;
            }
        }
    }

    internal class BFIRGen : BFIR
    {
        private List<IRInst> instructions = new List<IRInst>();

        public string Compile()
        {
            BFGen bFBuilder = new BFGen();
            foreach (var inst in instructions)
                inst.Compile(bFBuilder);
            return bFBuilder.ToString();
        }

        public void Add(IRInst inst) => instructions.Add(inst);

        public void While(Action code)
        {
            Add(new LoopStart());
            code();
            Add(new LoopEnd());
        }

        private class LoopStart : IRInst { public string Compile(BFGen host) => host.BFPut('['); }
        private class LoopEnd : IRInst { public string Compile(BFGen host) => host.BFPut(']'); }
    }

    internal interface IRInst { public string Compile(BFGen host); }
    internal class Print : IRInst { public string Compile(BFGen host) => host.BFPut('.'); }
    internal class Read : IRInst { public string Compile(BFGen host) => host.BFPut(','); }

    internal class Plus : IRInst
    {
        internal int val;
        public Plus(int val = 1) { this.val = val; }
        public string Compile(BFGen host) => host.BFPut('+', val);
    }

    internal class Minus : IRInst
    {
        internal int val;
        public Minus(int val = 1) { this.val = val; }
        public string Compile(BFGen host) => host.BFPut('-', val);
    }

    internal class MoveTo : IRInst
    {
        public readonly BFVar descriptor;
        public readonly int allocPos;
        public readonly Func<int>? shiftFromParentCxt;

        public MoveTo(BFVar descriptor, int allocPos, Func<int>? shiftFromParentCxt)
        {
            var activeContext = descriptor.Context.env.ActiveContext;
            if (descriptor.Context != activeContext && shiftFromParentCxt == null)
                throw new InvalidOperationException("Cannot generate MoveTo for a foreign descriptor without a context shift function.");

            this.descriptor = new BFVar(descriptor);
            this.allocPos = allocPos;
            this.shiftFromParentCxt = shiftFromParentCxt;
        }

        public int relativePos
        {
            get
            {
                var localPos = descriptor.Context.Resolve(descriptor, allocPos);
                var shift = 0; 
                if(shiftFromParentCxt != null)
                    shift = shiftFromParentCxt();
                return localPos + shift;
            }
        }
        public string Compile(BFGen host)
        {
            return host.BFMoveTo(relativePos);
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

        public string Compile(BFGen host) => host.BFShiftContext(shiftFromParentCxt());
    }
}
