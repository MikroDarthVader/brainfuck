namespace BFGenerator
{
    public class BFIR : List<IRInst>
    {
        public readonly BFContext baseContext;

        public BFIR(BFContext baseContext) 
        { 
            this.baseContext = baseContext;
        }

        public string Compile()
        {
            BFBuilder bFBuilder = new BFBuilder(baseContext);
            foreach(var inst in this)
                inst.Compile(bFBuilder);
            return bFBuilder.ToString();
        }
    }


    public interface IRInst { public void Compile(BFBuilder host); }
    public class Plus : IRInst { public void Compile(BFBuilder host) => host.BFPut('+'); }
    public class Minus : IRInst { public void Compile(BFBuilder host) => host.BFPut('-'); }
    public class Print : IRInst { public void Compile(BFBuilder host) => host.BFPut('.'); }
    public class Read : IRInst { public void Compile(BFBuilder host) => host.BFPut(','); }

    public class While : List<IRInst>, IRInst
    {
        public void Compile(BFBuilder host)
        {
            host.BFPut('[');
            foreach (var inst in this)
                inst.Compile(host);
            host.BFPut(']');
        }
    }

    public class MoveTo : IRInst
    {
        public readonly BFMemoryDescriptor descriptor;
        public readonly int pos; // смещение внутри блока

        public MoveTo(BFMemoryDescriptor descriptor, int pos = 0)
        {
            this.descriptor = descriptor;
            this.pos = pos;
        }

        public void Compile(BFBuilder host)
        {
            int physicalAddress = descriptor.Context.Resolve(descriptor, pos);
            host.BFMoveTo(physicalAddress);
        }
    }

    public class ShiftTo : IRInst
    {
        public readonly BFContext context;
        public readonly int shift;

        public ShiftTo(BFContext context, int shift)
        {
            this.context = context;
            this.shift = shift;
        }

        public void Compile(BFBuilder host) => host.BFShiftContext(shift, context);
    }
}
