namespace BFPcompiler
{
    /// <summary>
    /// Поддержка сервисных ячеек, динамической адресации
    /// Структура данных в текущем контексте:
    ///     block[0] = {address, stack[stackDensity раз], data},
    ///     block[1] = {stack[stackDensity + 1 раз], data},
    ///     block[2] = {stack[stackDensity + 1 раз], data},
    ///     ...
    /// Блоки размещаются на ленте последовательно.
    /// При смещении контекста через ShiftContext представления блоков смещаются соответственно
    /// </summary>
    public class BFPtr
    {
        public readonly int baseDataSize;
        public readonly BFstack stack;
        public int blockSize => stack.stackDensity + 2; // + data + address 
        private BFCompiler compiler;

        public BFPtr(int baseDataSize, int stackDensity)
        {
            this.baseDataSize = baseDataSize;
            stack = new BFstack(this, stackDensity);
            compiler = new BFCompiler();
        }

        public class BFstackData : BFdata, IDisposable
        {
            private readonly BFstack stack;
            public bool disposed { get; private set; }

            public BFstackData(BFPtr bfp) : 
                base(bfp.compiler, bfp.stack.Alloc(), bfp.baseDataSize) 
            {
                stack = bfp.stack;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposed) return;

                if (disposing)
                    stack.Free(this);

                disposed = true;
            }

            ~BFstackData() => Dispose(false);
        }

        /// <summary>
        /// Стек локальных переменных для функций адресации в рамках одного контекста.
        /// </summary>
        public class BFstack
        {
            public readonly int stackDensity;
            public int size { get; private set; }

            protected SortedSet<int> pendingDispose;
            protected BFPtr compiler;

            public BFstack(BFPtr compiler, int stackDensity)
            {
                this.compiler = compiler;
                this.stackDensity = stackDensity;

                pendingDispose = new SortedSet<int>();//reverse order
            }

            protected int CalcCellPos(int stackPos)
            {
                if (stackPos < stackDensity)
                    return stackPos + 1;

                int remainingIndex = stackPos - stackDensity;
                int blockNumber = remainingIndex / (stackDensity + 1);
                int positionInBlock = remainingIndex % (stackDensity + 1);

                return (stackDensity + 2) + blockNumber * (stackDensity + 2) + positionInBlock;
            }

            public int Alloc()
            {
                tryFreePending();

                int pos = CalcCellPos(size);
                size++;
                return pos;
            }

            public void Free(BFstackData data)
            {
                if (data.disposed)
                    throw new ObjectDisposedException(
                        nameof(data),
                        $"Attempt to free already disposed BFstackData object with position {data.pos}");

                if (data.pos == CalcCellPos(size - 1))
                    size--;
                else
                    pendingDispose.Add(data.pos);
            }

            protected bool tryFreePending()
            {
                while (pendingDispose.Count > 0)
                {
                    int pos = pendingDispose.Max;
                    int expectedPos = CalcCellPos(size - 1);

                    if (pos != expectedPos)
                        throw new InvalidOperationException(
                            $"Stack corruption: cannot free pos {pos}, expected {expectedPos} (size={size - 1})"
                        );

                    pendingDispose.Remove(pos);
                    size--;
                }
                return true;
            }
        }
    }
}
