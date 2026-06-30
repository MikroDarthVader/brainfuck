using System.Text;
namespace BFGenerator
{
    public class BFBuilder
    {
        private readonly StringBuilder bf;
        private int PosInContext;

        public BFBuilder()
        {
            bf = new StringBuilder();
        }

        private void BFPut_(char bfInst)
        {
            if (bf.Length > 0 && (
                (bfInst == '-' && bf[bf.Length - 1] == '+') ||
                (bfInst == '+' && bf[bf.Length - 1] == '-') ||
                (bfInst == '<' && bf[bf.Length - 1] == '>') ||
                (bfInst == '>' && bf[bf.Length - 1] == '<')))
                bf.Remove(bf.Length - 1, 1);
            else
                bf.Append(bfInst);
        }

        public void BFMoveTo(int dest)
        {
            int move = dest - PosInContext;
            char command = move > 0 ? '>' : '<';
            int absMove = move >= 0 ? move : -move;
            for (int i = 0; i < absMove; i++)
                BFPut_(command);
            PosInContext = dest;
        }

        public void BFShiftContext(int shift)
        {
            char command = shift > 0 ? '>' : '<';
            int absShift = shift >= 0 ? shift : -shift;

            for (int i = 0; i < absShift; i++)
                BFPut_(command);
        }

        public void BFPut(char bfInst)
        {
            if (!"+-.,[]".Contains(bfInst))
                return;

            BFPut_(bfInst);
        }

        public override string ToString() => bf.ToString();
    }
}