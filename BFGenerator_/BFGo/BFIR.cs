namespace BFGo
{
    internal abstract class BFIR(BFContext defaultCxt)
    {
        public readonly BFContext staticCxt = defaultCxt;

        public abstract void Add(IRInst inst);
        public abstract void While(Action code);
    }

    internal class BFIRDebugger : BFIR
    {
        private List<int> staticMem = new();
        private List<int> dynMem = new();
        private int posInContext = 0;
        private int shift = 0;          
        private bool isDynamic = false; 

        public BFIRDebugger(BFContext defaultCxt) : base(defaultCxt) { }

        private List<int> CurrentMem => isDynamic ? dynMem : staticMem;
        private int CurrentRealIndex => isDynamic ? shift + posInContext : posInContext;

        public override void While(Action code)
        {
            while (CurrentMem[CurrentRealIndex] != 0)
                code();
        }

        void EnsureSize()
        {
            if (CurrentRealIndex >= CurrentMem.Count)
                CurrentMem.AddRange(Enumerable.Repeat(0, CurrentRealIndex - CurrentMem.Count + 1));
        }

        public override void Add(IRInst inst)
        {            
            if (CurrentRealIndex < 0)
                throw new InvalidOperationException("Negative tape index.");

            switch (inst)
            {
                case Plus p:
                    EnsureSize();
                    CurrentMem[CurrentRealIndex] += p.val;
                    break;

                case Minus m:
                    EnsureSize();
                    CurrentMem[CurrentRealIndex] -= m.val;
                    break;

                case MoveTo mv:
                    if (mv.pos < 0)
                        throw new InvalidOperationException("MoveTo position cannot be negative.");

                    posInContext = mv.pos;
                    break;

                case ShiftContext sc:
                    var prevDyn = isDynamic;
                    isDynamic = sc.newContext == staticCxt;

                    if (isDynamic && prevDyn)
                            shift += sc.shiftFromParentCxt(); 
                    if (!isDynamic && shift != 0)
                        throw new InvalidOperationException(
                            $"Cannot enter static context: dynamic shift is not zero ({shift}). " +
                            "All dynamic shifts must be balanced before switching to static context."
                        );
                    break;

                case Print:
                    Console.Write((char)CurrentMem[CurrentRealIndex]); //TODO: Добавить режимы отладки аски/нумерик
                    break;

                case Read:
                    CurrentMem[CurrentRealIndex] = Console.Read(); //TODO: Добавить режимы отладки аски/нумерик
                    break;

                default: break;
            }
        }
    }

    internal class BFIRGen : BFIR
    {
        private List<IRInst> instructions = new List<IRInst>();

        public BFIRGen(BFContext defaultCxt) : base(defaultCxt) { }

        public string Compile()
        {
            BFGen bFBuilder = new BFGen();
            foreach (var inst in instructions)
                inst.Compile(bFBuilder);
            return bFBuilder.ToString();
        }

        public override void Add(IRInst inst) => instructions.Add(inst);

        public override void While(Action code)
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
        public readonly int pos;
        public readonly Func<int>? shiftFromParentCxt;

        public MoveTo(BFVar descriptor, int pos, Func<int>? shiftFromParentCxt)
        {
            var activeContext = descriptor.Context.env.ActiveContext;
            if (descriptor.Context != activeContext && shiftFromParentCxt == null)
                throw new InvalidOperationException("Cannot generate MoveTo for a foreign descriptor without a context shift function.");

            this.descriptor = new BFVar(descriptor);
            this.pos = pos;
            this.shiftFromParentCxt = shiftFromParentCxt;
        }

        public string Compile(BFGen host)
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

        public string Compile(BFGen host) => host.BFShiftContext(shiftFromParentCxt());
    }
}
