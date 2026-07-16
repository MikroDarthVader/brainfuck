namespace BFGo
{
    internal class BFUIntType : BFType
    {
        public BFUIntType(int size = 0) : base(size) { }
    }

    internal class BFIntType : BFType
    {
        public BFUIntType Pos { get; }
        public BFUIntType Neg { get; }

        public BFIntType(int addrSize) : base()
        {
            Pos = RegisterField(new BFUIntType(addrSize));
            Neg = RegisterField(new BFUIntType(addrSize));
        }

        public void Normalize(BFVar desc)
        {
            var posDesc = Pos.From(desc);
            var negDesc = Neg.From(desc);
            for (int i = 0; i < Pos.Size; i++)
                posDesc[i].CompareTo(negDesc[i]);
        }
    }
}