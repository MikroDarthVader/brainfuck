using BFGo;

class Program
{
    class TestProgram : BFGProgram
    {
        public TestProgram(int addrSize, int stackDens, int dataDens, int cellSize) : base(addrSize, stackDens, dataDens, cellSize) {}

        public override void Code(BFG env)
        {
            /*var cell = env.Alloc(AllocatorKind.Data)[0];
            cell.Init(3).While(() =>
            {
                using var tmp = env.Alloc(AllocatorKind.Data);
                tmp[0].Plus().Print();
                cell.Minus().Print();
                env.Break();
            });*/

            var tmp = env.Alloc(AllocatorKind.Data, 10);
            tmp[7].Init(42).Print();
            tmp[2].Print();
            env.Go(env.Alloc(AllocatorKind.Data)[0].Init(3).owner, tmp);
            env.Break();
            tmp[7].Print();
            tmp[2].Print();
        }
    }

    static void Main()
    {
        var prg = new TestProgram(addrSize: 1, stackDens: 1, dataDens: 1, cellSize: 256);
        prg.Debug(BFIOFormat.DetaledNumeric);

        Compile(prg);
    }

    static void Compile(BFGProgram prg)
    {
        string code = prg.Compile();
        Console.WriteLine("=== Generated Brainfuck ===\n");
        Console.WriteLine(code);
        Console.WriteLine("\n=== Run ===\n");
        RunBrainfuck(code, BFIOFormat.DetaledNumeric);
    }

    static void RunBrainfuck(string code, BFIOFormat format = BFIOFormat.ASCII)
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