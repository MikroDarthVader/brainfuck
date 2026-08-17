using BFGo;
using BFTypeSmart;

class Program
{
    class Test : BFGProgram
    {
        public Test(int addrSize, int stackDens, int dataDens, int cellSize)
            : base(addrSize, stackDens, dataDens, cellSize) { }

        public override void Code()
        {
            using var tmp = AllocStack(6);
            BFaddressType bFaddressType6 = new BFaddressType(3);
            BFUIntType bFUIntType3 = new BFUIntType(3);

            tmp[0].Init(100);
            tmp[1].Init();
            tmp[2].Init();

            tmp[3].Init(10);
            tmp[4].Init(255);
            tmp[5].Init();

            bFaddressType6.shorten(tmp);
            bFUIntType3.Print256(bFaddressType6.Pos.From(tmp));
            CellFunc.StringPrint(tmp, "\n");
            bFUIntType3.Print256(bFaddressType6.Neg.From(tmp));
        }
    }

    static void Main()
    {
        var prg = new Test(addrSize: 1, stackDens: 1, dataDens: 1, cellSize: 256);
        //prg.Debug(BFIOFormat.DetaledNumeric);
        Compile(prg, BFIOFormat.ASCII);
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