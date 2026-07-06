namespace BFGen
{
    /// <summary>
    /// Low‑level Brainfuck code emitter.
    /// Stores instructions as run‑length tokens to optimise long sequences of identical commands
    /// and to cancel opposite operations.
    /// </summary>
    internal class BFBuilder
    {
        /// <summary>Run‑length encoded list of BF commands (command, repetition count).</summary>
        private readonly List<(char cmd, int count)> tokens = [];

        /// <summary>Current absolute tape position (used by <see cref="BFMoveTo"/>).</summary>
        private int posInContext;

        /// <summary>
        /// Adds <paramref name="count"/> identical BF instructions.
        /// Merges with the previous token if it is the same command;
        /// cancels opposite commands (+/-, >/<) and recurses with the remainder when necessary.
        /// </summary>
        /// <param name="bfInst">BF command character. Must be '+', '-', '>', '<', '.', ',', '[', or ']'.</param>
        /// <param name="count">Number of repetitions (positive).</param>
        private void EmitRun(char bfInst, int count)
        {
            if (count <= 0)
                return;

            // Try to combine with the last token
            if (tokens.Count > 0)
            {
                var (lastCmd, lastCount) = tokens[^1];

                // Opposite commands cancel each other
                bool isOpposite = (bfInst == '+' && lastCmd == '-') ||
                                  (bfInst == '-' && lastCmd == '+') ||
                                  (bfInst == '>' && lastCmd == '<') ||
                                  (bfInst == '<' && lastCmd == '>');

                if (isOpposite)
                {
                    if (lastCount > count)
                    {
                        // The existing opposite token outlasts the new one
                        tokens[^1] = (lastCmd, lastCount - count);
                        return;
                    }
                    else if (lastCount == count)
                    {
                        // Perfect cancellation
                        tokens.RemoveAt(tokens.Count - 1);
                        return;
                    }
                    else // lastCount < count
                    {
                        // The new run is longer – remove the old token and keep the remainder
                        tokens.RemoveAt(tokens.Count - 1);
                        EmitRun(bfInst, count - lastCount);
                        return;
                    }
                }

                // Same command – just increase the count
                if (bfInst == lastCmd)
                {
                    tokens[^1] = (lastCmd, lastCount + count);
                    return;
                }
            }

            // No merge possible – append a new token
            tokens.Add((bfInst, count));
        }

        /// <summary>
        /// Moves the tape head to absolute position <paramref name="dest"/>.
        /// </summary>
        public void BFMoveTo(int dest)
        {
            int delta = dest - posInContext;
            char cmd = delta > 0 ? '>' : '<';
            int absDelta = Math.Abs(delta);
            EmitRun(cmd, absDelta);
            posInContext = dest;
        }

        /// <summary>
        /// Shifts the tape head by <paramref name="shift"/> cells without changing
        /// the current context.
        /// </summary>
        public void BFShiftContext(int shift)
        {
            char cmd = shift > 0 ? '>' : '<';
            int absShift = Math.Abs(shift);
            EmitRun(cmd, absShift);
        }

        /// <summary>
        /// Emits a single BF instruction, optionally repeated <paramref name="count"/> times.
        /// </summary>
        public void BFPut(char bfInst, int count = 1)
        {
            if (!"+-.,[]".Contains(bfInst) || count <= 0)
                return;

            EmitRun(bfInst, count);
        }

        /// <summary>
        /// Assembles the final Brainfuck source code from the accumulated tokens.
        /// </summary>
        public override string ToString()
        {
            if (tokens.Count == 0)
                return string.Empty;

            // Calculate total length
            int totalLength = 0;
            foreach (var (_, count) in tokens)
                totalLength += count;

            char[] result = new char[totalLength];
            int index = 0;
            foreach (var (cmd, count) in tokens)
            {
                for (int i = 0; i < count; i++)
                    result[index++] = cmd;
            }

            return new string(result);
        }
    }
}
