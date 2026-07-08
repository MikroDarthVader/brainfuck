using BFGen;

class Program
{
    static void Main(string[] args)
    {
        var bfg = new BFG(addrSize: 2, stackDens: 1, dataDens: 1, cellBits: 8);

        // Статический контекст: выделяем двухразрядный адрес и переменную-маркер
        var addr = bfg.Context.Alloc(AllocatorKind.Data, size: 2);
        var marker = bfg.Context.Alloc(AllocatorKind.Stack, size: 1);

        addr[0].Init(0x01);   // младший байт = 0
        addr[1].Init(0x05);   // старший байт = 5   → адрес 1280 (0x0500)
        marker[0].Init(99);   // не тащим, остаётся в статике

        // Переход по адресу 1280 без перемещения marker'а
        bfg.Go(addr);

        // Теперь мы в динамическом контексте по адресу 1280.
        // Выделяем две пользовательские переменные: одна в Stack, одна в Data.
        var dynStack = bfg.Context.Alloc(AllocatorKind.Stack, size: 1);
        dynStack[0].Init(111);
        var dynData = bfg.Context.Alloc(AllocatorKind.Data, size: 1);
        dynData[0].Init(222);

        // Выводим их, чтобы убедиться, что они живые.
        dynStack[0].Print();  // ожидаем 111
        dynData[0].Print();   // ожидаем 222

        Run(bfg);
    }

    static void Run(BFG bfg)
    {
        var code = bfg.Compile();
        Console.WriteLine(bfg.Dump());

        Console.WriteLine("=== CODE GENERATED ===");
        Console.WriteLine(code);

        var vm = new BFDiagnostics(code, BFIOFormat.RawNumeric);
        Console.WriteLine("\n=== DIAGNOSTIC VM WINDOW ===");
        vm.Execute();
    }
}
