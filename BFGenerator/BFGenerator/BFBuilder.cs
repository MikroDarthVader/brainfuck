using System.Text;

namespace BFGen
{
    /// <summary>
    /// Low‑level Brainfuck code emitter.
    /// Stores instructions as run‑length tokens to optimise long sequences of identical commands
    /// and to cancel opposite operations.
    /// </summary>
    internal class BFBuilder
    {
        private readonly StringBuilder bf = new();

        private int posInContext;

        /// <summary>
        /// Adds <paramref name="count"/> identical BF instructions.
        /// Merges with the previous token if it is the same command;
        /// cancels opposite commands (+/-, >/<) and recurses with the remainder when necessary.
        /// </summary>
        /// <param name="bfInst">BF command character. Must be '+', '-', '>', '<', '.', ',', '[', or ']'.</param>
        /// <param name="count">Number of repetitions (positive).</param>
        private string EmitRun(char bfInst, int count)
        {
            if (count <= 0)
                return "";

            var str = new string(bfInst, count);
            bf.Append(str);
            return str;
        }

        /// <summary>
        /// Moves the tape head to absolute position <paramref name="dest"/>.
        /// </summary>
        public string BFMoveTo(int dest)
        {
            int delta = dest - posInContext;
            char cmd = delta > 0 ? '>' : '<';
            int absDelta = Math.Abs(delta);
            posInContext = dest;
            return EmitRun(cmd, absDelta);
        }

        /// <summary>
        /// Shifts the tape head by <paramref name="shift"/> cells without changing
        /// the current context.
        /// </summary>
        public string BFShiftContext(int shift)
        {
            char cmd = shift > 0 ? '>' : '<';
            int absShift = Math.Abs(shift);
            return EmitRun(cmd, absShift);
        }

        /// <summary>
        /// Emits a single BF instruction, optionally repeated <paramref name="count"/> times.
        /// </summary>
        public string BFPut(char bfInst, int count = 1)
        {
            if (!"+-.,[]".Contains(bfInst) || count <= 0)
                return "";

            return EmitRun(bfInst, count);
        }

        /// <summary>
        /// Assembles the final Brainfuck source code from the accumulated tokens.
        /// </summary>
        public override string ToString()
        {
            return bf.ToString();
        }
    }
}
