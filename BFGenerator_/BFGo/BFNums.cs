namespace BFGo
{
    internal class BFAddrType : BFType
    {
        public BFAddrType(int size = 0) : base(size) { }
    }

    internal class BFAddrComparerType : BFType
    {
        public BFAddrType Pos { get; }
        public BFAddrType Neg { get; }

        public BFAddrComparerType(int addrSize) : base()
        {
            Pos = RegisterField(new BFAddrType(addrSize));
            Neg = RegisterField(new BFAddrType(addrSize));
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