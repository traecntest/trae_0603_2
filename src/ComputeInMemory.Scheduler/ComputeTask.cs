namespace ComputeInMemory.Scheduler;

public enum HardwareType
{
    CPU,
    GPU,
    FPGA,
    PIM
}

public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum TaskState
{
    Pending,
    Scheduled,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public sealed class HardwareResource
{
    public string Id { get; init; } = string.Empty;
    public HardwareType Type { get; init; }
    public int CoreCount { get; init; }
    public long MemoryBytes { get; init; }
    public double ComputeCapacity { get; init; }
    public double EnergyEfficiencyRatio { get; init; }
    public double CurrentLoad { get; set; }
    public bool IsAvailable { get; set; } = true;
    public HashSet<string> SupportedInstructionSets { get; init; } = new();
}

public sealed class ComputeTask
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public TaskPriority Priority { get; init; } = TaskPriority.Normal;
    public TaskState State { get; set; } = TaskState.Pending;
    public HardwareType PreferredHardware { get; init; } = HardwareType.CPU;
    public HashSet<string> Dependencies { get; init; } = new();
    public long EstimatedMemoryBytes { get; init; }
    public double EstimatedDurationMs { get; init; }
    public double ActualDurationMs { get; set; }
    public int MaxRetryCount { get; init; } = 3;
    public int RetryCount { get; set; }
    public Exception? LastError { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? AssignedResourceId { get; set; }

    internal Func<CancellationToken, Task> ExecutionFunc { get; set; } = _ => Task.CompletedTask;

    public static ComputeTask Create(string name, Func<CancellationToken, Task> execute,
        TaskPriority priority = TaskPriority.Normal,
        HardwareType preferredHardware = HardwareType.CPU,
        long estimatedMemoryBytes = 0,
        double estimatedDurationMs = 1000)
    {
        return new ComputeTask
        {
            Name = name,
            Priority = priority,
            PreferredHardware = preferredHardware,
            EstimatedMemoryBytes = estimatedMemoryBytes,
            EstimatedDurationMs = estimatedDurationMs,
            ExecutionFunc = execute
        };
    }

    public static ComputeTask Create(Action execute,
        string name = "",
        TaskPriority priority = TaskPriority.Normal,
        HardwareType preferredHardware = HardwareType.CPU,
        long estimatedMemoryBytes = 0,
        double estimatedDurationMs = 1000)
    {
        return new ComputeTask
        {
            Name = name,
            Priority = priority,
            PreferredHardware = preferredHardware,
            EstimatedMemoryBytes = estimatedMemoryBytes,
            EstimatedDurationMs = estimatedDurationMs,
            ExecutionFunc = _ =>
            {
                execute();
                return Task.CompletedTask;
            }
        };
    }
}
