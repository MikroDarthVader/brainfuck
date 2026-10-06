using BFGo;

namespace tests
{
    class Program
    {
        static void Main()
        {
            var prg = new BinaryCounter();
            //prg.Debug(BFIOFormat.ASCII);
            var code = Compile(prg);
            //RunBrainfuck(code, BFIOFormat.ASCII);
            Console.ReadKey();
        }

        static string Compile(BFGProgram prg)
        {
            string code = prg.Compile();
            Console.WriteLine("=== Generated Brainfuck ===\n");
            Console.WriteLine(code);
            return code;
        }

        static void RunBrainfuck(string code, BFIOFormat format = BFIOFormat.ASCII)
        {
            const int tapeSize = 30000;
            byte[] tape = new byte[tapeSize];
            int ptr = 0;
            int pc = 0;
            Stack<int> loopStack = new Stack<int>();

            Console.WriteLine("\n=== Run ===\n");

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
}