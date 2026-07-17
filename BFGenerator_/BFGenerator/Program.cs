using BFGo;

class Program
{
    static void Main()
    {
        var bfg = new BFG(addrSize: 1, stackDens: 1, dataDens: 1, cellSize: 8);

        var cell = bfg.ActiveContext.Alloc(AllocatorKind.Stack);
        var addr = bfg.ActiveContext.Alloc(AllocatorKind.Stack);
        cell[0].Init(5);
        addr[0].Init(3);

        bfg.Go(addr, cell);

        Run(bfg);
    }

    static void Run(BFG bfg)
    {
        new BFRuntimeError(bfg.ActiveContext, BFRuntimeError.ErrCode.OK);

        string debugInfo = bfg.Dump();
        Console.WriteLine(debugInfo);

        string code = bfg.Compile();
        Console.WriteLine("=== Сгенерированный Brainfuck ===");
        Console.WriteLine(code);

        var vm = new BFDiagnostics(code, BFIOFormat.ASCII);
        Console.WriteLine("\n=== Выполнение ===");
        vm.Execute();
    }
}