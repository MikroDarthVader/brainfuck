namespace BFGo
{
    internal abstract class BFIR
    {
        protected abstract void _Add(IRInst inst);
        public abstract void While(Action code);
        public abstract bool Debuggable { get; }

        internal BFContext ActiveContext { get; private protected set; }
        internal readonly BFGCfg cfg;

        internal BFIR(BFGCfg cfg)
        {
            this.cfg = cfg;
            ActiveContext = CreateCxt();
        }

        internal void Add(IRInst inst)
        {
            if (inst is MoveTo moveInst && moveInst.descriptor.Context != ActiveContext && !moveInst.descriptor.isTransitional)
                throw new InvalidOperationException("Cannot generate MoveTo for a foreign descriptor without a context shift function.");

            _Add(inst);

            if (inst is ShiftContext)
                ActiveContext = (inst as ShiftContext)!.newContext ?? ActiveContext;
        }

        private int cxtCount = 0;
        internal BFContext CreateCxt()
        {
            var cxt = new BFContext(this, cxtCount);
            cxtCount++;
            return cxt;
        }
    }

    internal class BFIRGen : BFIR
    {
        public override bool Debuggable => false;
        private List<IRInst> insts = new List<IRInst>();

        public BFIRGen(BFGCfg cfg) : base(cfg) { }

        public string Compile()
        {
            BFGen bFBuilder = new BFGen();
            foreach (var inst in insts)
                inst.Compile(bFBuilder);
            return bFBuilder.ToString();
        }

        public override void While(Action code)
        {
            Add(new LoopStart());
            code();
            Add(new LoopEnd());
        }

        protected override void _Add(IRInst inst) => insts.Add(inst);
        public IRInst this[int ind] => insts[ind];
        public int Count => insts.Count;
    }
}
