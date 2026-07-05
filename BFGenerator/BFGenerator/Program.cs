
using BFG;

internal class Program
{
    static void Main(/*string[] args*/)
    {
        var floatType = new BFFloatType(expoSize: 2, mantSize: 2);
        Console.WriteLine($"Float size: {floatType.Size} cells");
    }
}
