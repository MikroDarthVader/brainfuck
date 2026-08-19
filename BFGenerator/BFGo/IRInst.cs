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
    internal class Change : IRInst
    {
        internal int val;
        public Change(int val) { this.val = val; }
        public string Compile(BFGen host) => val > 0 ? host.BFPut('+', val) : host.BFPut('-', -val);
        public IRInst Clone() => new Change(val);
        public override bool Equals(object? obj) => obj is Change c && c.val == val;
        public override int GetHashCode() => HashCode.Combine(typeof(Change), val);
        public override string ToString() => $"[Change] (Value: {val})";
    }

    /// <summary>
    /// Represents a context shift that can be either constant or lazy.
    /// Lazy shifts are used when the shift value depends on final context sizes.
    /// </summary>
    internal class ShiftDescriptor
    {
        public int StaticShift { get; }
        public Func<int>? LazyProvider { get; }
        public int LateBoundSign { get; }

        public ShiftDescriptor(int staticShift = 0)
        {
            StaticShift = staticShift;
            LazyProvider = null;
            LateBoundSign = 0;
        }

        public ShiftDescriptor(Func<int> lazyProvider)
        {
            LazyProvider = lazyProvider ?? throw new ArgumentNullException(nameof(lazyProvider));
            StaticShift = 0;
            LateBoundSign = lazyProvider() > 0 ? 1 : -1;
        }

        private ShiftDescriptor(int staticShift, Func<int>? lazyProvider, int lateBoundSign)
        {
            StaticShift = staticShift;
            LazyProvider = lazyProvider;
            LateBoundSign = lateBoundSign;
        }

        /// <summary>Evaluates and returns the shift value.</summary>
        public int Shift => LazyProvider != null ? LazyProvider() : StaticShift;

        public ShiftDescriptor Clone() => new ShiftDescriptor(StaticShift, LazyProvider, LateBoundSign);

        /// <summary>
        /// Compares two descriptors for semantic equality.
        /// Constants are compared by value; lazy descriptors are compared by sign.
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is not ShiftDescriptor s) return false;
            if (s.LateBoundSign != LateBoundSign) return false;
            return LateBoundSign != 0 || s.StaticShift == StaticShift;
        }

        public override int GetHashCode() => HashCode.Combine(typeof(ShiftDescriptor), LateBoundSign, StaticShift);

        public override string ToString()
        {
            return LateBoundSign switch
            {
                1 => "Late Bound Positive",
                -1 => "Late Bound Negative",
                _ => StaticShift == 0 ? "None" : StaticShift.ToString()
            };
        }
    }

    /// <summary>
    /// Moves the tape head to a specific cell within the current or foreign context.
    /// The target address is computed by the <see cref="relativePos"/> property.
    /// </summary>
    internal class MoveTo : IRInst
    {
        public readonly BFVar descriptor;
        public readonly int cellPos;

        public MoveTo(BFVar descriptor, int cellPos)
        {
            this.descriptor = new BFVar(descriptor);
            this.cellPos = cellPos;
        }

        /// <summary>
        /// Tape offset relative to the active context's origin,
        /// including any transitional shift.
        /// </summary>
        public int relativePos => descriptor.Context.Resolve(descriptor, cellPos) +
                                    (descriptor.isTransitional ? descriptor.cxtShift!.Shift : 0);

        public IRInst Clone() => new MoveTo(new BFVar(descriptor), cellPos);

        public override bool Equals(object? obj)
        {
            return obj is MoveTo m &&
                   m.cellPos == cellPos &&
                   m.descriptor.Equals(descriptor);
        }

        public override int GetHashCode() => HashCode.Combine(typeof(MoveTo), cellPos, descriptor);

        public string Compile(BFGen host) => host.BFMoveTo(relativePos);

        public override string ToString()
        {
            return $"[MoveTo] (VarPos: {descriptor.BaseIndex}, " +
                   $"CellPos: {cellPos}, " +
                   $"ActiveShift: {descriptor.cxtShift}, " +
                   $"RelativePos: {relativePos})";
        }
    }

    /// <summary>
    /// Shifts the tape head by a given offset, optionally switching to a new logical context.
    /// The shift may be lazy (see <see cref="ShiftDescriptor"/>).
    /// </summary>
    internal class ShiftContext : IRInst
    {
        public readonly BFContext? newContext;
        public readonly ShiftDescriptor shiftFromParentCxt;

        public ShiftContext(Func<int> shiftFromParentCxt, BFContext? newContext = null) : this(new ShiftDescriptor(shiftFromParentCxt), newContext) { }
        public ShiftContext(int shiftFromParentCxt, BFContext? newContext = null) : this(new ShiftDescriptor(shiftFromParentCxt), newContext) { }

        public ShiftContext(ShiftDescriptor shiftFromParentCxt, BFContext? newContext = null)
        {
            this.shiftFromParentCxt = shiftFromParentCxt;
            this.newContext = newContext;
        }

        public IRInst Clone() => new ShiftContext(shiftFromParentCxt.Clone(), newContext);

        public override bool Equals(object? obj)
        {
            return obj is ShiftContext s &&
                   s.shiftFromParentCxt.Equals(shiftFromParentCxt);
        }

        public override int GetHashCode() => HashCode.Combine(typeof(ShiftContext), shiftFromParentCxt.GetHashCode());

        public string Compile(BFGen host) => host.BFShiftContext(shiftFromParentCxt.Shift);

        public override string ToString()
        {
            var targetContextId = newContext != null ? newContext.ID.ToString() : "Current";

            return $"[ShiftContext] (Target: {targetContextId}, Shift: {shiftFromParentCxt})";
        }
    }
}