using BFGen;

class Program
{
    static void Main(string[] args)
    {
        var bfg = new BFG(1, 1, 1);
        BFVar x = bfg.Context.Alloc(AllocatorKind.Data, 1);
        x[0]!.Input();
        x[0]!.Plus(1);
        x[0]!.Print();
        BFVar y = bfg.Context.Alloc(AllocatorKind.Stack, 2);
        y[0]!.Init(0x41);
        y[1]!.Init(0x42);

        bfg.Go(x, y);

        y[0]!.Print();
        y[1]!.Print();

        bfg.Go(null);

        x[0]!.Plus(33);
        x[0]!.Print();

        Console.WriteLine(bfg.Compile());
    }
}
