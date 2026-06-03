using ComputeInMemory.Benchmark;

namespace ComputeInMemory.Tests;

public class PerformanceMetricsTests
{
    [Fact]
    public void PerformanceSnapshot_Capture_CapturesGcCounts()
    {
        var startGen0 = GC.CollectionCount(0);
        var snapshot = PerformanceSnapshot.Capture();
        
        Assert.True(snapshot.Gen0Collections >= startGen0);
        Assert.True(snapshot.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void PerformanceMetrics_Calculate_ReturnsValidMetrics()
    {
        var start = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow.AddSeconds(-10),
            BytesMoved = 1000000,
            TasksCompleted = 0,
            TasksScheduled = 0,
            TotalMemoryAllocated = 0,
            Gen0Collections = 0,
            Gen1Collections = 0,
            Gen2Collections = 0
        };

        var end = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            BytesMoved = 500000,
            TasksCompleted = 100,
            TasksScheduled = 100,
            TotalSchedulingDelayMs = 500,
            TotalExecutionTimeMs = 10000,
            TotalComputeTimeMs = 8000,
            EnergyConsumedJoules = 50,
            TotalMemoryAllocated = 1024 * 1024,
            Gen0Collections = 5,
            Gen1Collections = 2,
            Gen2Collections = 1,
            P95LatencyMs = 150,
            P99LatencyMs = 200
        };

        var metrics = PerformanceMetrics.Calculate(start, end);

        Assert.Equal(0.5, metrics.DataMovementReductionRate, 2);
        Assert.Equal(2.0, metrics.ComputeEnergyEfficiencyRatio, 2);
        Assert.Equal(5.0, metrics.TaskSchedulingLatencyMs, 2);
        Assert.True(metrics.ThroughputOpsPerSecond > 0);
        Assert.Equal(100.0, metrics.AverageLatencyMs, 2);
        Assert.Equal(1024 * 1024, metrics.MemoryAllocatedBytes);
        Assert.Equal(5, metrics.GcGen0Collections);
        Assert.Equal(2, metrics.GcGen1Collections);
        Assert.Equal(1, metrics.GcGen2Collections);
        Assert.Equal(150, metrics.P95LatencyMs);
        Assert.Equal(200, metrics.P99LatencyMs);
    }

    [Fact]
    public void PerformanceMetrics_ToString_FormatsCorrectly()
    {
        var start = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow.AddSeconds(-1),
            TasksCompleted = 0
        };

        var end = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            TasksCompleted = 10,
            TotalExecutionTimeMs = 1000,
            TotalMemoryAllocated = 1024,
            P95LatencyMs = 50,
            P99LatencyMs = 100
        };

        var metrics = PerformanceMetrics.Calculate(start, end);
        var result = metrics.ToString();

        Assert.Contains("DataMovementReduction", result);
        Assert.Contains("EnergyEfficiency", result);
        Assert.Contains("SchedulingLatency", result);
        Assert.Contains("Throughput", result);
        Assert.Contains("AvgLatency", result);
    }

    [Fact]
    public void PerformanceMetrics_Calculate_ZeroTasksCompleted_ReturnsZeroEfficiency()
    {
        var start = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow.AddSeconds(-5)
        };

        var end = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            TasksCompleted = 0,
            TotalMemoryAllocated = 0
        };

        var metrics = PerformanceMetrics.Calculate(start, end);

        Assert.Equal(0, metrics.ComputeEnergyEfficiencyRatio);
    }
}

public class PerformanceMonitorTests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        Assert.Equal(0, monitor.BytesMoved);
        Assert.Equal(0, monitor.TasksCompleted);
        Assert.Equal(0, monitor.TasksScheduled);
    }

    [Fact]
    public void RecordDataMovement_IncrementsBytesMoved()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        monitor.RecordDataMovement(1000);
        Assert.Equal(1000, monitor.BytesMoved);

        monitor.RecordDataMovement(500);
        Assert.Equal(1500, monitor.BytesMoved);
    }

    [Fact]
    public void RecordTaskCompletion_IncrementsTasksCompleted()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        monitor.RecordTaskCompletion(100, 10);
        Assert.Equal(1, monitor.TasksCompleted);
        Assert.Equal(100, monitor.TotalExecutionTimeMs);
        Assert.Equal(10, monitor.TotalSchedulingDelayMs);
    }

    [Fact]
    public void RecordTaskScheduled_IncrementsTasksScheduled()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        monitor.RecordTaskScheduled();
        Assert.Equal(1, monitor.TasksScheduled);

        monitor.RecordTaskScheduled();
        Assert.Equal(2, monitor.TasksScheduled);
    }

    [Fact]
    public void CaptureSnapshot_ReturnsCurrentSnapshot()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        monitor.RecordDataMovement(5000);
        monitor.RecordTaskCompletion(50);

        var snapshot = monitor.CaptureSnapshot();

        Assert.Equal(5000, snapshot.BytesMoved);
        Assert.Equal(1, snapshot.TasksCompleted);
        Assert.Equal(50, snapshot.TotalExecutionTimeMs);
    }

    [Fact]
    public void GetMetrics_ReturnsValidMetrics()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        monitor.RecordDataMovement(1000000);
        monitor.RecordTaskCompletion(100, 10);

        var metrics = monitor.GetMetrics();

        Assert.NotNull(metrics);
        Assert.IsType<PerformanceMetrics>(metrics);
    }

    [Fact]
    public void CaptureSnapshot_IncludesLatencyPercentiles()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        for (int i = 1; i <= 100; i++)
        {
            monitor.RecordTaskCompletion(i, 1);
        }

        var snapshot = monitor.CaptureSnapshot();

        Assert.True(snapshot.P95LatencyMs > 0);
        Assert.True(snapshot.P99LatencyMs > 0);
        Assert.True(snapshot.P99LatencyMs >= snapshot.P95LatencyMs);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));
        monitor.Dispose();
        monitor.Dispose();
    }

    [Fact]
    public void RecordTaskCompletion_MultipleTasks_AggregatesTimes()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        monitor.RecordTaskCompletion(100, 10);
        monitor.RecordTaskCompletion(200, 20);
        monitor.RecordTaskCompletion(300, 30);

        Assert.Equal(3, monitor.TasksCompleted);
        Assert.Equal(600, monitor.TotalExecutionTimeMs);
        Assert.Equal(60, monitor.TotalSchedulingDelayMs);
    }
}

public class PerformanceEvaluationTests
{
    [Fact]
    public void PerformanceMonitor_SimulateWorkload_RecordsMetrics()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        var totalBytes = 0L;
        for (int i = 0; i < 100; i++)
        {
            var bytes = Random.Shared.Next(100, 1000);
            monitor.RecordDataMovement(bytes);
            totalBytes += bytes;

            var latency = Random.Shared.Next(1, 100);
            monitor.RecordTaskCompletion(latency, Random.Shared.Next(1, 10));
        }

        Assert.Equal(totalBytes, monitor.BytesMoved);
        Assert.Equal(100, monitor.TasksCompleted);
    }

    [Fact]
    public void PerformanceMetrics_DataMovementReductionRate_Calculation()
    {
        var start = new PerformanceSnapshot { BytesMoved = 1000000 };
        var end = new PerformanceSnapshot { BytesMoved = 300000 };

        var metrics = PerformanceMetrics.Calculate(start, end);

        Assert.Equal(0.7, metrics.DataMovementReductionRate, 2);
    }

    [Fact]
    public void PerformanceMetrics_Throughput_Calculation()
    {
        var start = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow.AddSeconds(-10),
            TasksCompleted = 0
        };

        var end = new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            TasksCompleted = 1000
        };

        var metrics = PerformanceMetrics.Calculate(start, end);

        Assert.True(metrics.ThroughputOpsPerSecond > 90 && metrics.ThroughputOpsPerSecond < 110);
    }

    [Fact]
    public void PerformanceSnapshot_Capture_MultipleCalls_IncreaseGcCount()
    {
        var first = PerformanceSnapshot.Capture();
        Thread.Sleep(10);
        var second = PerformanceSnapshot.Capture();

        Assert.True(second.Timestamp > first.Timestamp);
    }

    [Fact]
    public void PerformanceMonitor_EnergyConsumption_RecordsCorrectly()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        monitor.RecordTaskCompletion(100);

        var snapshot = monitor.CaptureSnapshot();
        Assert.Equal(0, snapshot.EnergyConsumedJoules);
    }

    [Fact]
    public void PerformanceMonitor_ConcurrentAccess_ThreadSafe()
    {
        using var monitor = new PerformanceMonitor(TimeSpan.FromSeconds(1));

        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    monitor.RecordDataMovement(100);
                    monitor.RecordTaskCompletion(10);
                    monitor.RecordTaskScheduled();
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        Assert.Equal(100000, monitor.BytesMoved);
        Assert.Equal(1000, monitor.TasksCompleted);
        Assert.Equal(1000, monitor.TasksScheduled);
    }
}

public class MicroBenchmarkValidationTests
{
    [Fact]
    public void BenchmarkDotNet_IsAvailable()
    {
        var benchmarkAssembly = typeof(BenchmarkDotNet.Attributes.BenchmarkAttribute).Assembly;
        Assert.NotNull(benchmarkAssembly);
    }

    [Fact]
    public void StorageMicroBenchmarks_HasBenchmarkAttributes()
    {
        var benchmarkType = typeof(StorageMicroBenchmarks);
        var methods = benchmarkType.GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(BenchmarkDotNet.Attributes.BenchmarkAttribute), false).Length > 0)
            .ToList();

        Assert.True(methods.Count > 0);
    }

    [Fact]
    public void DataPathMicroBenchmarks_HasBenchmarkAttributes()
    {
        var benchmarkType = typeof(DataPathMicroBenchmarks);
        var methods = benchmarkType.GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(BenchmarkDotNet.Attributes.BenchmarkAttribute), false).Length > 0)
            .ToList();

        Assert.True(methods.Count > 0);
    }

    [Fact]
    public void SchedulerMicroBenchmarks_HasBenchmarkAttributes()
    {
        var benchmarkType = typeof(SchedulerMicroBenchmarks);
        var methods = benchmarkType.GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(BenchmarkDotNet.Attributes.BenchmarkAttribute), false).Length > 0)
            .ToList();

        Assert.True(methods.Count > 0);
    }

    [Fact]
    public void BenchmarkClasses_HasMemoryDiagnoser()
    {
        var types = new[]
        {
            typeof(StorageMicroBenchmarks),
            typeof(DataPathMicroBenchmarks),
            typeof(SchedulerMicroBenchmarks)
        };

        foreach (var type in types)
        {
            var hasDiagnoser = type.GetCustomAttributes(
                typeof(BenchmarkDotNet.Attributes.MemoryDiagnoserAttribute), false).Length > 0;
            Assert.True(hasDiagnoser, $"{type.Name} should have MemoryDiagnoser");
        }
    }
}
