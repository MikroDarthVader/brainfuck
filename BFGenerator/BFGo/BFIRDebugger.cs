namespace BFGo
{
    internal class BFIRDebugger(BFContext staticCxt)
    {
        private readonly BFContext staticCxt = staticCxt;
        private List<int> staticMem = new(), dynMem = new();
        private int posInContext, shift;

        public void ProcessInst(IRInst inst)
        {

        }
    }
}
