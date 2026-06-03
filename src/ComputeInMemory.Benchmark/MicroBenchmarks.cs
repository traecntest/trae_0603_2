using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using ComputeInMemory.Storage;
using ComputeInMemory.Scheduler;
using ComputeInMemory.DataPath;

namespace ComputeInMemory.Benchmark;

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[RankColumn]
public class StorageMicroBenchmarks
{
    private class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.ShortRun
                .WithWarmupCount(3)
                .WithIterationCount(10));
            AddColumn(StatisticColumn.P95);
        }
    }

    private StoragePool _pool = null!;
    private ZeroCopyAccessor _accessor = null!;
    private byte[] _sourceData = null!;
    private StorageBlock _sourceBlock = null!;
    private StorageBlock _destBlock = null!;

    [Params(1024, 64 * 1024, 1024 * 1024)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _pool = new StoragePool();
        _accessor = new ZeroCopyAccessor();
        _sourceData = new byte[DataSize];
        Random.Shared.NextBytes(_sourceData);
        _sourceBlock = _pool.Allocate("source", DataSize);
        _destBlock = _pool.Allocate("dest", DataSize);
        _sourceData.CopyTo(_sourceBlock.GetSpan());
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _accessor.Dispose();
        _pool.Dispose();
    }

    [Benchmark(Description = "StorageBlock zero-copy span access")]
    public Span<byte> ZeroCopySpanAccess()
    {
        return _sourceBlock.GetSpan();
    }

    [Benchmark(Description = "ZeroCopyAccessor copy between blocks")]
    public Memory<byte> CopyBetweenBlocks()
    {
        return _accessor.CopyBetweenBlocks(_sourceBlock, _destBlock);
    }

    [Benchmark(Description = "Allocate storage block")]
    public StorageBlock AllocateBlock()
    {
        var block = _pool.Allocate(Guid.NewGuid().ToString(), DataSize);
        _pool.Release(block.Key);
        return block;
    }

    [Benchmark(Description = "ZeroCopyAccessor reinterpret cast")]
    public Memory<int> ReinterpretCast()
    {
        return _accessor.ReinterpretCast<int>(_sourceBlock.GetMemory());
    }

    [Benchmark(Description = "ZeroCopyAccessor pin and get memory")]
    public Memory<byte> PinAndGetMemory()
    {
        return _accessor.PinAndGetMemory("bench_key", _sourceData);
    }
}

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class DataPathMicroBenchmarks
{
    private class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.ShortRun
                .WithWarmupCount(3)
                .WithIterationCount(10));
            AddColumn(StatisticColumn.P95);
        }
    }

    private SharedMemoryRegion _sharedMem = null!;
    private NearComputeChannel _pimChannel = null!;
    private IntPtr _srcA;
    private IntPtr _srcB;
    private IntPtr _dst;
    private byte[] _managedData = null!;

    [Params(1024, 64 * 1024)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _sharedMem = new SharedMemoryRegion("bench", DataSize * 4);
        _pimChannel = new NearComputeChannel();
        _srcA = NativeMemoryBridge.Allocate(DataSize);
        _srcB = NativeMemoryBridge.Allocate(DataSize);
        _dst = NativeMemoryBridge.Allocate(DataSize);
        _managedData = new byte[DataSize];
        Random.Shared.NextBytes(_managedData);

        NativeMemoryBridge.CopyFromManaged(_managedData, _srcA, DataSize);
        NativeMemoryBridge.CopyFromManaged(_managedData, _srcB, DataSize);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        NativeMemoryBridge.Free(_srcA);
        NativeMemoryBridge.Free(_srcB);
        NativeMemoryBridge.Free(_dst);
        _sharedMem.Dispose();
        _pimChannel.Dispose();
    }

    [Benchmark(Description = "NativeMemoryBridge copy managed to native")]
    public void CopyManagedToNative()
    {
        NativeMemoryBridge.CopyFromManaged(_managedData, _srcA, DataSize);
    }

    [Benchmark(Description = "NativeMemoryBridge copy native to managed")]
    public void CopyNativeToManaged()
    {
        NativeMemoryBridge.CopyToManaged(_srcA, _managedData, DataSize);
    }

    [Benchmark(Description = "PIM simulate vector add")]
    public void PimVectorAdd()
    {
        _pimChannel.SimulateVectorAdd(_srcA, _srcB, _dst, DataSize);
    }

    [Benchmark(Description = "SharedMemory write")]
    public void SharedMemoryWrite()
    {
        _sharedMem.Write(_managedData);
    }

    [Benchmark(Description = "SharedMemory read")]
    public void SharedMemoryRead()
    {
        _sharedMem.Read(_managedData);
    }
}

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class SchedulerMicroBenchmarks
{
    private class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.ShortRun
                .WithWarmupCount(2)
                .WithIterationCount(5));
        }
    }

    [Benchmark(Description = "DAG topological sort 100 tasks")]
    public List<ComputeTask> TopologicalSort100()
    {
        var dag = CreateLinearDag(100);
        return dag.TopologicalSort();
    }

    [Benchmark(Description = "DAG cycle detection 100 tasks")]
    public bool CycleDetection100()
    {
        var dag = CreateLinearDag(100);
        return dag.HasCycle();
    }

    [Benchmark(Description = "DAG get ready tasks 100 tasks")]
    public List<ComputeTask> GetReadyTasks100()
    {
        var dag = CreateLinearDag(100);
        return dag.GetReadyTasks();
    }

    private static TaskDag CreateLinearDag(int taskCount)
    {
        var dag = new TaskDag();
        ComputeTask? previous = null;

        for (var i = 0; i < taskCount; i++)
        {
            var task = ComputeTask.Create(() => { }, name: $"task_{i}");
            if (previous != null)
            {
                task.Dependencies.Add(previous.Id.ToString());
            }

            dag.AddTask(task);
            previous = task;
        }

        return dag;
    }
}
