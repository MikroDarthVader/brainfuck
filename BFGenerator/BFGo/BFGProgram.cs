namespace BFGo
{
    public class BFGCfg
    {
        public readonly int addrSize;
        public readonly int stackDens;
        public readonly int dataDens;
        public readonly int cellSize;

        public BFGCfg(int addrSize, int stackDens, int dataDens, int cellSize)
        {
            if (addrSize <= 0)
                throw new ArgumentException("Address size must be greater than zero.", nameof(addrSize));
            if (stackDens <= 0)
                throw new ArgumentException("Stack density must be greater than zero.", nameof(stackDens));
            if (dataDens <= 0)
                throw new ArgumentException("Data density must be greater than zero.", nameof(dataDens));
            if (cellSize <= 0)
                throw new ArgumentException("Cell size must be greater than zero.", nameof(cellSize));

            this.addrSize = addrSize;
            this.stackDens = stackDens;
            this.dataDens = dataDens;
            this.cellSize = cellSize;
        }

        public int BlockSize => stackDens + dataDens;
    }

    public abstract class BFGProgram
    {
        public readonly BFGCfg cfg;

        protected BFGProgram(BFGCfg cfg)
        {
            this.cfg = cfg;
        }

        protected BFGProgram(int addrSize, int stackDens, int dataDens, int cellSize)
        {
            cfg  = new BFGCfg(addrSize, stackDens, dataDens, cellSize);
        }

        public abstract void Code(BFG env);

        public string Compile()
        {
            var bfir = new BFIRGen(cfg);
            Code(new BFG(bfir));
            return bfir.Compile();
        }
        
        public void Debug(BFIOFormat debugFormat)
        {
            var bfir = new BFIRGen(cfg);
            var compileEnv = new BFG(bfir);
            Code(compileEnv);
            var debugger = new BFIRDebugger(bfir, debugFormat);
            var debugEnv = new BFG(debugger);
            Code(debugEnv);
        }
    }
}
