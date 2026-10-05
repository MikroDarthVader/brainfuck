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
            return currIR.Context.StaticScope.Alloc(size);
        }
        public BFVar Alloc(BFType ofType) => Alloc(ofType.Size);

        public BFScope CreateScope()
        {
            if (currIR == null)
                throw new CompilerLifecycleException();
            return new BFScope(currIR.Context.CreateStackScope());
        }

        public BFVar GetData(int size = 1, int pos = 0)
        {
            if (currIR == null)
                throw new CompilerLifecycleException();

            return currIR.Context.GetData(size, pos);
        }

        public BFVar GetData(BFType ofType, int pos = 0) => GetData(ofType.Size, pos);

        public void Go(BFVar? addr)
        {
            if (currBFG == null)
                throw new CompilerLifecycleException();
            currBFG.Go(addr);
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
        public void Error(int error)
        {
            if (currBFG == null)
                throw new CompilerLifecycleException();

            using var scope = CreateScope();
            var errorDesc = scope.Alloc().Set(error).Print();
            Break(); //for debug runtime
            errorDesc.While(() => { }); //for bf runtime
        }

        public abstract void Code();

        public string Compile()
        {
            currIR = new BFIRGen(this);
            currBFG = new BFG(currIR);
            Code();

            var bf = (currIR as BFIRGen)!.Compile();

            currIR = null;
            currBFG = null;

            return bf;
        }

        public void Debug(BFIOFormat debugFormat)
        {
            currIR = new BFIRGen(this);
            currBFG = new BFG(currIR);
            Code();

            currIR = new BFIRDebugger(this, ((BFIRGen)currIR).GetIR(), debugFormat);
            currBFG = new BFG(currIR);
            Code();

            currIR = null;
            currBFG = null;
        }
    }
}
