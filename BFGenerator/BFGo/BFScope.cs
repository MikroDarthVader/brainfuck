namespace BFGo
{
    internal class BFMemView
    {
        internal BFContext Context { get; }
        internal BFMemView(BFContext ctx) => Context = ctx;
        internal virtual int Resolve(int localAddr) => Context.Resolve(this, localAddr);
    }

    internal class BFGenericScope : BFMemView
    {
        internal int Size { get; private protected set; }

        internal BFGenericScope(BFContext ctx) : base(ctx) { }

        public virtual BFVar Alloc(int size = 1)
        {
            var res = new BFVar(this, Size, size);
            Size += size;
            return res;
        }

        public virtual BFVar Alloc(BFType ofType) => Alloc(ofType.Size);
    }

    internal class BFStackScope : BFGenericScope, IDisposable
    {
        private bool _disposed;
        internal readonly int BaseAddr;

        internal BFStackScope(BFContext ctx, int baseAddr) : base(ctx)
        {
            BaseAddr = baseAddr;
        }

        public override BFVar Alloc(int size = 1)
        {
            if (_disposed) throw new InvalidOperationException("Scope disposed.");
            if (!Context.IsActive(this))
                throw new InvalidOperationException("Only the innermost active scope may allocate.");
            return base.Alloc(size);
        }

        internal override int Resolve(int localAddr) => Context.Resolve(this, BaseAddr + localAddr);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Context.OnScopeDispose(this);
            GC.SuppressFinalize(this);
        }
    }

    public sealed class BFScope : IDisposable
    {
        private readonly BFStackScope scope;
        internal BFScope(BFStackScope scope) { this.scope = scope; }
        public BFVar Alloc(int size = 1) => scope.Alloc(size);
        public BFVar Alloc(BFType ofType) => scope.Alloc(ofType);
        public void Dispose() => scope.Dispose();
    }
}
