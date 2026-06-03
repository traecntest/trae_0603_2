namespace ComputeInMemory.Benchmark;

public sealed class PerformanceMetrics
{
    public double DataMovementReductionRate { get; set; }
    public double ComputeEnergyEfficiencyRatio { get; set; }
    public double TaskSchedulingLatencyMs { get; set; }
    public double HardwareUtilizationRate { get; set; }
    public double ThroughputOpsPerSecond { get; set; }
    public double AverageLatencyMs { get; set; }
    public long MemoryAllocatedBytes { get; set; }
    public long MemorySavedBytes { get; set; }
    public int GcGen0Collections { get; set; }
    public int GcGen1Collections { get; set; }
    public int GcGen2Collections { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static PerformanceMetrics Calculate(PerformanceSnapshot start, PerformanceSnapshot end)
    {
        var elapsed = end.Timestamp - start.Timestamp;
        var elapsedMs = elapsed.TotalMilliseconds;

        return new PerformanceMetrics
        {
            DataMovementReductionRate = start.BytesMoved > 0
                ? 1.0 - ((double)end.BytesMoved / start.BytesMoved)
                : 0,
            ComputeEnergyEfficiencyRatio = end.TasksCompleted > 0
                ? end.TasksCompleted / (end.EnergyConsumedJoules > 0 ? end.EnergyConsumedJoules : 1.0)
                : 0,
            TaskSchedulingLatencyMs = end.TasksScheduled > 0
                ? end.TotalSchedulingDelayMs / end.TasksScheduled
                : 0,
            HardwareUtilizationRate = end.TotalComputeTimeMs / elapsedMs,
            ThroughputOpsPerSecond = elapsedMs > 0
                ? end.TasksCompleted / (elapsedMs / 1000.0)
                : 0,
            AverageLatencyMs = end.TasksCompleted > 0
                ? end.TotalExecutionTimeMs / end.TasksCompleted
                : 0,
            MemoryAllocatedBytes = end.TotalMemoryAllocated - start.TotalMemoryAllocated,
            MemorySavedBytes = start.TotalMemoryAllocated - end.TotalMemoryAllocated,
            GcGen0Collections = end.Gen0Collections - start.Gen0Collections,
            GcGen1Collections = end.Gen1Collections - start.Gen1Collections,
            GcGen2Collections = end.Gen2Collections - start.Gen2Collections,
            P95LatencyMs = end.P95LatencyMs,
            P99LatencyMs = end.P99LatencyMs,
            Timestamp = end.Timestamp
        };
    }

    public override string ToString() =>
        $"DataMovementReduction={DataMovementReductionRate:P1}, " +
        $"EnergyEfficiency={ComputeEnergyEfficiencyRatio:F2} tasks/J, " +
        $"SchedulingLatency={TaskSchedulingLatencyMs:F2}ms, " +
        $"HWUtilization={HardwareUtilizationRate:P1}, " +
        $"Throughput={ThroughputOpsPerSecond:F0} ops/s, " +
        $"AvgLatency={AverageLatencyMs:F2}ms, " +
        $"P95={P95LatencyMs:F2}ms, P99={P99LatencyMs:F2}ms, " +
        $"Allocated={MemoryAllocatedBytes / 1024.0:F1}KB, " +
        $"GC=[{GcGen0Collections}/{GcGen1Collections}/{GcGen2Collections}]";
}

public sealed class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long BytesMoved { get; set; }
    public long TasksCompleted { get; set; }
    public long TasksScheduled { get; set; }
    public double TotalSchedulingDelayMs { get; set; }
    public double TotalExecutionTimeMs { get; set; }
    public double TotalComputeTimeMs { get; set; }
    public double EnergyConsumedJoules { get; set; }
    public long TotalMemoryAllocated { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }

    public static PerformanceSnapshot Capture()
    {
        return new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            TotalMemoryAllocated = GC.GetTotalAllocatedBytes(true),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }
}

public sealed class PerformanceMonitor : IDisposable
{
    private readonly Timer _monitorTimer;
    private readonly List<PerformanceSnapshot> _snapshots = new();
    private readonly object _lock = new();
    private bool _disposed;

    private long _bytesMoved;
    private long _tasksCompleted;
    private long _tasksScheduled;
    private double _totalSchedulingDelayMs;
    private double _totalExecutionTimeMs;
    private double _totalComputeTimeMs;
    private double _energyConsumedJoules;
    private readonly List<double> _latencies = new();

    public long BytesMoved => _bytesMoved;
    public long TasksCompleted => _tasksCompleted;
    public long TasksScheduled => _tasksScheduled;
    public double TotalSchedulingDelayMs => _totalSchedulingDelayMs;
    public double TotalExecutionTimeMs => _totalExecutionTimeMs;
    public double TotalComputeTimeMs => _totalComputeTimeMs;
    public double EnergyConsumedJoules => _energyConsumedJoules;

    public PerformanceMonitor(TimeSpan? snapshotInterval = null)
    {
        var interval = snapshotInterval ?? TimeSpan.FromSeconds(1);
        _monitorTimer = new Timer(TakeSnapshot, null, interval, interval);
    }

    public void RecordDataMovement(long bytes) => Interlocked.Add(ref _bytesMoved, bytes);

    public void RecordTaskCompletion(double executionTimeMs, double schedulingDelayMs = 0)
    {
        Interlocked.Increment(ref _tasksCompleted);
        lock (_latencies)
        {
            _latencies.Add(executionTimeMs);
        }

        lock (_lock)
        {
            _totalExecutionTimeMs += executionTimeMs;
            _totalSchedulingDelayMs += schedulingDelayMs;
        }
    }

    public void RecordTaskScheduled() => Interlocked.Increment(ref _tasksScheduled);

    public PerformanceMetrics GetMetrics()
    {
        lock (_lock)
        {
            var current = CaptureSnapshot();
            var first = _snapshots.FirstOrDefault() ?? current;
            return PerformanceMetrics.Calculate(first, current);
        }
    }

    public PerformanceSnapshot CaptureSnapshot()
    {
        double p95 = 0, p99 = 0;
        lock (_latencies)
        {
            if (_latencies.Count > 0)
            {
                var sorted = _latencies.OrderBy(x => x).ToList();
                p95 = sorted[(int)(sorted.Count * 0.95)];
                p99 = sorted[(int)(sorted.Count * 0.99)];
            }
        }

        return new PerformanceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            BytesMoved = BytesMoved,
            TasksCompleted = TasksCompleted,
            TasksScheduled = TasksScheduled,
            TotalSchedulingDelayMs = TotalSchedulingDelayMs,
            TotalExecutionTimeMs = TotalExecutionTimeMs,
            TotalComputeTimeMs = TotalComputeTimeMs,
            EnergyConsumedJoules = EnergyConsumedJoules,
            TotalMemoryAllocated = GC.GetTotalAllocatedBytes(true),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            P95LatencyMs = p95,
            P99LatencyMs = p99
        };
    }

    private void TakeSnapshot(object? state)
    {
        if (_disposed) return;
        lock (_lock)
        {
            _snapshots.Add(CaptureSnapshot());
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitorTimer.Dispose();
    }
}
