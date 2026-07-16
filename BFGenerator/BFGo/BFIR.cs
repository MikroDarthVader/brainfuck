using System.Diagnostics;
using System.Text;

namespace BFGo
{
    internal class BFIR
    {
        public BFContext ActiveContext { get; private set; }

        private List<IRInst> instructions = new List<IRInst>();
        private BFIRDebugger? debugger;

        public BFIR(bool debug = false)
        {
            ActiveContext = new BFContext(this) { };
            debugger = debug ? new BFIRDebugger(ActiveContext) : null;
        }
        public string Compile()
        {
            BFBuilder bFBuilder = new BFBuilder();
            foreach (var inst in instructions)
                inst.Compile(bFBuilder);
            return bFBuilder.ToString();
        }

        public void Add(IRInst inst)
        {
            instructions.Add(inst);
            if (inst is ShiftContext)
                ActiveContext = (inst as ShiftContext)!.newContext ?? ActiveContext;
            debugger?.ProcessInst(inst);
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
