using BFGen;

class Program
{
    static void Main(string[] args)
    {
        var bfg = new BFG(2, 1, 1);

        var addr = bfg.Context.Alloc(AllocatorKind.Data, 2);
        var a = bfg.Context.Alloc(AllocatorKind.Data, 1);
        a[0].Init(71);
        a[0].Print();
        addr[0].Init(2);
        addr[1].Init(3);
        foreach (var c in addr.ToArray())
            c.Print();

        bfg.Go(addr);
        var addr1 = bfg.Context.Alloc(AllocatorKind.Stack, 2);
        addr1[0].Init(1);
        addr1[1].Init(0);
        foreach (var c in addr1.ToArray())
            c.Print();

        bfg.Go(addr1);
        addr1 = bfg.Context.Alloc(AllocatorKind.Stack, 2);
        addr1[0].Init(111);
        addr1[1].Init(11);
        foreach (var c in addr1.ToArray())
            c.Print();

        bfg.Go(addr1);
        var e = bfg.Context.Alloc(AllocatorKind.Stack, 1);
        e[0].Init(42);
        e[0].Print();

        bfg.Go(null, e);

        a[0].Print();
        e[0].Change(-15);
        e[0].Print();
        addr[0].Print();
        addr[1].Print();

        Run(bfg);
    }

    static void Run(BFG bfg)
    {
        var code = bfg.Compile();
        /*Console.WriteLine(bfg.Dump());

        Console.WriteLine("=== CODE GENERATED ===");
        Console.WriteLine(code);*/

        var vm = new BFDiagnostics(code, BFIOFormat.RawNumeric);
        Console.WriteLine("\n=== DIAGNOSTIC VM WINDOW ===");
        vm.Execute();
    }
}
