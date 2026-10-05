using BFGo;
using BFTypeSmart;

class Program
{
    class BinaryCounter : BFGProgram
    {
        public BinaryCounter() : base(addrSize: 1, stackDens: 1, dataDens: 1, cellSize: 256) { }

        public override void Code()
        {
            using var frame = CreateScope();

            var run = frame.Alloc();  // 1 = continue
            var len = frame.Alloc();  // number of bits
            var idx = frame.Alloc();  // current bit index (1-based)
            var remaining = frame.Alloc();  // bits left to process
            var carry = frame.Alloc();  // increment carry
            var bit = frame.Alloc();  // scratch: current bit value
            var tmp = frame.Alloc();  // scratch
            var tmp2 = frame.Alloc();  // scratch

            len.Set(1);
            run.Set(1);

            // Initial value = 1: bit 0 lives in context 1, data[0] = 1.
            idx.Set(1);
            Go(idx);
            GetData().Set(1);

            run.While(() =>
            {
                // ---------- PRINT ----------
                // Print bits from most significant (idx = len) down to 1.
                len.CopyTo(idx);
                len.CopyTo(remaining);

                remaining.While(() =>
                {
                    Go(idx);

                    GetData().CopyTo(bit);
                    tmp.Set(48);                 // '0'
                    bit.CopyTo(tmp2);
                    tmp2.If(() => tmp.Set(49));  // '1'
                    tmp.Print();

                    remaining.Change(-1);
                    idx.Change(-1);
                });

                tmp.Set(10);                     // '\n'
                tmp.Print();

                // ---------- INCREMENT ----------
                idx.Set(1);
                carry.Set(1);
                len.CopyTo(remaining);

                carry.While(() =>
                {
                    Go(idx);

                    // If remaining == 0: append new bit.
                    remaining.CopyTo(tmp);
                    tmp.Not();
                    tmp.If(() =>
                    {
                        GetData().Set(1);
                        len.Change(1);
                        carry.Set(0);
                    });

                    // If remaining > 0: regular increment of this bit.
                    remaining.CopyTo(tmp2);
                    tmp2.If(() =>
                    {
                        GetData().CopyTo(bit);

                        // bit == 1 → clear, carry stays 1
                        bit.CopyTo(tmp);
                        tmp.If(() => GetData().Set(0));

                        // bit == 0 → set, carry becomes 0
                        bit.Not();
                        bit.If(() =>
                        {
                            GetData().Set(1);
                            carry.Set(0);
                        });

                        remaining.Change(-1);
                        idx.Change(1);
                    });
                });
            });
        }
    }

    static void Main()
    {
        var prg = new BinaryCounter();
        //prg.Debug(BFIOFormat.ASCII);
        RunBrainfuck(Compile(prg), BFIOFormat.ASCII);
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