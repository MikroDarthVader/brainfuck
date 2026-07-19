using BFGo;

class Program
{
    static void Main()
    {
        var bfg = new BFG(addrSize: 1, stackDens: 1, dataDens: 1, cellSize: 256, BFIOFormat.None);

        var cell = bfg.Alloc(AllocatorKind.Data)[0];
        cell.Init(3).While(() => {
            bfg.Alloc(AllocatorKind.Data)[0].Plus().Print();
            cell.Minus().Print();
        });

        if (!bfg.DebugMode)
            Compile(bfg);
    }

    static void Compile(BFG bfg)
    {
        //new BFRuntimeError(bfg.ActiveContext, BFRuntimeError.ErrCode.OK);

        string code = bfg.Compile();
        Console.WriteLine("=== Generated Brainfuck ===\n");
        Console.WriteLine(code);
        Console.WriteLine("\n=== Run ===\n");
        RunBrainfuck(code, BFIOFormat.DetaledNumeric);
    }

    public static void RunBrainfuck(string code, BFIOFormat format = BFIOFormat.ASCII)
    {
        const int tapeSize = 30000;
        byte[] tape = new byte[tapeSize];
        int ptr = 0;
        int pc = 0;
        Stack<int> loopStack = new Stack<int>();

        while (pc < code.Length)
        {
            char c = code[pc];
            switch (c)
            {
                case '>': ptr++; break;
                case '<': ptr--; break;
                case '+': tape[ptr]++; break;
                case '-': tape[ptr]--; break;
                case '.':
                    if (format == BFIOFormat.ASCII)
                        Console.Write((char)tape[ptr]);
                    else if (format == BFIOFormat.Numeric)
                        Console.WriteLine(tape[ptr]);
                    else // Detailed
                        Console.WriteLine($"Cell {ptr}: {tape[ptr]}");
                    break;
                case ',':
                    if (format == BFIOFormat.ASCII)
                    {
                        int read = Console.Read();
                        tape[ptr] = read == -1 ? (byte)0 : (byte)read;
                    }
                    else if (format == BFIOFormat.Numeric)
                    {
                        Console.Write("Enter number: ");
                        string? input = Console.ReadLine();
                        if (int.TryParse(input, out int val))
                            tape[ptr] = (byte)(val % 256);
                        else
                            tape[ptr] = 0;
                    }
                    else // Detailed
                    {
                        Console.Write($"Enter number for cell {ptr}: ");
                        string? input = Console.ReadLine();
                        if (int.TryParse(input, out int val))
                            tape[ptr] = (byte)(val % 256);
                        else
                            tape[ptr] = 0;
                    }
                    break;
                case '[':
                    if (tape[ptr] == 0)
                    {
                        int depth = 1;
                        while (depth > 0)
                        {
                            pc++;
                            if (pc >= code.Length)
                                throw new Exception("Unmatched '['");
                            if (code[pc] == '[') depth++;
                            if (code[pc] == ']') depth--;
                        }
                    }
                    else
                    {
                        loopStack.Push(pc);
                    }
                    break;
                case ']':
                    if (tape[ptr] != 0)
                    {
                        pc = loopStack.Peek();
                    }
                    else
                    {
                        loopStack.Pop();
                    }
                    break;
            }
            pc++;
        }
    }
}