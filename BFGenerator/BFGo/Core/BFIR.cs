namespace BFGo
{
    internal abstract class BFIR
    {
        internal abstract void Add(IRInst inst);
        public abstract void While(Action code);
        public abstract bool Debuggable { get; }

        internal readonly BFContext Context;

        internal readonly BFGProgram env;

        internal BFIR(BFGProgram env)
        {
            this.env = env;
            Context = new BFContext(this);
        }
    }

    internal class BFIRGen(BFGProgram env) : BFIR(env)
    {
        public override bool Debuggable => false;
        private readonly List<IRInst> insts = [];

        public string Compile()
        {
            BFGen bFBuilder = new();

            // Static cells occupy negative logical addresses.
            // Shift right by StaticScope.Size so that logical 0 maps to physical
            // StaticSize and no tape index goes negative.
            bFBuilder.BFShiftContext(Context.StaticScope.Size);

            foreach (var inst in insts)
                inst.Compile(bFBuilder);
            return bFBuilder.ToString();
        }

        public List<IRInst> GetIR() => insts;

        public override void While(Action code)
        {
            Add(new LoopStart());
            code();
            Add(new LoopEnd());
        }

        internal override void Add(IRInst inst) => insts.Add(inst);
        public IRInst this[int ind] => insts[ind];
        public int Count => insts.Count;
    }
}
