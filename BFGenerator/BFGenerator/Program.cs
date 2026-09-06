using BFGo;
using BFTypeSmart;

class Program
{
    class Test1 : BFGProgram
    {
        public Test1()
            : base(addrSize : 1, stackDens : 1, dataDens: 1, cellSize: 256) { }
        public override void Code()
        {
            var tmp = Alloc();
            tmp[0].Init(5);

            GoFromStatic(tmp);
            GetData()[0].Init(42);
            GoStatic();

            var input = Alloc();
            var ctr = Alloc();
            ctr[0].Init(1);
            input[0].Read();
            GoFromStatic(ctr, move: [ctr, input]);
            input[0].While(() =>
            {
                ctr[0].Plus();
                input[0].Minus();

                GetData()[0].Print();

                Go(ctr, move: [ctr, input]);
            });
        }
    }

    /// <summary>
    /// Многоразрядное беззнаковое целое (little-endian) с внутренними временными ячейками.
    /// Полностью очищено от IfElse и использует безопасную механику Not/If.
    /// </summary>
    internal class BFUIntType : BFType
    {
        public BFType Value { get; private set; }
        public BFType Temp { get; private set; }
        public BFType CarryFlag { get; private set; }

        public BFUIntType(int size) : base()
        {
            Value = RegisterField(size);
            Temp = RegisterField(1);
            CarryFlag = RegisterField(1);
        }

        public void Inc(BFVar root)
        {
            var value = Value.From(root);
            var temp = Temp.From(root);
            var carry = CarryFlag.From(root);

            carry[0].Init(1);
            for (int i = 0; i < Value.Size; i++)
            {
                carry[0].CopyTo(temp[0]);
                temp[0].If(() =>
                {
                    value[i].Plus(1);
                    value[i].CopyTo(temp[0]);
                    temp[0].If(() =>
                    {
                        // value[i] != 0, сбрасываем перенос
                        carry[0].Init(0);
                    });
                    // если value[i] == 0, carry остаётся 1
                });
            }
            carry[0].Init(0);
        }

        public void Dec(BFVar root)
        {
            var value = Value.From(root);
            var temp = Temp.From(root);
            var borrow = CarryFlag.From(root);

            borrow[0].Init(1);
            for (int i = 0; i < Value.Size; i++)
            {
                borrow[0].CopyTo(temp[0]);
                temp[0].If(() =>
                {
                    // Сохраняем старое значение в temp
                    value[i].CopyTo(temp[0]);
                    // Декремент
                    value[i].Minus(1);

                    // Проверяем, было ли старое значение больше 0 (temp != 0)
                    temp[0].If(() =>
                    {
                        // temp != 0, значит заимствования нет, сбрасываем borrow
                        borrow[0].Init(0);
                    });

                    // Проверяем, было ли старое значение равно 0 (temp == 0)
                    // Используем ваш новый безопасный метод Not() прямо на месте!
                    temp[0].Not();
                    temp[0].If(() =>
                    {
                        // temp был равен 0 (старое значение == 0), заимствование остается
                        // Здесь ничего делать не нужно, borrow изначально равен 1, 
                        // но при необходимости можно явно зафиксировать логику.
                    });
                });
            }
            borrow[0].Init(0);
        }

        /// <summary>Проверка на ноль. Результат в CarryFlag: 1 — ноль, 0 — не ноль.</summary>
        public void IsZero(BFVar root)
        {
            var value = Value.From(root);
            var temp = Temp.From(root);
            var carry = CarryFlag.From(root);

            carry[0].Init(1); // предполагаем, что ноль
            for (int i = 0; i < Value.Size; i++)
            {
                value[i].CopyTo(temp[0]);
                temp[0].If(() =>
                {
                    carry[0].Init(0);
                });
            }
        }

        /// <summary>Установка маленького значения (0..255).</summary>
        public void SetSmall(BFVar root, int val)
        {
            var value = Value.From(root);
            for (int i = 0; i < Value.Size; i++)
                value[i].Init(0);
            value[0].Init(val);
        }

        /// <summary>Копирование значения из одного корневого дескриптора в другой.</summary>
        public void Copy(BFVar fromRoot, BFVar toRoot)
        {
            var fromVal = Value.From(fromRoot);
            var toVal = Value.From(toRoot);
            fromVal.CopyTo(toVal);
        }
    }
    class BinaryCounter : BFGProgram
    {
        public BinaryCounter(int addrSize, int stackDens, int dataDens, int cellSize)
            : base(addrSize, stackDens, dataDens, cellSize)
        {
            if (cellSize < 49)
                throw new ArgumentException("cellSize must be >= 49 for ASCII output (48='0', 49='1')");
        }

        public override void Code()
        {
            var uintType = new BFUIntType(cfg.addrSize);

            // =====================================================================
            // ЖЕСТКАЯ ИНВАРИАНТНАЯ РАСКЛАДКА ПАМЯТИ (Все Alloc делаются строго здесь)
            // =====================================================================
            var len = Alloc(uintType.Size);
            var idx = Alloc(uintType.Size);
            var remaining = Alloc(uintType.Size);

            var carry = Alloc(1);
            var running = Alloc(1);
            var tempChar = Alloc(1);
            var printMode = Alloc(1);

            var hasBitsLeft = Alloc(1);
            var charToPrint = Alloc(1);

            // Вспомогательные флаги для безопасной арифметики
            var isOneBit = Alloc(1);
            var isZeroBit = Alloc(1);
            var needNewBit = Alloc(1); // <-- Теперь живет в статической раскладке!

            // Инициализация стартового состояния в статике
            uintType.SetSmall(len, 0);
            running.Init(1);

            // Единый мигрирующий контекст переменных
            BFVar[] context = [len, idx, remaining, carry, running, tempChar, printMode, hasBitsLeft, charToPrint, isOneBit, isZeroBit, needNewBit];

            // ---- ЕДИНСТВЕННЫЙ ВЫХОД ИЗ СТАТИКИ ----
            var startAddr = Alloc(uintType.Size);
            uintType.SetSmall(startAddr, 1);
            GoFromStatic(startAddr, move: context);

            running.While(() =>
            {
                // ---- СТАДИЯ 1: ИНКРЕМЕНТ (Слева направо) ----
                carry.Init(1);
                uintType.SetSmall(idx, 2);
                uintType.Copy(len, remaining);

                carry.While(() =>
                {
                    Go(idx, move: context);

                    hasBitsLeft.Init(1);
                    isOneBit.Init(0);
                    isZeroBit.Init(0);
                    needNewBit.Init(1); // <-- Сбрасываем флаг на каждой итерации

                    uintType.IsZero(remaining);
                    var noMoreBits = uintType.CarryFlag.From(remaining);

                    noMoreBits.If(() => { hasBitsLeft.Init(0); });
                    hasBitsLeft.If(() => { needNewBit.Init(0); });

                    // ВЕТКА А: Число кончилось
                    needNewBit.If(() =>
                    {
                        GetData().Init(1);
                        uintType.Inc(len);
                        carry.Init(0);
                    });

                    // ВЕТКА Б: Мы внутри существующих разрядов
                    hasBitsLeft.If(() =>
                    {
                        GetData().CopyTo(tempChar);

                        tempChar.If(() => { isOneBit.Init(1); });
                        tempChar.Not();
                        tempChar.If(() => { isZeroBit.Init(1); });

                        isOneBit.If(() =>
                        {
                            GetData().Init(0);
                            uintType.Inc(idx);
                            uintType.Dec(remaining);
                        });

                        isZeroBit.If(() =>
                        {
                            GetData().Init(1);
                            carry.Init(0);
                        });
                    });

                    carry.If(() =>
                    {
                        Go(idx, move: context);
                    });
                });

                // Сброс на базу перед вычислением адресов вывода
                uintType.SetSmall(idx, 1);
                Go(idx, move: context);

                // ---- СТАДИЯ 2: ВЫВОД НА ЭКРАН (MSB-First, справа налево) ----
                uintType.Copy(len, remaining);

                printMode.Init(1);
                uintType.IsZero(len);
                var isZeroLength = uintType.CarryFlag.From(len);
                isZeroLength.If(() => { printMode.Init(0); });

                uintType.Copy(len, idx);
                uintType.Dec(idx);
                uintType.Inc(idx);
                uintType.Inc(idx);

                printMode.If(() =>
                {
                    Go(idx, move: context);
                });

                printMode.While(() =>
                {
                    GetData().CopyTo(tempChar);

                    charToPrint.Init(0);
                    tempChar.If(() => { charToPrint.Init(49); });
                    tempChar.Not();
                    tempChar.If(() => { charToPrint.Init(48); });
                    charToPrint.Print();

                    uintType.Dec(remaining);

                    uintType.IsZero(remaining);
                    var noMoreBitsToPrint = uintType.CarryFlag.From(remaining);

                    noMoreBitsToPrint.If(() => { printMode.Init(0); });

                    uintType.Copy(remaining, idx);
                    uintType.Dec(idx);
                    uintType.Inc(idx);
                    uintType.Inc(idx);

                    printMode.If(() =>
                    {
                        Go(idx, move: context);
                    });
                });

                // ---- СТАДИЯ 3: ПЕРЕВОД СТРОКИ И СБРОС НА БАЗУ ----
                uintType.SetSmall(idx, 1);
                Go(idx, move: context);

                tempChar.Init(10);
                tempChar.Print();
            });
        }


    }



    static void Main()
    {
        var prg = new BinaryCounter(addrSize: 2, stackDens: 1, dataDens: 1, cellSize: 256);
        prg.Debug(BFIOFormat.ASCII);
        //Compile(prg, BFIOFormat.ASCII);
    }

    static void Compile(BFGProgram prg, BFIOFormat IOFormat)
    {
        string code = prg.Compile();
        Console.WriteLine("=== Generated Brainfuck ===\n");
        Console.WriteLine(code);
        //Console.WriteLine("\n=== Run ===\n");
        //RunBrainfuck(code, IOFormat);
    }

    static void RunBrainfuck(string code, BFIOFormat format = BFIOFormat.ASCII)
    {
        const int tapeSize = 30000;
        byte[] tape = new byte[tapeSize];
        int ptr = 0;
        int pc = 0;
        Stack<int> loopStack = new Stack<int>();

        string Out = "";

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