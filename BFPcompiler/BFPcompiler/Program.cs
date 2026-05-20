// See https://aka.ms/new-console-template for more information
var bfp = new BFP_();
var bf = bfp.Process();
Console.WriteLine(bf);
string output_file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\test_code.bf"));
File.WriteAllText(output_file, bf);