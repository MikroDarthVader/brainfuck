using BFGo;

class Program
{
    class Test : BFGProgram
    {
        public Test(int addrSize, int stackDens, int dataDens, int cellSize)
            : base(addrSize, stackDens, dataDens, cellSize) { }

        public override void Code(BFG env)
        {
            var tmp = env.Alloc(AllocatorKind.Data);
            tmp[0].Init(5);

            env.Go(tmp);
            env.Alloc(AllocatorKind.Data)[0].Init(42);
            env.Go(null);

            var input = env.Alloc(AllocatorKind.Data);
            var ctr = env.Alloc(AllocatorKind.Stack);
            ctr[0].Init(1);
            input[0].Read();
            env.Go(ctr, ctr, input); // Initial static-to-dynamic bootstrap transition produces a distinct code layout; handled outside the loop.
            input[0].While(() =>
            {
                ctr[0].Plus();
                input[0].Minus();

                env.Alloc(AllocatorKind.Data)[0].Print();

                env.Go(ctr, ctr, input);
            });
        }
    }

    static void Main()
    {
        var prg = new Test(addrSize: 1, stackDens: 1, dataDens: 1, cellSize: 256);
        prg.Debug(BFIOFormat.DetaledNumeric);
        //Compile(prg, BFIOFormat.ASCII);
    }

    static void Compile(BFGProgram prg, BFIOFormat IOFormat)
    {
        string code = prg.Compile();
        Console.WriteLine("=== Generated Brainfuck ===\n");
        Console.WriteLine(code);
        Console.WriteLine("\n=== Run ===\n");
        RunBrainfuck(code, IOFormat);
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