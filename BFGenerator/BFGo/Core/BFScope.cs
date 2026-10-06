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

        /// <summary>
        /// Allocates a new variable of the given size and emits IR that zeroes it.
        /// After Alloc returns, the variable is guaranteed to hold 0 in the
        /// generated program at this point in execution.
        /// </summary>
        public virtual BFVar Alloc(int size = 1)
        {
            var res = new BFVar(this, Size, size);
            Size += size;
            res.Zero();
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

        /// <summary>
        /// Allocates a new variable in this scope. Inherits auto-zero from
        /// <see cref="BFGenericScope.Alloc"/>: the variable holds 0 immediately
        /// after this call in the generated program.
        /// </summary>
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

        /// <summary>
        /// Allocates a new variable. The variable is auto-zeroed:
        /// it holds 0 immediately after this call in the generated program.
        /// </summary>
        public BFVar Alloc(int size = 1) => scope.Alloc(size);

        /// <summary>
        /// Allocates a new variable of the given type. Auto-zeroed, see Alloc(int).
        /// </summary>
        public BFVar Alloc(BFType ofType) => scope.Alloc(ofType);
        
        public void Dispose() => scope.Dispose();
    }
}
