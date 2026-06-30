namespace BFGenerator
{
    public class BFIR : List<IRInst>
    {
        public string Compile()
        {
            BFBuilder bFBuilder = new BFBuilder();
            foreach (var inst in this)
                inst.Compile(bFBuilder);
            return bFBuilder.ToString();
        }
    }

    public interface IRInst { public void Compile(BFBuilder host); }
    public class Print : IRInst { public void Compile(BFBuilder host) => host.BFPut('.'); }
    public class Read : IRInst { public void Compile(BFBuilder host) => host.BFPut(','); }
    public class LoopStart : IRInst { public void Compile(BFBuilder host) => host.BFPut('['); }
    public class LoopEnd : IRInst { public void Compile(BFBuilder host) => host.BFPut(']'); }

    public class Plus : IRInst
    {
        int val;
        public Plus(int val = 1)
        {
            this.val = val;
        }

        public void Compile(BFBuilder host)
        {
            for (int i = 0; i < val; i++)
                host.BFPut('+');
        }
    }

    public class Minus : IRInst
    {
        int val;
        public Minus(int val = 1)
        {
            this.val = val;
        }

        public void Compile(BFBuilder host)
        {
            for (int i = 0; i < val; i++)
                host.BFPut('-');
        }
    }

    public class MoveTo : IRInst
    {
        public readonly BFMemoryDescriptor descriptor;
        public readonly int pos;

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

    public class ShiftContext : IRInst
    {
        public readonly int shift;

        public ShiftContext(int shift)
        {
            this.shift = shift;
        }

        public void Compile(BFBuilder host) => host.BFShiftContext(shift);
    }
}