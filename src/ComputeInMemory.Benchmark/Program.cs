using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using ComputeInMemory.Benchmark;

Console.WriteLine("============================================");
Console.WriteLine("  存算一体架构 性能基准测试");
Console.WriteLine("============================================");
Console.WriteLine();

if (args.Length > 0)
{
    BenchmarkSwitcher.FromAssembly(typeof(StorageMicroBenchmarks).Assembly).Run(args);
    return;
}

Console.WriteLine("可用测试套件:");
Console.WriteLine("  1 - 存储管理层 (StorageMicroBenchmarks)");
Console.WriteLine("  2 - 数据通路优化层 (DataPathMicroBenchmarks)");
Console.WriteLine("  3 - 任务调度层 (SchedulerMicroBenchmarks)");
Console.WriteLine("  a - 全部测试 (默认)");
Console.WriteLine("  q - 快速验证模式 (1次预热 + 3次迭代)");
Console.WriteLine();
Console.Write("请选择 [a]: ");
var input = Console.ReadLine()?.Trim().ToLowerInvariant();

IConfig? config = null;

if (input == "q")
{
    Console.WriteLine();
    Console.WriteLine(">> 快速验证模式: 每个测试仅1次预热 + 3次迭代");
    Console.WriteLine();
    config = ManualConfig.Create(DefaultConfig.Instance)
        .AddJob(Job.ShortRun
            .WithWarmupCount(1)
            .WithIterationCount(3));
}

switch (input)
{
    case "1":
        Console.WriteLine(">> 运行: 存储管理层基准测试");
        Console.WriteLine();
        BenchmarkRunner.Run<StorageMicroBenchmarks>(config);
        break;
    case "2":
        Console.WriteLine(">> 运行: 数据通路优化层基准测试");
        Console.WriteLine();
        BenchmarkRunner.Run<DataPathMicroBenchmarks>(config);
        break;
    case "3":
        Console.WriteLine(">> 运行: 任务调度层基准测试");
        Console.WriteLine();
        BenchmarkRunner.Run<SchedulerMicroBenchmarks>(config);
        break;
    default:
        Console.WriteLine(">> 运行: 全部基准测试");
        Console.WriteLine();
        Console.WriteLine("[1/3] 存储管理层基准测试...");
        BenchmarkRunner.Run<StorageMicroBenchmarks>(config);

        Console.WriteLine();
        Console.WriteLine("[2/3] 数据通路优化层基准测试...");
        BenchmarkRunner.Run<DataPathMicroBenchmarks>(config);

        Console.WriteLine();
        Console.WriteLine("[3/3] 任务调度层基准测试...");
        BenchmarkRunner.Run<SchedulerMicroBenchmarks>(config);
        break;
}

Console.WriteLine();
Console.WriteLine("============================================");
Console.WriteLine("  基准测试完成");
Console.WriteLine("============================================");
