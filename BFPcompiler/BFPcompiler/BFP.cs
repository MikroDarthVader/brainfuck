using System.Linq;
using System.Text;

public class BFP
{
    public bool Debug;
    public class CompileException : Exception
    {
        public CompileException(string str) : base(str) { }
    }

    private enum Cell
    {
        c_ptr,
        c_service,
        c_data,
        r_service1,
        r_service2,
        r_data,
        rr_service1,
        rr_service2,
        rr_data
    }
    private int BlockSize = 3;

    private StringBuilder bf;
    private Cell carriage;

    public BFP(bool debug)
    {
        bf = new StringBuilder();
        carriage = Cell.c_ptr;
        Debug = debug;
    }

    public string Process()
    {
        /*compilation*/
        for (int i = 0; i < 50; i++)
            Plus(Cell.c_data);
        GoByPtr();
        
        MoveWindowRight();

        for (int i = 0; i < 42; i++)
            Plus(Cell.c_data);

        MoveWindowRight();
        GoByPtr();

        Plus(Cell.c_data);
        GoByPtr();

        Print(Cell.c_data);

        return bf.ToString();
    }

    public void GoByPtr()
    {
        Cell toPtr = Cell.r_service2;
        Cell fromPtr = Cell.r_service1;
        Cell s1 = Cell.c_service;
        Cell s2 = Cell.rr_service1;
        Cell s3 = Cell.rr_service2;

        CopyVal(Cell.c_ptr, s1, fromPtr);
        CopyVal(Cell.c_data, s1, toPtr);

        Compare(fromPtr, toPtr, s1, s2, s3);
        var RightMovement = toPtr;
        var leftMovement = fromPtr;

        While(leftMovement, () =>
        {
            Minus(leftMovement);
            MoveVal(leftMovement, leftMovement - BlockSize);
            MoveWindowLeft();
            Zero(RightMovement);
        });
        While(RightMovement, () =>
        {
            Minus(RightMovement);
            MoveVal(RightMovement, RightMovement + BlockSize);
            MoveWindowRight();
        });
    }

    private void MoveWindowRight()
    {
        CopyVal(Cell.c_ptr, Cell.c_service, Cell.c_ptr + BlockSize);
        Plus(Cell.c_ptr + BlockSize);
        bf.Append(">>>");
    }

    private void MoveWindowLeft()
    {
        bf.Append("<<<");
        //add checkup of moving in negative space
    }

    /// <summary>
    ///     /// Not a, b Safe
    /// O(n^2)
    /// if (a==b) => a = b = 0;
    /// if (a>b)  => a = a-b; b = 0;
    /// if (a<b)  => a = 0; b = b-a;
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="s1"></param>
    /// <param name="s2"></param>
    /// <param name="s3"></param>
    private void Compare(Cell a, Cell b, Cell s1, Cell s2, Cell s3)
    {
        Zero(s1);
        Zero(s2);
        Zero(s3);
        While(a, () =>
        {
            Plus(s1);
            While(b, () =>
            {
                Zero(s1);
                Plus(s2);
                Minus(b);
            });
            Sum(s1, s3);
            Sum(s2, b);

            Minus(a);
            Minus(b);
        });

        If(s3, () =>
        {
            While(b, () =>
            {
                Plus(a);
                Plus(b);
            });
        });
    }
    // 
    /// <summary>
    /// Not val Safe
    /// O(n)
    /// </summary>
    /// <param name="val"></param>
    /// <param name="res"></param>
    private void Not(Cell val, Cell res)
    {
        Zero(res);
        Plus(res);
        If(val, () =>
        {
            Minus(val);
            Minus(res);
        });
    }

    /// <summary>
    /// Not val Safe
    /// O(n)
    /// </summary>
    /// <param name="val"></param>
    /// <param name="s1"></param>
    /// <param name="s2"></param>
    /// <param name="codeIf"></param>
    /// <param name="codeElse"></param>
    private void IfElse(Cell val, Cell service, Action codeIf, Action codeElse)
    {
        Zero(service);
        Plus(service);
        If(val, () =>
        {
            codeIf();
            Minus(service);
        });
        If(service, () =>
        {
            codeElse();
        });
    }

    /// <summary>
    /// Not val Safe
    /// O(n)
    /// </summary>
    /// <param name="val"></param>
    /// <param name="res"></param>
    private void ToBool(Cell val, Cell res)
    {
        Zero(res);
        If(val, () =>
        {
            Plus(res);
        });
    }

    /// <summary>
    /// Not val Safe
    /// O(n)
    /// </summary>
    /// <param name="val"></param>
    /// <param name="code"></param>
    private void If(Cell val, Action code)
    {
        While(val, () =>
        {
            code();
            Zero(val);
        });
    }

    /// <summary>
    /// O(n)
    /// </summary>
    /// <param name="from"></param>
    /// <param name="service"></param>
    /// <param name="to"></param>
    private void CopyVal(Cell from, Cell service, params Cell[] to)
    {
        to = to.Append(service).ToArray();
        MoveVal(from, to);
        MoveVal(service, from);
    }

    /// <summary>
    /// O(n)
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    private void MoveVal(Cell from, params Cell[] to)
    {
        Array.Sort(to);

        foreach (var dest in to)
            Zero(dest);

        Sum(from, to);
    }

    /// <summary>
    /// Not valSafe
    /// to => to+from
    /// from => 0
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    private void Sum(Cell from, params Cell[] to)
    {
        Array.Sort(to);

        While(from, () =>
        {
            Minus(from);
            foreach (var dest in to)
                Plus(dest);
        });
    }

    private void Error(object toPrint)
    {
        Print(toPrint, Cell.c_service);
        Zero(Cell.c_service);
        Plus(Cell.c_service);
        While(Cell.c_service, () => { }); // ethernal while after error occurs
    }

    private void Print(object toPrint, Cell service)
    {
        var str = toPrint.ToString();

        if (str == null)
            return;

        Zero(service);

        char output = '\0';
        foreach (char c in str)
        {
            while (output != c)
            {
                if (output > c)
                {
                    output--;
                    Minus(service);
                }
                else
                {
                    output++;
                    Plus(service);
                }
            }
            Print(service);
        }
    }

    private void Zero(Cell val)
    {
        While(val, () =>
        {
            Minus(val);
        });
    }

    private void Minus(Cell val) { MoveCarriage(val); bf.Append('-'); }
    private void Plus(Cell val) { MoveCarriage(val); bf.Append('+'); }
    private void Print(Cell val) { MoveCarriage(val); bf.Append('.'); }
    private void Input(Cell val) { bf.Append(','); }
    private void While(Cell val, Action code)
    {
        MoveCarriage(val);
        bf.Append('[');

        code();

        MoveCarriage(val);
        bf.Append(']');
    }

    private void MoveCarriage(Cell to)
    {
        while (carriage != to)
        {
            if (carriage > to)
            {
                carriage--;
                bf.Append('<');
            }
            else
            {
                carriage++;
                bf.Append('>');
            }
        }
    }
}