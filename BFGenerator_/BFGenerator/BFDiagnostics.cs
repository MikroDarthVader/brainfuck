using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BFGen
{
    /// <summary>
    /// Configuration format for Brainfuck VM input and output streams.
    /// </summary>
    public enum BFIOFormat
    {
        /// <summary>Reads and writes raw numeric byte values (e.g., 65, 0).</summary>
        RawNumeric,
        /// <summary>Reads and writes standard ASCII text characters (e.g., 'A').</summary>
        ASCII
    }

    /// <summary>
    /// Advanced diagnostic suite and interpreter for the compiled Brainfuck source code.
    /// Intercepts the .[] termination sequence and determines if it is a safe exit or a fatal trap.
    /// </summary>
    public class BFDiagnostics
    {
        private readonly string code;
        private readonly BFIOFormat ioFormat;
        private readonly byte[] tape;
        private int ptr;
        private int codeIdx;

        // Tracks characters emitted by '.' to catch runtime crash error text signatures
        private readonly StringBuilder outBuffer = new();

        public BFDiagnostics(string compiledCode, BFIOFormat ioFormat = BFIOFormat.RawNumeric)
        {
            this.code = compiledCode ?? throw new ArgumentNullException(nameof(compiledCode));
            this.ioFormat = ioFormat;
            this.tape = new byte[30000]; // Standard virtual machine tape size
            this.ptr = 0;
            this.codeIdx = 0;
        }

        /// <summary>
        /// Executes the Brainfuck program inside the diagnostic VM.
        /// Automatically intercepts runtime error traps and formats the terminal output.
        /// </summary>
        public void Execute()
        {
            outBuffer.Clear();
            bool crashed = false;

            while (codeIdx < code.Length)
            {
                // INTERCEPT ACCURATE TERMINATION SIGNAL: ".[]"
                // Both success exits (value 0) and fatal crash traps (value != 0) trigger this block.
                if (codeIdx + 2 < code.Length && code[codeIdx] == '.' && code[codeIdx + 1] == '[' && code[codeIdx + 2] == ']')
                {
                    // Catch the absolute final character into the buffer before freezing
                    outBuffer.Append((char)tape[ptr]);
                    crashed = true;

                    // FIX: Invoking the correct method name
                    HandleRuntimeCrash();
                    break; // Stop execution right here before entering the deadlock block
                }

                char cmd = code[codeIdx];
                switch (cmd)
                {
                    case '>': ptr++; break;
                    case '<': ptr--; break;
                    case '+': tape[ptr]++; break;
                    case '-': tape[ptr]--; break;

                    case '.':
                        char character = (char)tape[ptr];
                        outBuffer.Append(character); // Feed the string capture buffer

                        if (ioFormat == BFIOFormat.ASCII)
                            Console.Write(character);
                        else
                            Console.Write($"[{tape[ptr]}] ");
                        break;

                    case ',':
                        if (ioFormat == BFIOFormat.ASCII)
                        {
                            tape[ptr] = (byte)Console.Read();
                        }
                        else
                        {
                            Console.Write("\nInput: ");
                            string input = Console.ReadLine() ?? "0";
                            tape[ptr] = byte.TryParse(input, out byte val) ? val : (byte)0;
                        }
                        break;

                    case '[':
                        if (tape[ptr] == 0)
                        {
                            int depth = 1;
                            while (depth > 0)
                            {
                                codeIdx++;
                                if (codeIdx >= code.Length) throw new InvalidOperationException("Unbalanced brackets: Missing closing ']' loop boundary.");
                                if (code[codeIdx] == '[') depth++;
                                if (code[codeIdx] == ']') depth--;
                            }
                        }
                        break;

                    case ']':
                        if (tape[ptr] != 0)
                        {
                            int depth = 1;
                            while (depth > 0)
                            {
                                codeIdx--;
                                if (codeIdx < 0) throw new InvalidOperationException("Unbalanced brackets: Missing opening '[' loop boundary.");
                                if (code[codeIdx] == ']') depth++;
                                if (code[codeIdx] == '[') depth--;
                            }
                        }
                        break;
                }
                codeIdx++;
            }

            // Normal fallback exit if the code ends without the explicit .[] sequence
            if (!crashed)
            {
                Console.WriteLine();
                PrintNonZeroTapeDump();
            }
        }

        /// <summary>
        /// Inspects the active tape cell value at the moment of .[] interception.
        /// Determines if the loop would execute permanently (Crash) or safely pass through (Success OK).
        /// </summary>
        private void HandleRuntimeCrash()
        {
            Console.WriteLine(); // Break line away from standard execution print stream

            // Case A: The active cell is 0. The cycle [] would skip entirely.
            // This is the clean, successful program completion marker.
            if (tape[ptr] == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n[SUCCESS]");
                Console.ResetColor();
            }
            // Case B: The active cell is non-zero. The cycle [] would trap the execution thread forever.
            // This is an unrecoverable runtime violation.
            else
            {
                string matchedEnumToken = "UNKNOWN_ERROR";
                if (outBuffer.Length > 0)
                {
                    // Последний символ в буфере — это код ошибки (выведенный через BFRuntimeError)
                    byte errorCode = (byte)outBuffer[^1];
                    var enumName = Enum.GetName(typeof(BFRuntimeError.ErrCode), errorCode);
                    if (enumName != null)
                        matchedEnumToken = enumName;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[FATAL CRASH TRAP DETECTED]");
                Console.WriteLine($"Err: {matchedEnumToken}");
                Console.ResetColor();
            }

            // Print the tape summary at the end of the termination phase
            PrintNonZeroTapeDump();
        }

        /// <summary>
        /// Scans the virtual machine tape and displays all non-zero cells with their indices.
        /// </summary>
        private void PrintNonZeroTapeDump()
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\n=== NON-ZERO TAPE MEMORY DUMP ===");

            bool empty = true;
            for (int i = 0; i < tape.Length; i++)
            {
                if (tape[i] != 0)
                {
                    Console.WriteLine($"  Cell #{i,-5} => {tape[i]}");
                    empty = false;
                }
            }

            if (empty)
            {
                Console.WriteLine("  (All tape cells are completely zeroed out)");
            }

            Console.WriteLine($"\nlast carriage pos: {ptr}");

            Console.WriteLine("=================================");
            Console.ResetColor();
        }
    }
}
