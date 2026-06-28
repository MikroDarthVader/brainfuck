namespace BFGenerator
{
    public class BFIR
    {
        public interface IRInst { public void Compile(BFBuilder host); }

        public class Plus : IRInst { public void Compile(BFBuilder host) => host.BFPut('+'); }
        public class Minus : IRInst { public void Compile(BFBuilder host) => host.BFPut('-'); }
        public class Print : IRInst { public void Compile(BFBuilder host) => host.BFPut('.'); }
        public class Read : IRInst { public void Compile(BFBuilder host) => host.BFPut(','); }

        public class Func : List<IRInst>, IRInst
        {
            public void Compile(BFBuilder host)
            {
                foreach (var inst in this)
                    inst.Compile(host);
            }
        }

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
            public readonly BFAllocator allocator;
            public readonly int pos;

            public MoveTo(BFAllocator allocator, int pos)
            {
                this.allocator = allocator;
                this.pos = pos;
            }

            public void Compile(BFBuilder host) => host.BFMoveTo(allocator, pos);
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

        public readonly Func Main;
        public readonly BFContext baseContext;

        public BFIR(BFContext baseContext) 
        { 
            this.baseContext = baseContext;
            Main = new Func();
        }

        public string Compile()
        {
            BFBuilder bFBuilder = new BFBuilder(baseContext);
            Main.Compile(bFBuilder);
            return bFBuilder.ToString();
        }
    }
}
