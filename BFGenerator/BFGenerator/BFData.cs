namespace BFGenerator
{
    public class BFData
    {
        public virtual int size { get; protected set; }
        public int addr;

        BFData(BFMemoryDescriptor descriptor, BFIR ir)
        {
            this.space = space;
            addr = space.Alloc(size);
        }

        public void Free() { space.Free(addr, size); }

        public void MoveTo(params BFData[] to)
        {

        }

        public void CopyTo(params BFData[] to)
        {
            
        }


    }
}
