using System.Diagnostics;
using System.Runtime.CompilerServices;

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

    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public class CompilerLifecycleException([CallerMemberName] string operation = "") : 
        InvalidOperationException($"Compiler Execution Out of Bounds:\n" +
                   $"Cannot execute '{operation}' outside of an active compilation pass.\n" +
                   $"Ensure this operation is called exclusively within the Code() scope during pipeline execution.")
    {
    }

    public abstract class BFGProgram
    {
        public readonly BFGCfg cfg;
        internal BFIR? currIR;
        internal BFG? currBFG;
        public bool Debugging => currIR?.Debuggable ?? false;

        protected BFGProgram(BFGCfg cfg)
        {
            this.cfg = cfg;
        }

        protected BFGProgram(int addrSize, int stackDens, int dataDens, int cellSize)
        {
            cfg = new BFGCfg(addrSize, stackDens, dataDens, cellSize);
        }

        public BFVar Alloc(int size = 1)
        {
            if (currIR == null)
                throw new CompilerLifecycleException();
            return currIR.ActiveContext.Alloc(size);
        }
        public BFVar Alloc(BFType ofType) => Alloc(ofType.Size);

        public BFVar GetData(int size = 1, int pos = 0)
        {
            if (currIR == null)
                throw new CompilerLifecycleException();
            if (currIR.ActiveContext is not BFDynamicContext)
                throw new InvalidOperationException("GetData could be exequted only in dynamic contexts");

            return (currIR.ActiveContext as BFDynamicContext)!.GetData(size, pos);
        }
        public BFVar GetData(BFType ofType, int pos = 0) => GetData(ofType.Size, pos);

        public void GoFromStatic(BFVar addr, BFVar[]? move = null)
        {
            if (currBFG == null)
                throw new CompilerLifecycleException();
            currBFG.GoFromStatic(addr, move);
        }

        public void Go(BFVar addr, BFVar[]? move = null)
        {
            if (currBFG == null)
                throw new CompilerLifecycleException();
            currBFG.Go(addr, move);
        }

        public void GoStatic(BFVar[]? move = null)
        {
            if (currBFG == null)
                throw new CompilerLifecycleException();
            currBFG.GoStatic(move);
        }

        [DebuggerHidden]
        public void Break()
        {
            if (currBFG == null)
                throw new CompilerLifecycleException();

            if (Debugging && Debugger.IsAttached)
                Debugger.Break();
        }

        /// <summary>
        /// Emits a runtime crash trap. Prints the error code and locks the execution thread.
        /// </summary>
        public void Error(ErrCode error)
        {
            if (currBFG == null)
                throw new CompilerLifecycleException();

            using var errorCellDesc = Alloc();
            var errorCell = errorCellDesc[0];

            errorCell.Init((byte)error);
            errorCell.Print();

            Break(); //for debug runtime
            errorCell.While(() => { }); //for bf runtime
        }

        public abstract void Code();

        public string Compile()
        {
            currIR = new BFIRGen(cfg, this);
            currBFG = new BFG(currIR);
            Code();

            var bf = (currIR as BFIRGen)!.Compile();

            currIR = null;
            currBFG = null;

            return bf;
        }

        public void Debug(BFIOFormat debugFormat)
        {
            currIR = new BFIRGen(cfg, this);
            currBFG = new BFG(currIR);
            Code();

            currIR = new BFIRDebugger((BFIRGen)currIR, debugFormat);
            currBFG = new BFG(currIR);
            Code();

            currIR = null;
            currBFG = null;
        }

        public enum ErrCode
        {
            OK = 0,
            ERR_SAME_PTR
        }
    }
}
