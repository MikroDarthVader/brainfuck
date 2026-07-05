namespace BFG
{
    internal class BFIR(BFContext? activeContext) : List<IRInst>
    {
        public BFContext? ActiveContext = activeContext;
        public string Compile()
        {
            BFBuilder bFBuilder = new BFBuilder();
            foreach (var inst in this)
                inst.Compile(bFBuilder);
            return bFBuilder.ToString();
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
        public readonly BFRootDescriptor descriptor;
        public readonly int pos;
        public readonly Func<int>? shiftFromParentCxt;

        public MoveTo(BFRootDescriptor descriptor, int pos, Func<int>? shiftFromParentCxt)
        {
            var activeContext = descriptor.Context.IR.ActiveContext;
            if (descriptor.Context != activeContext && shiftFromParentCxt == null)
                throw new InvalidOperationException("Cannot generate MoveTo for a foreign descriptor without a context shift function.");

            this.descriptor = new BFRootDescriptor(descriptor);
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
        public readonly int shift;

        public ShiftContext(int shift)
        {
            this.shift = shift;
        }

        public void Compile(BFBuilder host) => host.BFShiftContext(shift);
    }
}