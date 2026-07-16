namespace BFGo
{
    /// <summary>
    /// Generates runtime error traps inside the Brainfuck program.
    /// Emits code to print error markers and enters an unrecoverable infinite loop to halt execution.
    /// </summary>
    public class BFRuntimeError
    {

        public enum ErrCode
        {
            OK = 0,
            ERR_SAME_PTR
        }
        /// <summary>
        /// Emits a runtime crash trap. Prints the error code and locks the execution thread.
        /// </summary>
        public BFRuntimeError(BFContext context, ErrCode error)
        {
            var ir = context.IR;

            var errorCellDesc = context.Alloc(AllocatorKind.Stack, 1);
            var errorCell = errorCellDesc[0];

            errorCell.Init((byte)error);
            errorCell.Print();

            errorCell.While(() => {});
        }
    }
}
