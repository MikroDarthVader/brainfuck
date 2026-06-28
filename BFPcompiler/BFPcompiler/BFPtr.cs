namespace BFPcompiler
{
    /// <summary>
    /// Поддержка сервисных ячеек, динамической адресации
    /// Структура данных в текущем контексте:
    ///     block[0] = {address[baseDataSize], stack[stackDensity], data[baseDataSize]},
    ///     block[1] = {stack[stackDensity + 1 раз], data[baseDataSize]},
    ///     block[2] = {stack[stackDensity + 1 раз], data[baseDataSize]},
    ///     ...
    /// Блоки размещаются на ленте памяти последовательно.
    /// При смещении контекста через ShiftContext представления блоков смещаются соответственно
    /// </summary>
    //public class BFPtr
    //{
    //    public readonly int baseDataSize;
    //    public readonly BFstack stack;
    //    public int blockSize => stack.stackDensity + baseDataSize * 2; // + data + address 
    //    private BFCompiler compiler;

    //    public BFPtr(int baseDataSize, int stackDensity)
    //    {
    //        this.baseDataSize = baseDataSize;
    //        stack = new BFstack(this, stackDensity);
    //        compiler = new BFCompiler();
    //    }
    //}

    //public abstract class BFSpace()
    //{
    //    public abstract int CellPosByIdx(int idx);
    //}

    //public abstract class BFContext
    //{
     
    //}

    //public abstract class BFType
    //{

    //}

    //public class BFVar : List<BFVar>
    //{
    //    public virtual int size
    //    {
    //        get
    //        {
    //            int size = 0;
    //            foreach (BFVar t in this)
    //                size += t.size;
    //            return size;
    //        }
    //    }

    //    public readonly int pos;
    //}

    ///// <summary>
    ///// Стек локальных переменных для функций адресации в рамках одного контекста.
    ///// </summary>
    //public class BFstack : BFSpace
    //{
    //    public int depth { get; private set; }

    //    protected BFContext context;

    //    public BFstack(BFContext context, )
    //    {
    //        this.context = context;
    //    }

    //    /*protected int CalcCellPos(int stackPos)
    //    {
    //        if (stackPos < stackDensity + host.baseDataSize) //skip address field
    //            return stackPos + host.baseDataSize;

    //        int remainingIndex = stackPos - stackDensity;
    //        int blockNumber = remainingIndex / (stackDensity + host.baseDataSize);
    //        int positionInBlock = remainingIndex % (stackDensity + host.baseDataSize);

    //        return host.blockSize + blockNumber * host.blockSize + positionInBlock;
    //    }*/

    //    public int Alloc(BFVar var)
    //    {
    //        int idx = CellPosByIdx(type.size);
    //        depth += type.size;

    //        return idx;
    //    }

    //    public void Free(BFVar var)
    //    {
    //        depth -= type.size;
    //    }
    //}
}
