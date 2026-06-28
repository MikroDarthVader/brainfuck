using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BFPcompiler
{
    public class BFcell : IDisposable
    {
        public readonly BFCompiler bf;
        public readonly int pos;
        private readonly Action<BFcell>? onDispose;
        private bool disposed;

        public BFcell(BFCompiler bf, int pos, Action<BFcell>? onDispose = null)
        {
            this.bf = bf;
            this.pos = pos;
            this.onDispose = onDispose;
        }

        /// <summary>
        /// Not self, b Safe
        /// O(n^2)
        /// if (self == b) => self = b = 0;
        /// if (self > b)  => self = self - b; b = 0;
        /// if (self < b)  => self = 0; b = b - self;
        /// </summary>
        /// <param name="b"></param>
        /// <param name="s1"></param>
        /// <param name="s2"></param>
        /// <param name="s3"></param>
        public void CompareTo(BFcell b, BFcell s1, BFcell s2, BFcell s3)
        {
            s1.Init();
            s2.Init();
            s3.Init();
            While(() =>
            {
                s1.Plus();
                b.While(() =>
                {
                    s1.Init();
                    s2.Plus();
                    b.Minus();
                });
                s1.AddTo(s3); // s1 => 0
                s2.AddTo(b);  // s2 => 0

                Minus();
                b.Minus();
            });

            s3.If(() =>
            {
                b.While(() =>
                {
                    Plus();
                    b.Plus();
                });
            });
        }

        /// <summary>
        /// Not self Safe
        /// O(n)
        /// </summary>
        /// <param name="res"></param>
        public void Not(BFcell res)
        {
            res.Init(1);
            If(() =>
            {
                res.Init();
            });
        }

        /// <summary>
        /// Not self Safe
        /// O(n)
        /// </summary>
        /// <param name="res"></param>
        public void ToBool(BFcell res)
        {
            res.Init();
            If(() =>
            {
                res.Plus();
            });
        }

        /// <summary>
        /// Not self Safe
        /// O(n)
        /// </summary>
        /// <param name="s1"></param>
        /// <param name="codeIf"></param>
        /// <param name="codeElse"></param>
        public void IfElse(BFcell s1, Action codeIf, Action codeElse)
        {
            s1.Init(1);
            If(() =>
            {
                codeIf();
                s1.Init();
            });
            s1.If(() =>
            {
                codeElse();
            });
        }

        /// <summary>
        /// Not self Safe
        /// O(n)
        /// </summary>
        /// <param name="code"></param>
        public void If(Action code)
        {
            While(() =>
            {
                code();
                Init();
            });
        }

        /// <summary>
        /// O(n)
        /// </summary>
        /// <param name="s1"></param>
        /// <param name="to"></param>
        public void CopyTo(BFcell s1, params BFcell[] to)
        {
            to = to.Append(s1).ToArray();
            MoveTo(to);
            s1.MoveTo(this);
        }

        /// <summary>
        /// O(n)
        /// </summary>
        /// <param name="to"></param>
        public void MoveTo(params BFcell[] to)
        {
            Array.Sort(to);

            foreach (var dest in to)
                dest.Init();

            AddTo(to);
        }

        /// <summary>
        /// Not self Safe
        /// to => to + self
        /// self => 0
        /// </summary>
        /// <param name="to"></param>
        public void AddTo(params BFcell[] to)
        {
            Array.Sort(to);

            While(() =>
            {
                Plus();
                foreach (var dest in to)
                    dest.Plus();
            });
        }

        public void Init(byte val = 0)
        {
            While(() => { Minus(); });
            Plus(val);
        }

        /// <summary>
        /// O(n)
        /// </summary>
        /// <param name="code"></param>
        private void While(Action code) => bf.While(pos, code);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            onDispose?.Invoke(this);
        }

        public void Plus(int val = 1) => bf.Plus(pos, val);
        public void Minus(int val = 1) => bf.Minus(pos, val);
    }

}
