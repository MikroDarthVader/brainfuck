using System.Text;

namespace BFPcompiler
{
    /// <summary>
    /// Поддержка базовых операций brainfuck
    /// </summary>
    public class BFCompiler
    {
        private StringBuilder bf;
        private int carriage;

        public BFCompiler()
        {
            bf = new StringBuilder();
            carriage = 0;
        }

        public static BFCompiler operator ++(BFCompiler bfc) { return bfc += 1; }
        public static BFCompiler operator --(BFCompiler bfc) { return bfc -= 1; }

        public static BFCompiler operator >>(BFCompiler bfc, int count)
        {
            bfc.ShiftContext(count);
            return bfc;
        }

        public static BFCompiler operator <<(BFCompiler bfc, int count)
        {
            bfc.ShiftContext(-count);
            return bfc;
        }

        public static BFCompiler operator +(BFCompiler bfc, byte val)
        {
            bfc.Modify(val);
            return bfc;
        }

        public static BFCompiler operator -(BFCompiler bfc, byte val)
        {
            bfc.Modify(-val);
            return bfc;
        }

        public void MoveTo(int pos)
        {
            ShiftContext(pos - carriage);
            carriage = pos;
        }

        protected void Modify(int val)
        {
            char command = val >= 0 ? '+' : '-';
            int absVal = val >= 0 ? val : -val;

            bf.Append(new string(command, absVal));
        }

        public void While(int pos, Action code)
        {
            MoveTo(pos);
            bf.Append('[');
            code();
            MoveTo(pos);
            bf.Append(']');
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
