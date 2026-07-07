using BFGen;

class Program
{
    static void Main(string[] args)
    {
        var bfg = new BFG(1, 1, 1);
        BFVar x = bfg.Context.Alloc(AllocatorKind.Data, 1);
        x[0]!.Init(3);
        BFVar y = bfg.Context.Alloc(AllocatorKind.Stack, 1);
        y[0]!.Init(2);//2
        /*bfg.Go(x);
        x = bfg.Context.Alloc(AllocatorKind.Data, 1);
        x[0]!.Init(6);
        bfg.Go(x);
        x = bfg.Context.Alloc(AllocatorKind.Data, 1);
        x[0]!.Init(2);
        bfg.Go(x);*/


        bfg.Go(x, y);

        y[0]!.Plus(1);
        y[0]!.Print();//3
        x = bfg.Context.Alloc(AllocatorKind.Stack, 1);
        x[0]!.Init(11);

        bfg.Go(x, y);

        y[0]!.Plus(1);
        y[0]!.Print();//4
        x = bfg.Context.Alloc(AllocatorKind.Stack, 1);
        x[0]!.Init(5);

        bfg.Go(x, y);

        y[0]!.Plus(1);
        y[0]!.Print();//5

        bfg.Go(null, y);

        y[0]!.Plus(1);
        y[0]!.Print();//6

        Console.WriteLine(bfg.Dump());
        ExecuteBF(bfg.Compile());
    }

    static void ExecuteBF(string code)
    {
        byte[] tape = new byte[30000];
        int ptr = 0;
        int codeIdx = 0;

        Console.WriteLine("=== GENERATED BRAINFUCK CODE ===");
        Console.WriteLine(code);
        Console.WriteLine("================================\n");

        while (codeIdx < code.Length)
        {
            char cmd = code[codeIdx];
            if (cmd == '>') ptr++;
            else if (cmd == '<') ptr--;
            else if (cmd == '+') tape[ptr]++;
            else if (cmd == '-') tape[ptr]--;
            else if (cmd == '.') Console.Write($"[OUT:{tape[ptr]}] "); // выводим маркер вывода
            else if (cmd == ',') tape[ptr] = (byte)Console.Read();
            else if (cmd == '[' && tape[ptr] == 0)
            {
                int depth = 1;
                while (depth > 0)
                {
                    codeIdx++;
                    if (code[codeIdx] == '[') depth++;
                    if (code[codeIdx] == ']') depth--;
                }
            }
            else if (cmd == ']' && tape[ptr] != 0)
            {
                int depth = 1;
                while (depth > 0)
                {
                    codeIdx--;
                    if (code[codeIdx] == ']') depth++;
                    if (code[codeIdx] == '[') depth--;
                }
            }
            codeIdx++;
        }

        Console.WriteLine("\n--- EXECUTION FINISHED ---");
        Console.WriteLine($"Final ptr position: {ptr}");
        Console.WriteLine("Non-zero tape cells:");
        for (int i = 0; i < tape.Length; i++)
        {
            if (tape[i] != 0)
            {
                Console.WriteLine($"  [{i}] = {tape[i]}");
            }
        }
        Console.WriteLine("--------------------------");
    }


}
