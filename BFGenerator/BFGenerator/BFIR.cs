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
            if(inst is ShiftContext)
                ActiveContext = (inst as ShiftContext)!.newContext ?? ActiveContext;
        }
    }

    internal interface IRInst { public void Compile(BFBuilder host); }
    internal class Print : IRInst { public void Compile(BFBuilder host) => host.BFPut('.'); }
    internal class Read : IRInst { public void Compile(BFBuilder host) => host.BFPut(','); }
    internal class LoopStart : IRInst { public void Compile(BFBuilder host) => host.BFPut('['); }
    internal class LoopEnd : IRInst { public void Compile(BFBuilder host) => host.BFPut(']'); }

    internal class Plus : IRInst
    {
        int val;
        public Plus(int val = 1)
        {
            this.val = val;
        }

        public void Compile(BFBuilder host)
        {
            host.BFPut('+', val);
        }
    }

    internal class Minus : IRInst
    {
        int val;
        public Minus(int val = 1)
        {
            this.val = val;
        }

        public void Compile(BFBuilder host)
        {
            host.BFPut('-', val);
        }
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
            // Чистый локальный адрес + сдвиг (если он есть)
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