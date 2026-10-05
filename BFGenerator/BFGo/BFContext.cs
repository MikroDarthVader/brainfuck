namespace BFGo
{
    internal class BFContext
    {
        internal readonly BFGenericScope StaticScope;
        private readonly BFMemView DataView;
        internal readonly BFIR ir;
        private readonly Stack<BFStackScope> StackScopes = new();
        private bool _atRoot = true;
        internal bool AtRoot => _atRoot;
        internal void SetAtRoot(bool value) => _atRoot = value;

        internal int StackSize => StackScopes.Count > 0 ? StackScopes.Peek().BaseAddr + StackScopes.Peek().Size : 0;

        private BFGCfg cfg => ir.env.cfg;

        internal BFContext(BFIR ir)
        {
            this.ir = ir;
            StaticScope = new BFGenericScope(this);
            DataView = new BFMemView(this);
        }


        internal int Resolve(BFMemView memView, int addr)
        {
            if (memView == DataView) return ResolveData(addr);
            if (memView == StaticScope)
            {
                // Static addresses are only meaningful in the root frame: outside it,
                // a negative logical address points to the neighbour context's tail,
                // not to static memory. The compiler tracks frame position and throws
                // eagerly if static is touched from a non-root frame.
                if (!_atRoot)
                    throw new InvalidOperationException(
                        "Static memory is only accessible from the root context. " +
                        "Call Go(null) to return to root.");
                return ResolveStatic(addr);
            }
            if (memView is BFStackScope && memView.Context == this) return ResolveStack(addr);
            
            throw new InvalidOperationException($"Cannot resolve memory view: unknown view or view belongs to a different context.");
        }

        internal int ResolveStack(int addr) => (addr / cfg.stackDens) * cfg.BlockSize + addr % cfg.stackDens;
        internal int ResolveData(int addr) => (addr / cfg.dataDens) * cfg.BlockSize + cfg.stackDens + addr % cfg.dataDens;
        private int ResolveStatic(int addr) => -1 - addr;

        internal bool IsStatic(int pos) => pos < 0;
        internal bool IsStack(int pos) => pos >= 0 && pos % cfg.BlockSize < cfg.stackDens;
        internal bool IsData(int pos) => pos >= 0 && pos % cfg.BlockSize >= cfg.stackDens;

        internal BFStackScope CreateStackScope()
        {
            var newTop = new BFStackScope(this, StackSize);
            StackScopes.Push(newTop);
            return newTop;
        }

        internal void OnScopeDispose(BFStackScope s)
        {
            if (StackScopes.Count == 0 || StackScopes.Peek() != s)
                throw new InvalidOperationException("Not the active scope.");
            StackScopes.Pop();
        }

        internal bool IsActive(BFStackScope scope) => StackScopes.Count > 0 && scope == StackScopes.Peek();

        internal BFVar GetData(int size, int pos) => new(DataView, pos, size);
    }
}
