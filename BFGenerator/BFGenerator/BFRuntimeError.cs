namespace BFGen
{
    /// <summary>
    /// Generates runtime error traps inside the Brainfuck program.
    /// Emits code to print error markers and enters an unrecoverable infinite loop to halt execution.
    /// </summary>
    public class BFRuntimeError
    {
        /// <summary>
        /// Emits a runtime crash trap. Prints the error code and locks the execution thread.
        /// </summary>
        public BFRuntimeError(BFContext context, string error)
        {
            var ir = context.IR;

            var errorCellDesc = context.Alloc(AllocatorKind.Stack, 1);
            var errorCell = errorCellDesc[0]!;

            foreach (var c in error)
            {
                errorCell.Init((byte)c);
                errorCell.Print();
            }

            errorCell.While(() => {});
        }
    }
}
