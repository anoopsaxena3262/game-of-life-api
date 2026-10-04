using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using GameOfLife.Domain;

BenchmarkRunner.Run<StepBenchmarks>();

/// <summary>One step of the string-based engine on square boards up to the 300x300 cell cap.</summary>
[MemoryDiagnoser]
public class StepBenchmarks
{
    private string _state = "";

    [Params(30, 100, 300)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // About a third of the cells alive, fixed seed so runs compare.
        var random = new Random(42);
        var sb = new StringBuilder(Size * Size);
        for (var i = 0; i < Size * Size; i++)
        {
            sb.Append(random.Next(3) == 0 ? '1' : '0');
        }

        _state = sb.ToString();
    }

    [Benchmark]
    public string NaiveStep() => LifeEngine.Step(_state, Size, Size);
}
