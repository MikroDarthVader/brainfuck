using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace BFEcompiler
{
    static class BFEcompiler
    {
        const string special_chars = " [](){}.,<>-+:";       // : for array

        public static string Process(string bfe)
        {
            var str = Process_Inc(bfe);
            str = Clear_junk(str);
            str = Process_procs_vars(str).Replace(" ", "");
            return str;
        }

        private static bool IsVisible(char c)
        {
            return !char.IsWhiteSpace(c) && !char.IsControl(c);
        }

        private static string Clear_junk(string code)
        {
            StringBuilder result = new StringBuilder();
            bool inComment = false;

            for (int i = 0; i < code.Length; i++)
            {
                char p;
                if (i >= code.Length - 12)
                    p = code[i];

                if (code[i] == '#')
                    inComment = true;
                else if (code[i] == '\n')
                    inComment = false;
                if (!inComment)
                {
                    if (IsVisible(code[i]))
                    {

                        if (!char.IsLetterOrDigit(code[i]) && !(code[i] == '_') && !special_chars.Contains(code[i]))
                            throw new FormatException("Invalid symbol: " + code[i]);
                        if (i > 0 && char.IsDigit(code[i]) && special_chars.Contains(code[i - 1]) && code[i - 1] != ':')
                            throw new FormatException("Unexpected digit: " + code[i]);
                        result.Append(code[i]);
                    }
                    else if (i >= 1 && i < code.Length - 1 && IsVisible(code[i - 1]) && !special_chars.Contains(code[i - 1])) //end of var or proc
                    {
                        for (; i < code.Length && !IsVisible(code[i]); i++) ;//find next code
                        if (i == code.Length)
                            continue; //end of code
                        if (!special_chars.Contains(code[i]))//next code is var or proc too
                            result.Append(' ');
                        result.Append(code[i]);
                    }
                }
            }

            return result.ToString();
        }

        private static string Process_Inc(string code)
        {
            var result = new StringBuilder();
            int index = 0;
            bool inComment = false;

            while (index < code.Length)
            {
                if (code[index] == '#') //check for comments
                    inComment = true;
                else if (code[index] == '\n')
                    inComment = false;

                if (!inComment && code[index] == '\"')
                {
                    int tmp_cursor = index + 1;
                    for (; tmp_cursor < code.Length - 1 && code[tmp_cursor] != '\"'; tmp_cursor++) ;

                    if (code[tmp_cursor] != '\"')
                        throw new FormatException("not a complete interpretation of the lines " + code.Substring(index, tmp_cursor - index + 1));

                    string str = code.Substring(index + 1, tmp_cursor - index - 1);

                    index = tmp_cursor + 1;

                    StringBuilder res = new StringBuilder();
                    foreach (char c in str)
                    {
                        res.Append('+', c);
                        res.Append('>');
                    }
                    res.Append('<', str.Length);
                    result.Append(res);
                    continue;
                }

                

                if (inComment || (code[index] != '+' && code[index] != '-'))
                {
                    if (!inComment)
                        result.Append(code[index]);
                    else result.Append(' ');
                    index++;
                    continue;
                }

                if (index + 1 >= code.Length)
                    throw new FormatException("Invalid format after sign");

                char sign = code[index];
                if (char.IsDigit(code[index + 1]))
                {
                    int numEnd = index + 1;
                    while (numEnd < code.Length && char.IsDigit(code[numEnd]))
                        numEnd++;

                    string numberStr = code.Substring(index + 1, numEnd - index - 1);
                    if (!int.TryParse(numberStr, out int number) || number > 255)
                        throw new FormatException("Invalid number or number too large");

                    result.Append(sign, number);
                    index = numEnd;
                }
                else if (code[index + 1] == '*')
                {
                    if (index + 2 >= code.Length)
                        throw new FormatException("Missing character after asterisk");

                    char symbol = code[index + 2];
                    result.Append(sign, (int)symbol);
                    index += 3;
                }
                else
                {
                    throw new FormatException("Unexpected character after sign");
                }
            }

            return result.ToString();
        }


        private class Procedure
        {
            List<string> operands;
            string name;
            string code;

            public Procedure(string code)
            {
                bool reading_name = true;
                operands = new List<string>();
                foreach (string s in ParceHead(code))
                {
                    if (reading_name)
                        name = s;
                    else if (s.Length > 0)
                        operands.Add(s);
                    reading_name = false;
                }

                if (code[ParceHeadLen] != '{')
                    throw new FormatException("Error in procedure declaration body. " +
                                                "Opening parenthesis missing \"{\". " +
                                                "Procedure \"" + name + "\"");
                ParceHeadLen++;
                if (code[code.Length - 1] != '}')
                    throw new FormatException("Error in procedure declaration body. " +
                                                "Closing parenthesis missing \"}\". " +
                                                "Procedure \"" + name + "\"");

                this.code = code.Substring(ParceHeadLen, code.Length - ParceHeadLen - 1);

                RenameLocalVars();
            }

            private int ParceHeadLen;//не всеми своими решениями я горжусь
            private IEnumerable<string> ParceHead(string code)
            {
                int cursor = 0;
                int tmp_cursor = 0;

                for (tmp_cursor = cursor; !special_chars.Contains(code[tmp_cursor]); tmp_cursor++) ;//getting proc name
                yield return code.Substring(cursor, tmp_cursor - cursor);

                if (code[tmp_cursor] != '(')
                    throw new FormatException();

                cursor = tmp_cursor + 1;
                tmp_cursor = cursor;

                for (; code[tmp_cursor] != ')'; cursor = tmp_cursor + 1)
                {
                    for (tmp_cursor = cursor; !special_chars.Contains(code[tmp_cursor]); tmp_cursor++) ;//getting operand name
                    if (code[tmp_cursor] != ')' && code[tmp_cursor] != ' ')
                        throw new FormatException("Error in procedure declaration operands. Procedure \"" + name + "\"");

                    yield return code.Substring(cursor, tmp_cursor - cursor);
                }
                ParceHeadLen = tmp_cursor + 1;
                yield return "";
            }

            private string RenameVar(string var_name)
            {
                return "v" + ((name + var_name).GetHashCode() & 0x7FFFFFFF).ToString(); //positive hash
            }

            private void RenameLocalVars()
            {
                int cursor = 0;
                int tmp_cursor = 0;

                StringBuilder sb = new StringBuilder();

                while (tmp_cursor < code.Length)
                {
                    for (cursor = tmp_cursor; cursor < code.Length && special_chars.Contains(code[cursor]); cursor++)//skip not name
                        sb.Append(code[cursor]);

                    for (tmp_cursor = cursor; tmp_cursor < code.Length && !special_chars.Contains(code[tmp_cursor]); tmp_cursor++) ;

                    if (tmp_cursor - cursor == 0)//on code end
                        continue;

                    var _name = code.Substring(cursor, tmp_cursor - cursor);
                    if ((tmp_cursor < code.Length && code[tmp_cursor] == '(') || operands.Contains(_name))//if procedure or operand name
                        sb.Append(_name);
                    else
                    {
                        var global_name = RenameVar(_name);
                        sb.Append(global_name);
                    }
                }
                code = sb.ToString();
            }

            public string Insert(string call)
            {
                Dictionary<string, string> operands = new Dictionary<string, string>();
                int i = 0;
                foreach (string s in ParceHead(call))
                {
                    if (i == 0)//call of other proc
                    { if (s != name) return ""; }
                    else if (s.Length > 0)//operands
                        operands.Add(this.operands[i - 1], s);
                    i++;
                }

                if (operands.Count != this.operands.Count)
                    throw new FormatException("arguments quantity mismatch in call of procedure: " + name);

                int cursor = 0;
                int tmp_cursor = 0;
                var res = new StringBuilder();
                while (tmp_cursor < code.Length) //Да, это реюз кода из RenameLocalVars. Ну и что? я же говорил, что я не всеми своими решениями горжусь. Главное, что работает! (но лучше так, конечно, не делать)
                {
                    for (cursor = tmp_cursor; cursor < code.Length && special_chars.Contains(code[cursor]); cursor++)//skip not name
                        res.Append(code[cursor]);

                    for (tmp_cursor = cursor; tmp_cursor < code.Length && !special_chars.Contains(code[tmp_cursor]); tmp_cursor++) ;

                    if (tmp_cursor - cursor == 0)//on code end
                        continue;

                    var _name = code.Substring(cursor, tmp_cursor - cursor);
                    if (operands.ContainsKey(_name))//if operand name
                        res.Append(operands[_name]);
                    else
                        res.Append(_name);
                }

                return res.ToString();
            }
        }

        public static string Process_procs_vars(string code)
        {


            List<string> vars = new List<string>();
            List<Procedure> procs = new List<Procedure>();
            int tmp_cursor;
            int cursor;

            for (cursor = code.Length - 1; cursor > -1; cursor--) // сохранение кода процедур в procs и удаление его из общего кода
            {
                if (code[cursor] == '}')
                {

                    tmp_cursor = cursor;
                    cursor--;
                    for (; code[cursor] != '{'; cursor--)
                        if (code[cursor] == '}')
                            throw new FormatException("Creation procedure in procedure");
                    for (; code[cursor] != '('; cursor--) ;
                    for (; !special_chars.Contains(code[cursor - 1]) & cursor > 1; cursor--) ;
                    if (cursor == 1 & !special_chars.Contains(code[cursor - 1])) cursor--;
                    procs.Add(new Procedure(code.Substring(cursor, tmp_cursor - cursor + 1)));

                    code = code.Remove(cursor, tmp_cursor - cursor + 1);

                }
            }


            string answer;                                          // замена вызова процедуры на процедуру
            string call_procedures;
            for (cursor = code.Length - 1; cursor > -1; cursor--)
            {

                if (code[cursor] == ')')
                {
                    tmp_cursor = cursor;

                    for (; code[cursor] != '('; cursor--) ;
                    for (; !special_chars.Contains(code[cursor - 1]) && cursor > 1; cursor--) ;
                    if (cursor == 1 && !special_chars.Contains(code[cursor - 1])) cursor--;

                    call_procedures = code.Substring(cursor, tmp_cursor - cursor + 1);
                    for (int proc_i = 0; proc_i < procs.Count; proc_i++)
                    {
                        answer = procs[proc_i].Insert(call_procedures);
                        if (answer != "")
                        {

                            code = code.Remove(cursor, tmp_cursor - cursor + 1).Insert(cursor, answer);
                            cursor += answer.Length;
                            tmp_cursor = cursor;
                        }
                    }
                }
            }

            cursor = 0;
            tmp_cursor = 0;
            int position = 0; // на какой мы сейчас переменной если смотреть по индексу в списке variable


            List<string> variables = new List<string>();
            List<int> variables_index = new List<int>();

            StringBuilder sb = new StringBuilder();

            while (tmp_cursor < code.Length)
            {
                for (cursor = tmp_cursor; cursor < code.Length && special_chars.Contains(code[cursor]) || cursor < code.Length && char.IsDigit(code[cursor]); cursor++)//skip not name
                {
                    if (code[cursor] == '<') position -= 1;
                    else if (code[cursor] == '>') position += 1;
                    sb.Append(code[cursor]);
                }


                for (tmp_cursor = cursor; tmp_cursor < code.Length && !special_chars.Contains(code[tmp_cursor]); tmp_cursor++) ;

                if (tmp_cursor - cursor == 0)//on code end
                    continue;


                var name_var = code.Substring(cursor, tmp_cursor - cursor);
                var size_var = 1;



                if (!variables.Contains(name_var))
                {

                    if (tmp_cursor < code.Length && code[tmp_cursor] == ':')
                    {

                        if (!char.IsDigit(code[tmp_cursor + 1]))
                            throw new FormatException("array size not specified " + name_var);
                        int num_cursor = tmp_cursor + 1;
                        for (; num_cursor < code.Length && char.IsDigit(code[num_cursor]); num_cursor++) ;
                        size_var += int.Parse(code.Substring(tmp_cursor + 1, num_cursor - tmp_cursor - 1)) - 1;
                        tmp_cursor = num_cursor;
                    }
                    if (size_var == 0)
                        throw new FormatException("zero length array " + name_var);
                    variables_index.Add(size_var);
                    variables.Add(name_var);
                }

                StringBuilder address_var = new StringBuilder();

                int var_position = variables_index.Take(variables.IndexOf(name_var)).Sum();

                int distance = position - var_position < 0 ? -(position - var_position) : position - var_position; // модуль числа
                char side;                                                                                                   // куда двигатся для того чтоб прийти к перменной
                if (position - var_position < 0)
                    side = '>';
                else
                    side = '<';
                for (int i = 0; i < distance; i++)
                    address_var.Append(side);

                position = var_position;

                sb.Append(address_var);
            }
            code = sb.ToString();

            StringBuilder result = new StringBuilder();
            return code;
        }
    }
}