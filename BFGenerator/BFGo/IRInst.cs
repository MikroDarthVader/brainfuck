namespace BFGo
{
    internal interface IRInst
    {
        public IRInst Clone();
        public string Compile(BFGen host);
    }

    internal class LoopStart : IRInst
    {
        public string Compile(BFGen host) => host.BFPut('[');
        public IRInst Clone() => new LoopStart();
        public override bool Equals(object? obj) => obj != null && obj is LoopStart;
        public override int GetHashCode() => typeof(LoopStart).GetHashCode();
        public override string ToString() => "[LoopStart]";
    }

    internal class LoopEnd : IRInst
    {
        public string Compile(BFGen host) => host.BFPut(']');
        public IRInst Clone() => new LoopEnd();
        public override bool Equals(object? obj) => obj != null && obj is LoopEnd;
        public override int GetHashCode() => typeof(LoopEnd).GetHashCode();
        public override string ToString() => "[LoopEnd]";
    }

    internal class Print : IRInst
    {
        public IRInst Clone() => new Print();
        public string Compile(BFGen host) => host.BFPut('.');
        public override bool Equals(object? obj) => obj != null && obj is Print;
        public override int GetHashCode() => typeof(Print).GetHashCode();
        public override string ToString() => "[Print]";
    }
    internal class Read : IRInst
    {
        public IRInst Clone() => new Read();
        public string Compile(BFGen host) => host.BFPut(',');
        public override bool Equals(object? obj) => obj != null && obj is Read;
        public override int GetHashCode() => typeof(Read).GetHashCode();
        public override string ToString() => "[Read]";
    }

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

        public int Invoke() => LazyProvider != null ? LazyProvider() : StaticShift;

        public ShiftDescriptor Clone() => new ShiftDescriptor(StaticShift, LazyProvider, LateBoundSign);

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

    internal class MoveTo : IRInst
    {
        public readonly BFVar descriptor;
        public readonly int cellPos;

        public MoveTo(BFVar descriptor, int cellPos)
        {
            this.descriptor = new BFVar(descriptor);
            this.cellPos = cellPos;
        }

        public int relativePos => descriptor.Context.Resolve(descriptor, cellPos) +
                                    (descriptor.isTransitional ? descriptor.cxtShift!.Invoke() : 0);

        public IRInst Clone() => new MoveTo(descriptor, cellPos);

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
                   Equals(s.newContext?.ID, newContext?.ID) &&
                   s.shiftFromParentCxt.Equals(shiftFromParentCxt);
        }

        public override int GetHashCode() => HashCode.Combine(typeof(ShiftContext), newContext?.ID, shiftFromParentCxt.GetHashCode());

        public string Compile(BFGen host) => host.BFShiftContext(shiftFromParentCxt.Invoke());

        public override string ToString()
        {
            var targetContextId = newContext != null ? newContext.ID.ToString() : "Current";

            return $"[ShiftContext] (Target: {targetContextId}, Shift: {shiftFromParentCxt})";
        }
    }
}
