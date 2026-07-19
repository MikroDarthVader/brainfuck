namespace BFGo
{
    internal interface BFIR
    {
        public abstract void Add(IRInst inst);
        public abstract void While(Action code);
    }

    internal class BFIRGen : BFIR
    {
        private List<IRInst> instructions = new List<IRInst>();

        public string Compile()
        {
            BFGen bFBuilder = new BFGen();
            foreach (var inst in instructions)
                inst.Compile(bFBuilder);
            return bFBuilder.ToString();
        }

        public void Add(IRInst inst) => instructions.Add(inst);

        public void While(Action code)
        {
            Add(new LoopStart());
            code();
            Add(new LoopEnd());
        }

        private class LoopStart : IRInst 
        { 
            public string Compile(BFGen host) => host.BFPut('[');
            public IRInst Clone() => new LoopStart();
        }
        private class LoopEnd : IRInst 
        { 
            public string Compile(BFGen host) => host.BFPut(']');
            public IRInst Clone() => new LoopEnd();
        }
    }

    internal interface IRInst
    {
        public IRInst Clone();
        public string Compile(BFGen host);
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

    internal class MoveTo : IRInst
    {
        public readonly BFVar descriptor;
        public readonly int cellPos;
        public readonly Func<int>? shiftFromParentCxt;

        public MoveTo(BFVar descriptor, int cellPos, Func<int>? shiftFromParentCxt)
        {
            var activeContext = descriptor.Context.env.ActiveContext;
            if (descriptor.Context != activeContext && shiftFromParentCxt == null)
                throw new InvalidOperationException("Cannot generate MoveTo for a foreign descriptor without a context shift function.");

            this.descriptor = new BFVar(descriptor);
            this.cellPos = cellPos;
            this.shiftFromParentCxt = shiftFromParentCxt;
        }

        public int relativePos
        {
            get
            {
                var localPos = descriptor.Context.Resolve(descriptor, cellPos);
                var shift = 0;
                if (shiftFromParentCxt != null)
                    shift = shiftFromParentCxt();
                return localPos + shift;
            }
        }
        public IRInst Clone() => new MoveTo(descriptor, cellPos, shiftFromParentCxt);

        public override bool Equals(object? obj)
        {
            return obj is MoveTo m &&
                   m.cellPos == cellPos &&
                   m.descriptor.Equals(descriptor) &&
                   (m.shiftFromParentCxt == null && shiftFromParentCxt == null ||
                    m.shiftFromParentCxt != null && shiftFromParentCxt != null &&
                    m.shiftFromParentCxt() == shiftFromParentCxt());
        }

        public override int GetHashCode() => HashCode.Combine(typeof(MoveTo), cellPos, descriptor, shiftFromParentCxt == null ? 0 : shiftFromParentCxt());

        public string Compile(BFGen host) => host.BFMoveTo(relativePos);

        public override string ToString()
        {
            var shiftVal = shiftFromParentCxt != null ? shiftFromParentCxt().ToString() : "None";
            return $"[MoveTo] (VarPos: {descriptor.BaseIndex}, CellPos: {cellPos}, ActiveShift: {shiftVal}, TotalRelativePos: {relativePos})";
        }
    }

    internal class ShiftContext : IRInst
    {
        public readonly Func<int> shiftFromParentCxt;
        public readonly BFContext? newContext;

        public ShiftContext(int shift, BFContext? newContext = null)
        {
            shiftFromParentCxt = () => shift;
            this.newContext = newContext;
        }

        public ShiftContext(Func<int> shift, BFContext? newContext = null)
        {
            shiftFromParentCxt = shift;
            this.newContext = newContext;
        }
        public IRInst Clone() => new ShiftContext(shiftFromParentCxt, newContext);

        public override bool Equals(object? obj)
        {
            return obj is ShiftContext s &&
                   s.newContext == newContext &&
                   (s.shiftFromParentCxt == null && shiftFromParentCxt == null ||
                    s.shiftFromParentCxt != null && shiftFromParentCxt != null &&
                    s.shiftFromParentCxt() == shiftFromParentCxt());
        }

        public override int GetHashCode() => HashCode.Combine(typeof(ShiftContext), newContext?.GetHashCode(), shiftFromParentCxt == null ? 0 : shiftFromParentCxt());

        public string Compile(BFGen host) => host.BFShiftContext(shiftFromParentCxt());

        public override string ToString()
        {
            var targetContextId = newContext != null ? newContext.ID.ToString() : "Null (Relative Shift)";
            return $"[ShiftContext] (Target Context ID: {targetContextId}, ShiftDelta: {shiftFromParentCxt()})";
        }
    }
}
