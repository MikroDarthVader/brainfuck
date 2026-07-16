using BFGo;

class Program
{
    static void Main()
    {
        var bfg = new BFG(addrSize: 1, stackDens: 1, dataDens: 1, cellBits: 8);

        var cell = bfg.Context.Alloc(AllocatorKind.Stack);
        var addr = bfg.Context.Alloc(AllocatorKind.Stack);
        cell[0].Init(5);
        addr[0].Init(3);

        bfg.Go(addr, cell);

        Run(bfg);
    }

    static void Run(BFG bfg)
    {
        new BFRuntimeError(bfg.Context, BFRuntimeError.ErrCode.OK);

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