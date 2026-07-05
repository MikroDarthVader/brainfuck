namespace BFG
{
    public class BFUIntType : BFType
    {
        public BFUIntType(int size = 0) : base(size) { }
    }

    public class BFIntType : BFType
    {
        public BFUIntType Pos { get; }
        public BFUIntType Neg { get; }

        public BFIntType(int addrSize) : base()
        {
            Pos = RegisterField(new BFUIntType(addrSize));
            Neg = RegisterField(new BFUIntType(addrSize));
        }

        public void Normalize(BFRootDescriptor desc)
        {
            var posDesc = Pos.From(desc);
            var negDesc = Neg.From(desc);
            for (int i = 0; i < Pos.Size; i++)
                posDesc[i]!.CompareTo(negDesc[i]!);
        }
    }

    public class BFFloatType : BFType
    {
        public BFIntType Exponent { get; }
        public BFUIntType Mantissa { get; }

        public BFFloatType(int expoSize, int mantSize) : base(0)
        {
            Exponent = RegisterField(new BFIntType(expoSize));
            Mantissa = RegisterField(new BFUIntType(mantSize));
        }
    }
}