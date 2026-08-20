using BFGo;
using BFTypeSmart;

class Program
{
    class Test() : BFGProgram(addrSize: 1, stackDens: 1, dataDens: 1, cellSize: 256)
    {
        public override void Code()
        {
            using var addr = Alloc();
            using var tmp = Alloc();

            using var running = Alloc();
            running[0].Init(1);

            running[0].While(() =>
            {


                addr[0].Plus();

                GoFromStatic(addr: addr);
                Break();
                var b = GetData();
                b[0].Read();
                GoStatic();



                addr.CopyTo(tmp);
                tmp[0].While(() =>
                {
                    Break();
                    GoFromStatic(addr: tmp);
                    GetData()[0].Print();
                    GoStatic();
                    tmp[0].Minus();
                });


            });

        }
    }

    static void Main()
    {
        var prg = new Test();
        prg.Debug(BFIOFormat.Numeric);
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
        Stack<int> loopStack = new();

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