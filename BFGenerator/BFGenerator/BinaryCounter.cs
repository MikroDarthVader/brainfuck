using BFGo;

namespace tests
{
    /// <summary>
    /// Prints natural numbers in binary, one per line, indefinitely.
    ///
    /// Value layout: bit i lives in data[0] of context i; bit 0 is in the
    /// root (address 0). Working state lives on the stack and travels with
    /// each Go.
    ///
    /// Only single-digit addresses are supported: bitCount, bitIndex and
    /// remaining are single-cell variables, so the counter is bounded by
    /// cellSize - 1 bits. Extending to multi-digit addresses requires
    /// multi-digit arithmetic on those variables.
    /// </summary>
    class BinaryCounter : BFGProgram
    {
        public BinaryCounter()
            : base(addrSize: 1, stackDens: 1, dataDens: 1, cellSize: 256) { }

        public override void Code()
        {
            using var frame = CreateScope();

            var bitCount = frame.Alloc();  // number of bits in the current value
            var bitIndex = frame.Alloc();  // current bit position (0 = LSB)
            var remaining = frame.Alloc();  // bits left to process in a pass

            bitCount.Set(1);
            // Value starts at 0: bit 0 in context 0 is already zero.

            frame.Alloc().Set(1).While(() =>
            {
                PrintValue(bitCount, bitIndex, remaining);
                Increment(bitCount, bitIndex, remaining);
            });
        }

        /// <summary>
        /// Prints data[0] of the current context as '0' or '1'.
        /// charBuf is reused as the output cell.
        /// </summary>
        private void PrintBit(BFVar charBuf)
        {
            using var scope = CreateScope();
            var scratch = scope.Alloc();

            GetData().CopyTo(scratch);
            charBuf.Set('0');
            scratch.If(() => charBuf.Set('1'));
            charBuf.Print();
        }

        /// <summary>
        /// Prints the current value MSB-first, followed by a newline.
        /// </summary>
        private void PrintValue(BFVar bitCount, BFVar bitIndex, BFVar remaining)
        {
            using var scope = CreateScope();
            var charBuf = scope.Alloc();

            bitCount.CopyTo(bitIndex);
            bitIndex.Change(-1);            // start at the highest bit
            bitCount.CopyTo(remaining);

            remaining.While(() =>
            {
                Go(bitIndex);
                PrintBit(charBuf);
                bitIndex.Change(-1);
                remaining.Change(-1);
            });

            charBuf.Set('\n');
            charBuf.Print();
        }

        /// <summary>
        /// Increments the value by 1, carrying through leading 1-bits.
        /// Appends a new bit if the carry reaches past the current length.
        /// </summary>
        private void Increment(BFVar bitCount, BFVar bitIndex, BFVar remaining)
        {
            using var scope = CreateScope();
            var carry = scope.Alloc();
            var bitCopy = scope.Alloc();  // preserves the current bit
            var helper = scope.Alloc();

            bitIndex.Zero();
            carry.Set(1);
            bitCount.CopyTo(remaining);

            carry.While(() =>
            {
                Go(bitIndex);

                // Past the current length: append a new leading 1-bit.
                remaining.IsZero(helper);
                helper.If(() =>
                {
                    GetData().Set(1);
                    bitCount.Change(1);
                    carry.Zero();
                });

                // Existing bit: flip it and stop carry at the first 0.
                remaining.CopyTo(helper);
                helper.If(() =>
                {
                    GetData().CopyTo(bitCopy);

                    // bit was 1: clear, carry continues.
                    bitCopy.CopyTo(helper);
                    helper.If(() => GetData().Set(0));

                    // bit was 0: set, carry stops.
                    bitCopy.IsZero(helper);
                    helper.If(() =>
                    {
                        GetData().Set(1);
                        carry.Zero();
                    });

                    remaining.Change(-1);
                    bitIndex.Change(1);
                });
            });
        }
    }
}
