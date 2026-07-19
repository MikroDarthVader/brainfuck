namespace BFGo
{
    public enum BFIOFormat
    {
        None,
        ASCII,
        Numeric,
        DetaledNumeric
    }

    internal class BFIRDebugger : BFIR
    {
        private readonly BFIOFormat IOFormat;
        private readonly BFG env;
        private readonly LoopValidator validator = new();

        private readonly Dictionary<int, int> memStatic = new();
        private readonly Dictionary<int, int> memDynamic = new();

        private int dynCxtPos = 0, cellPos = 0;

        public BFIRDebugger(BFG env, BFIOFormat IOFormat)
        {
            this.IOFormat = IOFormat;
            this.env = env;
        }

        internal int GetValue(int addr)
        {
            if (env.ActiveContext == env.staticCxt)
            {
                if (addr > env.staticCxt.MaxSize)
                    return memDynamic.GetValueOrDefault(addr - env.staticCxt.MaxSize);
                else
                    return memStatic.GetValueOrDefault(addr);
            }
            else
            {
                if (addr + dynCxtPos < 0)
                    return memStatic.GetValueOrDefault(env.staticCxt.MaxSize + addr);
                else
                    return memDynamic.GetValueOrDefault(addr + dynCxtPos);
            }
        }

        private void SetValue(int val, int addr)
        {
            if (env.ActiveContext == env.staticCxt)
            {
                if (addr > env.staticCxt.MaxSize)
                    memDynamic[addr - env.staticCxt.MaxSize] = val;
                else
                    memStatic[addr] = val;
            }
            else
            {
                if (addr + dynCxtPos < 0)
                    memStatic[env.staticCxt.MaxSize + addr] = val;
                else
                    memDynamic[addr + dynCxtPos] = val;
            }
        }

        private int GetCurrValue() => GetValue(cellPos);
        private void SetCurrValue(int val) => SetValue(val, cellPos);

        public void While(Action code)
        {
            validator.EnterLoop();
            int iteration = 0;

            while (GetCurrValue() > 0)
            {
                iteration++;
                if (iteration > 1)
                    validator.AdvanceIteration();
                
                code();
            }

            validator.ExitLoop(iteration);
        }

        public void Add(IRInst inst)
        {
            validator.ValidateInstruction(inst, currentIteration: 1);

            switch (inst)
            {
                case Change c:
                    {
                        var val = GetCurrValue();
                        val += c.val;
                        val %= env.cellSize;
                        if (val < 0)
                            val += env.cellSize;
                        SetCurrValue(val);
                    }
                    break;

                case MoveTo mv:
                    cellPos = mv.relativePos;
                    break;

                case ShiftContext sc:
                    if (sc.newContext != env.staticCxt && env.ActiveContext != env.staticCxt)
                        dynCxtPos += sc.shiftFromParentCxt();
                    break;

                case Print:
                    {
                        var val = GetCurrValue();
                        if (IOFormat == BFIOFormat.ASCII)
                            Console.Write((char)val);
                        else if (IOFormat == BFIOFormat.Numeric)
                            Console.WriteLine($"Print: {val}");
                        else
                            Console.WriteLine($"Print cell {cellPos}, context {env.ActiveContext.ID}: {val}");
                    }
                    break;

                case Read:
                    if (IOFormat == BFIOFormat.ASCII)
                        SetCurrValue(Console.Read());
                    else if (IOFormat == BFIOFormat.Numeric)
                    {
                        Console.Write("Read: ");
                        if (int.TryParse(Console.ReadLine(), out int val))
                            SetCurrValue(val);
                        else
                            SetCurrValue(0);
                    }
                    else
                    {
                        Console.Write($"Read cell {cellPos}, context {env.ActiveContext.ID}: ");
                        if (int.TryParse(Console.ReadLine(), out int val))
                            SetCurrValue(val);
                        else
                            SetCurrValue(0);
                    }
                    break;

                default:
                    break;
            }
        }
    }
    internal class LoopValidator
    {
        private readonly Stack<LoopValidationState> loopStates = new();

        private class LoopValidationState
        {
            public List<IRInst> ExpectedSnapshots { get; } = new();
            public int CurrentInstIndex { get; set; } = 0;
            public bool IsFirstIteration { get; set; } = true;
        }

        public void EnterLoop()
        {
            loopStates.Push(new LoopValidationState());
        }

        public void AdvanceIteration()
        {
            if (loopStates.Count == 0) return;

            var state = loopStates.Peek();
            state.CurrentInstIndex = 0;
            state.IsFirstIteration = false;
        }

        public void ExitLoop(int actualIteration)
        {
            if (loopStates.Count == 0) return;

            var state = loopStates.Pop();

            // Check if the loop generated fewer instructions on its final iteration
            if (!state.IsFirstIteration && state.CurrentInstIndex < state.ExpectedSnapshots.Count)
            {
                throw new InvalidOperationException(
                    $"Critical meta-generation mismatch! At iteration {actualIteration}, the loop execution " +
                    $"terminated prematurely. Expected {state.ExpectedSnapshots.Count} instructions, " +
                    $"but only received {state.CurrentInstIndex}. Your C# code inside the loop is not invariant!");
            }
        }

        public void ValidateInstruction(IRInst inst, int currentIteration)
        {
            if (loopStates.Count == 0) return;

            var state = loopStates.Peek();
            var currentSnapshot = inst.Clone();

            if (state.IsFirstIteration)
            {
                // Record the baseline on the very first iteration
                state.ExpectedSnapshots.Add(currentSnapshot);
            }
            else
            {
                int index = state.CurrentInstIndex;

                if (index >= state.ExpectedSnapshots.Count)
                {
                    throw new InvalidOperationException(
                        $"Critical meta-generation mismatch! At loop iteration {currentIteration}, " +
                        $"an unexpected EXTRA instruction of type '{inst.GetType().Name}' was generated. " +
                        $"Your C# code state fluctuates across different runtime loop iterations.");
                }

                var expectedSnapshot = state.ExpectedSnapshots[index];

                if (!currentSnapshot.Equals(expectedSnapshot))
                {
                    throw new InvalidOperationException(
                        $"Critical pipeline variance detected! At loop iteration {currentIteration}, " +
                        $"instruction payload at index {index} mutated.\n" +
                        $"Expected: {expectedSnapshot}\n" +
                        $"Received: {currentSnapshot}\n" +
                        $"Ensure that C# variables or allocations inside the loop do not depend on the runtime iteration count.");
                }

                state.CurrentInstIndex++;
            }
        }
    }
}
