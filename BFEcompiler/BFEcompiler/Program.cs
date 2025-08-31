
string input_file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\test_code.bfe"));
string output_file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\test_code.bf"));
string bf = BFEcompiler.BFEcompiler.Process(File.ReadAllText(input_file));
Console.WriteLine(bf);
File.WriteAllText(output_file, bf);