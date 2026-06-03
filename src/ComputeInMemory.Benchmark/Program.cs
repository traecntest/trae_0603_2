using BenchmarkDotNet.Running;
using ComputeInMemory.Benchmark;

if (args.Length == 0)
{
    BenchmarkRunner.Run<StorageMicroBenchmarks>();
    BenchmarkRunner.Run<DataPathMicroBenchmarks>();
    BenchmarkRunner.Run<SchedulerMicroBenchmarks>();
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(StorageMicroBenchmarks).Assembly).Run(args);
}
