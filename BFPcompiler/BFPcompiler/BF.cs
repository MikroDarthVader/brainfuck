using System.Text;

namespace BFPcompiler
{
    public class BFCompiler
    {
        private StringBuilder bf;
        private int carriage;

        public BFCompiler()
        {
            bf = new StringBuilder();
            carriage = 0;
        }

        public void Minus(int pos, int val = 1) { Modify(pos, -val); }
        public void Plus(int pos, int val = 1) { Modify(pos, val); }
        public void Print(int pos) { MoveTo(pos); bf.Append('.'); }
        public void Input(int pos) { MoveTo(pos); bf.Append(','); }
        public void While(int pos, Action code)
        {
            MoveTo(pos);
            bf.Append('[');

            code();

            MoveTo(pos);
            bf.Append(']');
        }

        public void Modify(int pos, int val)
        {
            MoveTo(pos);

            char command = val >= 0 ? '+' : '-';
            int absVal = val >= 0 ? val : -val;

            bf.Append(new string(command, absVal % 256));
        }

        public void AppendMarker(char marker) => bf.Append(marker);

        public void MoveTo(int pos)
        {
            ShiftContext(pos - carriage);
            carriage = pos;
        }

        public void ShiftContext(int shift)
        {
            char command = shift > 0 ? '>' : '<';
            int absShift = shift >= 0 ? shift : -shift;

            bf.Append(new string(command, absShift));
        }

        public override string ToString() => bf.ToString();
    }
}
