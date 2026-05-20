using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFPcompiler
{
    public class Cell
    {
        public BFCompiler bf { get; private set; }
        public readonly int pos;

        public Cell(BFCompiler bf, int pos)
        {
            this.bf = bf;   
            this.pos = pos;
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
        /// Not valSafe
        /// to => to+from
        /// from => 0
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        private void Sum(params Cell[] to)
        {
            Array.Sort(to);

            bf.While(pos, () =>
            {
                bf--;
                foreach (var dest in to)
                {
                    bf.MoveTo(dest.pos);
                    bf++;
                }
            });
        }

        private void Init(byte val = 0)
        {
            bf.While(pos, () => {  });
            bf += val;
        }
    }

    /// <summary>
    /// в описаниях функций:
    ///     d - длина пути между данными
    ///     s - размер данных
    /// </summary>
    public class BFdata
    {
        public BFCompiler bf { get; private set; }
        public readonly int pos;
        public readonly int size;

        public BFdata(BFCompiler bf, int pos, int size)
        {
            this.bf = bf;
            this.pos = pos;
            this.size = size;
        }

        public void Init()
        {
            for (int i = 0; i < size; i++)
                bf.While(pos + i, () => { bf--; });
        }

        /// <summary>
        /// O(ds)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="service"></param>
        /// <param name="to"></param>
        public void CopyVal(int serviceCell, params BFdata[] to)
        {
            to = to.Append(service).ToArray();
            MoveVal(to);
            MoveVal(service, this);
        }

        /// <summary>
        /// O(ds)
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void MoveVal(params BFdata[] to)
        {
            Array.Sort(to);

            for (int i = 0; i < size; i++)

                foreach (var dest in to)
            {
                if (dest.size < size)
                    throw new Exception("***");
                
                dest.Init();

                bf.While(pos, () =>
                {
                    bf--;
                    foreach (var dest in to)
                        Plus(dest);
                });
            }
        }
    }
}
