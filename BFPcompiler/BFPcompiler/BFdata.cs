using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFPcompiler
{
    /// <summary>
    /// в описаниях функций:
    ///     d - длина пути между данными
    ///     s - размер данных
    /// </summary>
    //public class BFdata
    //{
    //    public BFCompiler bf { get; private set; }
    //    public readonly int pos;
    //    public readonly BFcell[];

    //    public BFdata(BFCompiler bf, int pos, int size)
    //    {
    //        this.bf = bf;
    //        this.pos = pos;
    //        this.size = size;
    //    }

    //    public void Init()
    //    {
    //        for (int i = 0; i < size; i++)
    //            bf.While(pos + i, () => { bf--; });
    //    }

    //    /// <summary>
    //    /// O(ds)
    //    /// </summary>
    //    /// <param name="service"></param>
    //    /// <param name="to"></param>
    //    public void CopyVal(int s1, params BFdata[] to)
    //    {
    //        to = to.Append(s1).ToArray();
    //        MoveVal(to);
    //        MoveVal(s1, this);
    //    }

    //    /// <summary>
    //    /// O(ds)
    //    /// </summary>
    //    /// <param name="to"></param>
    //    public void MoveVal(params BFdata[] to)
    //    {
    //        Array.Sort(to);

    //        for (int i = 0; i < size; i++)

    //            foreach (var dest in to)
    //            {
    //                if (dest.size < size)
    //                    throw new Exception("***");

    //                dest.Init();

    //                bf.While(pos, () =>
    //                {
    //                    bf--;
    //                    foreach (var dest in to)
    //                        Plus(dest);
    //                });
    //            }
    //    }
    //}
}
