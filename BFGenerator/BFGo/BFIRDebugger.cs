namespace BFGo
{
    public enum BFIOFormat
    {
        ASCII,
        Numeric,
        DetaledNumeric
    }

    internal class BFIRDebugger : BFIR
    {
        private readonly BFIOFormat IOFormat;
        private readonly BFIRGen compiled;

        private readonly Dictionary<int, int> mem = new();

        public override bool Debuggable => true;

        private int cxtPos = 0, cellPos = 0;
        private int codeCursor = 0;

        public BFIRDebugger(BFIRGen compiled, BFIOFormat IOFormat) : base(compiled.cfg)
        {
            this.IOFormat = IOFormat;
            this.compiled = compiled;
        }

        internal int GetValue(int addr) => mem.GetValueOrDefault(addr + cxtPos);
        private void SetValue(int val, int addr) => mem[addr + cxtPos] = val;
        private int GetCurrValue() => GetValue(cellPos);
        private void SetCurrValue(int val) => SetValue(val, cellPos);

        public override void While(Action code)
        {
            codeCursor++; //skip loopStart IRInst
            int startAddr = codeCursor;

            while (GetCurrValue() > 0)
            {
                codeCursor = startAddr;
                code();
            }

            for (int depth = 1; depth > 0; codeCursor++) //skip loop and LoopEnd
            {
                if (compiled[codeCursor] is LoopStart)
                    depth++;
                else if (compiled[codeCursor] is LoopEnd)
                    depth--;
            }
        }

        protected override void _Add(IRInst inst)
        {
            if (codeCursor >= compiled.Count)
            {
                throw new InvalidOperationException(
                    $"[BFG Pipeline Mismatch] Live run emitted an EXTRA instruction at step index {codeCursor}.\n" +
                    $"Live instruction received: {inst}\n" +
                    $"Expected: End of compiled bytecode sequence.\n\n" +
                    $"Cause: Your high-level C# loop logic or contextual allocations generated " +
                    $"more commands on this runtime iteration than during the shadow compile pass.");
            }

            var expectedInst = compiled[codeCursor];

            if (!expectedInst.Equals(inst))
            {
                // Извлекаем детальную информацию о значениях прыжков и изменений
                string expectedDetails = GetInstDebugDetails(expectedInst);
                string actualDetails = GetInstDebugDetails(inst);

                throw new InvalidOperationException(
                    $"[BFG Pipeline Mismatch] Structural invariance broken at baseline index {codeCursor}!\n\n" +
                    $"EXPECTED (Shadow Pass):\n" +
                    $"  Type: {expectedInst.GetType().Name}\n" +
                    $"  Data: {expectedDetails}\n\n" +
                    $"RECEIVED (Live Run):\n" +
                    $"  Type: {inst.GetType().Name}\n" +
                    $"  Data: {actualDetails}\n\n" +
                    $"Context Tracking State:\n" +
                    $"  Current Window Base (cxtPos): {cxtPos}\n" +
                    $"  Current Virtual Head (cellPos): {cellPos}\n" +
                    $"  Calculated Memory Address: {cellPos + cxtPos}\n\n" +
                    $"Error Check: Ensure that local C# variables, counters, or multi-pass allocations " +
                    $"inside your While loop do not fluctuate or depend on the runtime loop iteration count.");
            }

            switch (inst)
            {
                case Change c:
                    {
                        var val = GetCurrValue();
                        val += c.val;
                        val %= cfg.cellSize;
                        if (val < 0)
                            val += cfg.cellSize;
                        SetCurrValue(val);
                    }
                    break;

                case MoveTo mv:
                    cellPos = ((MoveTo)expectedInst).relativePos;
                    break;

                case ShiftContext sc:
                    cxtPos += ((ShiftContext)expectedInst).shiftFromParentCxt.Invoke();
                    break;

                case Print:
                    {
                        var val = GetCurrValue();
                        if (IOFormat == BFIOFormat.ASCII)
                            Console.Write((char)val);
                        else if (IOFormat == BFIOFormat.Numeric)
                            Console.WriteLine($"Print: {val}");
                        else
                            Console.WriteLine($"Print cell {cellPos}, context {ActiveContext.ID}: {val}");
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
                        Console.Write($"Read cell {cellPos}, context {ActiveContext.ID}: ");
                        if (int.TryParse(Console.ReadLine(), out int val))
                            SetCurrValue(val);
                        else
                            SetCurrValue(0);
                    }
                    break;

                default:
                    break;
            }

            codeCursor++;
        }

        private string GetInstDebugDetails(IRInst inst)
        {
            return inst switch
            {
                Change c => $"Value modification: {c.val}",
                MoveTo mv => $"Local context allocation offset (relativePos): {mv.relativePos}, Defined target block variable: {mv.descriptor}",
                ShiftContext sc => $"Global grid system shift delta (shiftFromParentCxt): {sc.shiftFromParentCxt.Invoke()}",
                _ => inst.ToString() ?? inst.GetType().Name
            };
        }
    }
}
