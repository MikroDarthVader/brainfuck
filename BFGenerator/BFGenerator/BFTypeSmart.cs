using BFGo;

namespace BFTypeSmart
{
   
   /*public class BFaddressType : BFType
   {
       public BFUIntType Pos { get; }
       public BFUIntType Neg { get; }

       public BFaddressType(int addrSize) : base()
       {

           Pos = RegisterField(new BFUIntType(addrSize));
           Neg = RegisterField(new BFUIntType(addrSize));
       }

          public void shorten(BFVar desc) // бойтесь ребятки сейчас будем проводить внеплановое сокращение
       {
           var posDesc = Pos.From(desc);
           var negDesc = Neg.From(desc);

           var ctx = desc.env;
           using var tmp = ctx.Alloc(1);
           using var tmp2 = ctx.Alloc(1);

           using var flag = ctx.Alloc(1);

           using var tmpPos = ctx.Alloc(Pos.Size);
           using var tmpNeg = ctx.Alloc(Pos.Size);



           for (int i = 0; i < Pos.Size-1; i++) 
           {
               BFCell.Compare(posDesc[i], negDesc[i]);

               tmp2[0].Init(128);

               posDesc.CopyTo(tmpPos);
               negDesc.CopyTo(tmpNeg);

               posDesc[i].CopyTo(tmp[0]);
               tmp[0].IfElse(() =>   // pos > neg
               {
                   BFCell.Compare(tmp[0], tmp2[0]);
                   tmp[0].If(() =>
                   {
                       tmp2[0].Init(255);
                       posDesc[i].While(() =>
                       {
                           tmp2[0].Minus(1);
                           posDesc[i].Minus(1);
                       });
                       tmp2[0].Plus(1);

                       this.Plus(negDesc, tmp2, i);

                       if (i < Pos.Size - 1)
                       {
                           flag[0].Init(1); // флаг переноса

                           for (int i2 = i + 1; i2 < Pos.Size; i2++)
                           {
                               flag.CopyTo(tmp2); // temp = flag

                               // Если temp != 0 (перенос активен)
                               tmp2[0].If(() =>
                               {

                                   posDesc[i2].Plus(1); // инкремент байта

                                   posDesc[i2].CopyTo(tmp2[0]); // tmp2 = cell

                                   // Если tmp2 != 0, переноса нет – обнуляем flag
                                   tmp2[0].If(() =>
                                   {
                                       flag[0].Init(0);
                                   });

                               });
                           }


                       }
                       flag[0].If(() =>
                       {
                           tmpPos.CopyTo(posDesc);
                           tmpNeg.CopyTo(negDesc);
                       });

                   });
               }, () =>
               {                       // pos < neg
                   negDesc[i].CopyTo(tmp[0]);
                   BFCell.Compare(tmp[0], tmp2[0]);
                   tmp[0].If(() =>
                   {

                       tmp2[0].Init(255);
                       negDesc[i].While(() =>
                       {
                           tmp2[0].Minus(1);
                           negDesc[i].Minus(1);
                       });
                       tmp2[0].Plus(1);

                       this.Plus(posDesc, tmp2, i);

                       if (i < Pos.Size - 1)
                       {
                           flag[0].Init(1); // флаг переноса

                           for (int i2 = i + 1; i2 < Pos.Size; i2++)
                           {
                               flag.CopyTo(tmp2); // temp = flag

                               // Если temp != 0 (перенос активен)
                               tmp2[0].If(() =>
                               {

                                   negDesc[i2].Plus(1); // инкремент байта

                                   negDesc[i2].CopyTo(tmp2[0]); // tmp2 = cell

                                   // Если tmp2 != 0, переноса нет – обнуляем flag
                                   tmp2[0].If(() =>
                                   {
                                       flag[0].Init(0);
                                   });

                               });
                           }


                       }
                       flag[0].If(() =>
                       {
                           tmpPos.CopyTo(posDesc);
                           tmpNeg.CopyTo(negDesc);
                       });


                   });
               });
           }

       }


       public void Plus(BFVar self, BFVar other, int move)
       {

           var uintType = new BFUIntType(self.Size);

           var ctx = self.env;

           using var other_tmp = ctx.Alloc(other.Size);
           other.CopyTo(other_tmp);

           using var duty = ctx.Alloc(1);

           using var tmp = ctx.Alloc(1);

           var min_size = other.Size < self.Size ? other.Size : self.Size;

           duty[0].Init();

           int ix;

           for (int i = 0; i < self.Size - 1; i++)
           {
               ix = i + move < self.Size ? i + move : i;

               self[ix].CopyTo(tmp[0]);
               tmp[0].IfElse(() =>
               {

                   duty[0].If(() =>
                   {
                       self[ix].Plus();
                   });

                   self[ix].CopyTo(tmp[0]);
                   tmp[0].If(() =>
                   {
                       duty[0].Minus();
                   });
                   duty[0].Plus();
               }, () =>
               {

                   duty[0].If(() =>
                   {
                       self[ix].Plus();
                   });

               });



               if (i < other.Size)
               {
                   other_tmp[i].While(() =>
                   {
                       other_tmp[i].Minus(1);
                       self[ix].Plus(1);

                       self[ix].CopyTo(tmp[0]);
                       tmp[0].If(() =>
                       {
                           duty[0].Minus();
                       });
                       duty[0].Plus();
                   });
               }
           }
       }      
   }




   public class BFUIntType : BFType
   {
       public BFUIntType(int size = 0) : base(size) { }


       public void Plus(BFVar self, BFVar other, int move = 0) // move это сдвиг числа прибавляемого
       {

           var uintType = new BFUIntType(self.Size);

           var ctx = self.env;

           using var other_tmp = ctx.Alloc(other.Size);
           other.CopyTo(other_tmp);

           using var duty = ctx.Alloc(1);

           using var tmp = ctx.Alloc(1);

           var min_size = other.Size < self.Size ? other.Size : self.Size;

           duty[0].Init();

           for (int i = 0; i < Size - move; i++)
           {

               self[i + move].CopyTo(tmp[0]);
               tmp[0].IfElse(() =>
               {

                   duty[0].If(() =>
                   {
                       self[i + move].Plus();
                   });

                   self[i + move].CopyTo(tmp[0]);
                   tmp[0].If(() =>
                   {
                       duty[0].Minus();
                   });
                   duty[0].Plus();
               }, () =>
               {

                   duty[0].If(() =>
                   {
                       self[i + move].Plus();
                   });

               });



               if (i < other.Size)
               {
                   other_tmp[i].While(() =>
                   {
                       other_tmp[i].Minus(1);
                       self[i + move].Plus(1);

                       self[i + move].CopyTo(tmp[0]);
                       tmp[0].If(() =>
                       {
                           duty[0].Minus();
                       });
                       duty[0].Plus();
                   });
               }
           }

       }

       // self size can be != other size
       public void Minus(BFVar self, BFVar other, int move=0)
       {

           var uintType = new BFUIntType(self.Size);

           var ctx = self.env;

           using var other_tmp = ctx.Alloc(other.Size);
           other.CopyTo(other_tmp);

           using var duty = ctx.Alloc(1);

           using var tmp = ctx.Alloc(1);

           var min_size = other.Size < self.Size ? other.Size : self.Size;

           duty[0].Init();

           for (int i = 0; i < Size - 1 - move; i++)
           {

               self[i + move].CopyTo(tmp[0]);
               tmp[0].IfElse(() =>
               {

                   duty[0].If(() =>
                   {
                       self[i + move].Minus();
                   });


               }, () =>
               {

                   duty[0].If(() =>
                   {
                       self[i + move].Minus();
                   });


                   tmp[0].If(() =>
                   {
                       duty[0].Minus();
                   });
                   duty[0].Plus();

               });



               if (i < other.Size)
               {
                   other_tmp[i].While(() =>
                   {
                       self[i + move].CopyTo(tmp[0]);

                       other_tmp[i].Minus(1);
                       self[i + move].Minus(1);


                       tmp[0].If(() =>
                       {
                           duty[0].Minus();
                       });
                       duty[0].Plus();
                   });
               }
           }

       }


       public void Plus1(BFVar desc, int move=0)
       {

           var ctx = desc.env;

           // Временные ячейки
           using var flagVar = ctx.Alloc(1);
           using var tempVar = ctx.Alloc(1);
           using var tmp2Var = ctx.Alloc(1);

           var flag = flagVar[0];
           var temp = tempVar[0];
           var tmp2 = tmp2Var[0];


           flag.Init(1); // флаг переноса

           for (int i = move; i < Size; i++)
           {
               //   desc[i].Print();
               flag.CopyTo(temp); // temp = flag

               // Если temp != 0 (перенос активен)
               temp.While(() =>
               {

                   desc[i].Plus(1); // инкремент байта

                   desc[i].CopyTo(tmp2); // tmp2 = cell

                   // Если tmp2 != 0, переноса нет – обнуляем flag
                   tmp2.While(() =>
                   {
                       flag.Init(0);
                       tmp2.Init(0);
                   });

                   tmp2.Init(0); // очистка


                   temp.Init(0);
               });
           }
       }

       public void Minus1(BFVar desc, int move=0)
       {

           var ctx = desc.env;

           using var flagVar = ctx.Alloc(1);
           using var tempVar = ctx.Alloc(1);
           using var tmp2Var = ctx.Alloc(1);

           var flag = flagVar[0];
           var temp = tempVar[0];
           var tmp2 = tmp2Var[0];

           flag.Init(1); // флаг заёма

           for (int i = move; i < Size; i++)
           {

               flag.CopyTo(temp); // temp = flag

               // Если temp != 0 (заём активен)
               temp.While(() =>
               {
                   // Сохраняем копию байта в tmp2
                   desc[i].CopyTo(tmp2);

                   // Если tmp2 != 0, байт не ноль -> декремент и завершение заёма
                   tmp2.While(() =>
                   {
                       desc[i].Minus(1);   // уменьшаем на 1 (если есть Dec() – используйте его)
                       flag.Init(0);    // заём завершён
                       tmp2.Init(0);    // выход из внутреннего цикла
                   });

                   // Теперь tmp2 = 0 (либо был 0, либо обнулён)
                   // Проверяем flag: если он всё ещё 1, значит байт был нулём и заём продолжается
                   flag.CopyTo(tmp2);   // tmp2 = flag

                   tmp2.While(() =>
                   {
                       // Байт был нулём – устанавливаем его в 255
                       desc[i].Init(255);
                       // flag оставляем 1 (заём продолжается)
                       tmp2.Init(0);    // выход
                   });

                   // Очищаем tmp2 (уже 0) и выходим из внешнего цикла
                   tmp2.Init(0);
                   temp.Init(0);
               });
           }

           // (Опционально) Если после всех байтов flag всё ещё 1 – это переполнение вниз (число стало максимальным), можно игнорировать.
       }

       public void Print256(BFVar desc)
       {

           for (int i = Size - 1; i > -1; i--)
           {
               CellFunc.StringPrint(desc, "[");
               CellFunc.Print10(desc[i]);
               CellFunc.StringPrint(desc, "], ");
           }

       }


       public BFVar multiplication(BFVar desc, BFVar factor)
       {
           var uintType = new BFUIntType(desc.Size + factor.Size);

           using BFVar tmp = desc.env.Alloc(1);

           BFVar Out = desc.env.Alloc(desc.Size + factor.Size);

           using BFVar factor_ = desc.env.Alloc(factor.Size);
           factor.CopyTo(factor_);

           for (int i = 0; i < factor.Size; i++)
           {
               factor_[i].CopyTo(tmp[0]);
               tmp[0].While(() =>
               {
                   uintType.Plus(Out, desc, i);

                   tmp[0].Minus();
               });
           }

           return Out;
       }

       public void Print10(BFVar num)
       {
           int quantity_digits = (int)(num.Size * Math.Log10(256)) + 1;
           var uintType = new BFUIntType(2);

           using var num_ = num.env.Alloc(num.Size);
           using var carry = num.env.Alloc(1);
           using var temp = num.env.Alloc(2);
           using var tmp = num.env.Alloc(1);
           using var tmp2 = num.env.Alloc(1);
           using var digits = num.env.Alloc(quantity_digits);

           num.CopyTo(num_);
           carry.Init();


           for (int digit = 0; digit < quantity_digits; digit++)
           {
               for (int cell = num.Size - 1; cell >= 0; cell--)
               {
                   carry[0].MoveTo(temp[1]);
                   num_[cell].MoveTo(temp[0]);

                   carry.Init();
                   num_[cell].Init();

                   temp[0].While(() =>
                   {
                       num_[cell].Plus();
                       carry[0].Minus(9);
                       carry.CopyTo(tmp);
                       tmp[0].If(() =>
                       {
                           num_[cell].Minus();
                           carry[0].Plus(10);
                       });

                       temp[0].Minus();
                   });

                   temp[1].While(() =>
                   {
                       temp[0].Init(255);

                       temp[0].While(() =>
                       {
                           num_[cell].Plus();
                           carry[0].Minus(9);
                           carry.CopyTo(tmp);
                           tmp[0].If(() =>
                           {
                               num_[cell].Minus();
                               carry[0].Plus(10);
                           });

                           temp[0].Minus();
                       });

                       num_[cell].Plus();
                       carry[0].Minus(9);
                       carry.CopyTo(tmp);
                       tmp[0].If(() =>
                       {
                           num_[cell].Minus();
                           carry[0].Plus(10);
                       });


                       temp[1].Minus();
                   });

               }
               carry[0].MoveTo(digits[digit]);
           }

           for (int digit = quantity_digits - 1; digit >= 0; digit--)
           {
               digits[digit].Plus(48);
               digits[digit].Print();
           }

       }



   }



   static public class CellFunc // функции ячеек
   {
       static public void StringPrint(BFVar desc, string s)
       {
           int c = 0;
           using var C = desc.env.Alloc(1);
           C.Init();
           for (int i = 0; i < s.Length; i++)
           {

               c = s[i] - c;
               if (c > 128) c -= 256;
               else if (c < -127) c += 256;

               C[0].Change(c);
               C[0].Print();
               c = s[i];
           }
       }


       static public void Print10(BFCell num) // в десятичной системе исчесления
       {

           using var num_ = num.env.Alloc(1);
           num.CopyTo(num_[0]);

           using var numbers = num.env.Alloc(3);

           var tmp = num.env.Alloc(1);
           var flag = num.env.Alloc(1);

           var q = num.env.Alloc(1); // частное
           var r = num.env.Alloc(1); // остаток

           for (int i = 0; i < 3; i++)
           {

               q[0].Init(0);
               r[0].Init(0);

               num_[0].While(() =>
               {
                   num_[0].Minus(1);

                   r[0].Plus(1);

                   r[0].Minus(10);
                   r[0].CopyTo(tmp[0]);

                   flag[0].Init(1);
                   tmp[0].If(() =>
                   {
                       flag[0].Init(0);
                       r[0].Plus(10);
                   });

                   flag[0].If(() =>
                   {
                       q[0].Plus(1);
                   });


               });


               r[0].CopyTo(numbers[i]);
               q[0].CopyTo(num_[0]);
           }

           numbers[2].Plus(48);
           numbers[2].Print();
           numbers[1].Plus(48);
           numbers[1].Print();
           numbers[0].Plus(48);
           numbers[0].Print();
       }
   }*/

}