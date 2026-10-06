namespace BFGo
{
    /// <summary>Base interface for all IR instructions.</summary>
    internal interface IRInst
    {
        /// <summary>Creates a deep copy of this instruction.</summary>
        IRInst Clone();

        /// <summary>Emits Brainfuck code for this instruction using the provided emitter.</summary>
        string Compile(BFGen host);
    }

    /// <summary>Marks the start of a loop.</summary>
    internal class LoopStart : IRInst
    {
        public string Compile(BFGen host) => host.BFPut('[');
        public IRInst Clone() => new LoopStart();
        public override bool Equals(object? obj) => obj is LoopStart;
        public override int GetHashCode() => typeof(LoopStart).GetHashCode();
        public override string ToString() => "[LoopStart]";
    }

    /// <summary>Marks the end of a loop.</summary>
    internal class LoopEnd : IRInst
    {
        public string Compile(BFGen host) => host.BFPut(']');
        public IRInst Clone() => new LoopEnd();
        public override bool Equals(object? obj) => obj is LoopEnd;
        public override int GetHashCode() => typeof(LoopEnd).GetHashCode();
        public override string ToString() => "[LoopEnd]";
    }

    /// <summary>Outputs the current cell's value as a character.</summary>
    internal class Print : IRInst
    {
        public IRInst Clone() => new Print();
        public string Compile(BFGen host) => host.BFPut('.');
        public override bool Equals(object? obj) => obj is Print;
        public override int GetHashCode() => typeof(Print).GetHashCode();
        public override string ToString() => "[Print]";
    }

    /// <summary>Reads a character from input into the current cell.</summary>
    internal class Read : IRInst
    {
        public IRInst Clone() => new Read();
        public string Compile(BFGen host) => host.BFPut(',');
        public override bool Equals(object? obj) => obj is Read;
        public override int GetHashCode() => typeof(Read).GetHashCode();
        public override string ToString() => "[Read]";
    }

    /// <summary>Adds a delta to the current cell (positive or negative).</summary>
    internal class Change(int val) : IRInst
    {
        internal int _val = val;
        public string Compile(BFGen host) => _val > 0 ? host.BFPut('+', _val) : host.BFPut('-', -_val);
        public IRInst Clone() => new Change(_val);
        public override bool Equals(object? obj) => obj is Change c && c._val == _val;
        public override int GetHashCode() => HashCode.Combine(typeof(Change), _val);
        public override string ToString() => $"[Change] (Value: {_val})";
    }

    /// <summary>
    /// Moves the head to a logical cell position.
    /// addr — position within the current frame.
    /// shift — transient offset, non-zero only inside Go's MoveStack.
    /// <summary>
    internal class MoveTo(int _addr, int _shift = 0) : IRInst
    {
        public readonly int addr = _addr, shift = _shift;
        public override bool Equals(object? obj) => obj is MoveTo m && m.addr == addr && m.shift == shift;
        public IRInst Clone() => new MoveTo(addr, shift);
        public override int GetHashCode() => HashCode.Combine(typeof(MoveTo), addr, shift);
        public string Compile(BFGen host) => host.BFMoveTo(addr + shift);
        public override string ToString() => $"[MoveTo] (Address: {addr}, Shift: {shift})";
    }

    /// <summary>
    /// /// Shifts the coordinate system by the given offset.
    /// Head moves with the frame; relative position is unchanged.
    /// </summary>
    internal class ShiftContext(int shift) : IRInst
    {
        public readonly int shift = shift;
        public override bool Equals(object? obj)=> obj is ShiftContext s && s.shift.Equals(shift);
        public IRInst Clone() => new ShiftContext(shift);
        public override int GetHashCode() => HashCode.Combine(typeof(ShiftContext), shift.GetHashCode());
        public string Compile(BFGen host) => host.BFShiftContext(shift);
        public override string ToString() => $"[ShiftContext] (Shift: {shift})";
    }
}