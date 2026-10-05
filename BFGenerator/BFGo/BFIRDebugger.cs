using System;
using System.Collections.Generic;

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
        private readonly List<IRInst> compiled;
        private readonly BFIOFormat IOFormat;

        public override bool Debuggable => true;

        // Single logical address space: static lives in negative positions,
        // stack/data in non-negative. dynCxtPos is the current frame origin.
        private readonly Dictionary<int, int> mem = [];
        private int dynCxtPos;
        private int cellPos;
        private int codeCursor;

        public BFIRDebugger(BFGProgram env, List<IRInst> compiled, BFIOFormat format) : base(env)
        {
            this.compiled = compiled;
            IOFormat = format;
        }

        private int Abs => dynCxtPos + cellPos;
        private int GetCurr() => mem.GetValueOrDefault(Abs);
        private void SetCurr(int val) => mem[Abs] = val;

        /// <summary>Reads current values of all cells in <paramref name="var"/>.</summary>
        internal int[] GetVarValues(BFVar var)
        {
            var vals = new int[var.Size];
            for (int i = 0; i < var.Size; i++)
                vals[i] = mem.GetValueOrDefault(dynCxtPos + var.Resolve(i));
            return vals;
        }

        public override void While(Action code)
        {
            codeCursor++; // skip LoopStart
            int start = codeCursor;

            while (GetCurr() > 0)
            {
                codeCursor = start;
                code();
            }

            for (int depth = 1; depth > 0; codeCursor++) // skip loop body and LoopEnd
            {
                if (compiled[codeCursor] is LoopStart) depth++;
                else if (compiled[codeCursor] is LoopEnd) depth--;
            }
        }

        internal override void Add(IRInst inst)
        {
            if (codeCursor >= compiled.Count)
                throw new InvalidOperationException(
                    $"[BFG Pipeline Mismatch] Extra instruction at index {codeCursor}.\n" +
                    $"  Received: {inst}\n" +
                    $"  Compiled IR ended prematurely. " +
                    $"Loop logic or contextual allocations differ between passes.");

            var expected = compiled[codeCursor];

            if (!expected.Equals(inst))
                throw new InvalidOperationException(
                    $"[BFG Pipeline Mismatch] at index {codeCursor}.\n" +
                    $"  Expected: {expected}\n" +
                    $"  Actual:   {inst}\n" +
                    $"  dynCxtPos={dynCxtPos}, cellPos={cellPos}");

            switch (inst)
            {
                case Change c:
                    {
                        int val = GetCurr() + c._val;
                        val %= env.cfg.cellSize;
                        if (val < 0) val += env.cfg.cellSize;
                        SetCurr(val);
                    }
                    break;

                case MoveTo mv:
                    cellPos = mv.addr + mv.shift;
                    break;

                case ShiftContext sc:
                    dynCxtPos += sc.shift;
                    break;

                case Print:
                    {
                        int val = GetCurr();
                        if (IOFormat == BFIOFormat.ASCII)
                            Console.Write((char)val);
                        else if (IOFormat == BFIOFormat.Numeric)
                            Console.WriteLine($"Print: {val}");
                        else
                            Console.WriteLine($"Print cell {cellPos}, frame {dynCxtPos}: {val}");
                    }
                    break;

                case Read:
                    if (IOFormat == BFIOFormat.ASCII)
                        SetCurr(Console.Read());
                    else
                    {
                        Console.Write(IOFormat == BFIOFormat.Numeric
                            ? "Read: "
                            : $"Read cell {cellPos}, frame {dynCxtPos}: ");

                        if (int.TryParse(Console.ReadLine(), out int val))
                            SetCurr(val);
                        else
                            SetCurr(0);
                    }
                    break;
            }

            codeCursor++;
        }
    }
}