using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<StepBenchmarks>();

/// <summary>Placeholder. The measured step lands after the naive engine exists.</summary>
[MemoryDiagnoser]
public class StepBenchmarks
{
    [Benchmark]
    public void NaiveStep()
    {
    }
}
