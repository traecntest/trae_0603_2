using System.Text.Json.Serialization;
using ComputeInMemory.Benchmark;
using ComputeInMemory.DataPath;
using ComputeInMemory.Scheduler;
using ComputeInMemory.Storage;
using ComputeInMemory.WebUI.Services;
using PimTaskScheduler = ComputeInMemory.Scheduler.TaskScheduler;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
});

builder.Services.AddSingleton<StoragePool>(_ => new StoragePool());
builder.Services.AddSingleton<ZeroCopyAccessor>();
builder.Services.AddSingleton<DataLifecycleManager>(sp =>
    new DataLifecycleManager(sp.GetRequiredService<StoragePool>()));
builder.Services.AddSingleton<HardwareResourceMonitor>(_ =>
{
    var monitor = new HardwareResourceMonitor();
    monitor.RegisterResource(new HardwareResource
    {
        Id = "cpu-main",
        Type = HardwareType.CPU,
        CoreCount = 8,
        MemoryBytes = 16L * 1024 * 1024 * 1024,
        ComputeCapacity = 100,
        EnergyEfficiencyRatio = 10,
        CurrentLoad = 0.0,
        IsAvailable = true
    });
    monitor.RegisterResource(new HardwareResource
    {
        Id = "gpu-main",
        Type = HardwareType.GPU,
        CoreCount = 1024,
        MemoryBytes = 8L * 1024 * 1024 * 1024,
        ComputeCapacity = 500,
        EnergyEfficiencyRatio = 5,
        CurrentLoad = 0.0,
        IsAvailable = true
    });
    monitor.RegisterResource(new HardwareResource
    {
        Id = "pim-main",
        Type = HardwareType.PIM,
        CoreCount = 256,
        MemoryBytes = 4L * 1024 * 1024 * 1024,
        ComputeCapacity = 300,
        EnergyEfficiencyRatio = 25,
        CurrentLoad = 0.0,
        IsAvailable = true
    });
    monitor.RegisterResource(new HardwareResource
    {
        Id = "fpga-main",
        Type = HardwareType.FPGA,
        CoreCount = 64,
        MemoryBytes = 2L * 1024 * 1024 * 1024,
        ComputeCapacity = 200,
        EnergyEfficiencyRatio = 15,
        CurrentLoad = 0.0,
        IsAvailable = true
    });
    return monitor;
});
builder.Services.AddSingleton<PimTaskScheduler>(sp =>
    new PimTaskScheduler(sp.GetRequiredService<HardwareResourceMonitor>()));
builder.Services.AddSingleton<SharedMemoryManager>();
builder.Services.AddSingleton<NearComputeChannel>();
builder.Services.AddSingleton<PerformanceMonitor>(_ => new PerformanceMonitor());
builder.Services.AddHostedService<PerformanceDataSimulator>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();

app.MapGet("/api/storage/blocks", (StoragePool pool) =>
{
    var blocks = new List<object>();
    foreach (StorageTier tier in Enum.GetValues(typeof(StorageTier)))
    {
        blocks.AddRange(pool.GetBlocksByTier(tier).Select(b => new
        {
            b.Id,
            b.Key,
            b.SizeBytes,
            Tier = b.Tier.ToString(),
            Type = b.Type.ToString(),
            b.AccessCount,
            b.LastAccessedAt,
            b.IsPinned,
            b.CreatedAt,
            b.IsUnmanaged,
            TimeToLive = b.TimeToLive?.TotalMilliseconds
        }));
    }
    return Results.Ok(blocks);
});

app.MapPost("/api/storage/allocate", (StoragePool pool, string key, long size, string? type, string? tier) =>
{
    var storageType = Enum.TryParse<StorageType>(type, out var st) ? st : StorageType.Memory;
    var storageTier = Enum.TryParse<StorageTier>(tier, out var sti) ? sti : StorageTier.Hot;
    var block = pool.Allocate(key, size, storageType, storageTier);
    return Results.Ok(new { block.Id, block.Key, block.SizeBytes });
});

app.MapDelete("/api/storage/{key}", (StoragePool pool, string key) =>
{
    var result = pool.Release(key);
    return Results.Ok(new { Success = result });
});

app.MapPost("/api/storage/{key}/promote", (StoragePool pool, string key, string targetTier) =>
{
    if (Enum.TryParse<StorageTier>(targetTier, out var tier))
    {
        var result = pool.Promote(key, tier);
        return Results.Ok(new { Success = result });
    }
    return Results.BadRequest("Invalid tier");
});

app.MapGet("/api/scheduler/tasks", (PimTaskScheduler scheduler) =>
{
    return Results.Ok(new
    {
        scheduler.PendingCount,
        scheduler.RunningCount,
        scheduler.CompletedCount,
        scheduler.FailedCount
    });
});

app.MapGet("/api/scheduler/resources", (HardwareResourceMonitor monitor) =>
{
    var resources = monitor.GetAvailableResources().Select(r => new
    {
        r.Id,
        Type = r.Type.ToString(),
        r.CoreCount,
        r.MemoryBytes,
        r.ComputeCapacity,
        r.EnergyEfficiencyRatio,
        r.CurrentLoad,
        r.IsAvailable
    });
    return Results.Ok(resources);
});

app.MapPost("/api/scheduler/submit", (PimTaskScheduler scheduler, string name, int priority) =>
{
    var taskPriority = Enum.TryParse<TaskPriority>(priority.ToString(), out var tp) ? tp : TaskPriority.Normal;
    var task = ComputeTask.Create(name, (Func<CancellationToken, Task>)(async ct =>
    {
        await Task.Delay(100, ct);
    }), priority: taskPriority);
    
    var taskId = scheduler.SubmitTask(task);
    return Results.Ok(new { TaskId = taskId });
});

app.MapPost("/api/scheduler/{taskId}/cancel", (PimTaskScheduler scheduler, string taskId) =>
{
    var result = scheduler.CancelTask(taskId);
    return Results.Ok(new { Success = result });
});

app.MapPost("/api/scheduler/start", (PimTaskScheduler scheduler) =>
{
    scheduler.Start();
    return Results.Ok(new { Status = "Started" });
});

app.MapPost("/api/scheduler/stop", (PimTaskScheduler scheduler) =>
{
    scheduler.Stop();
    return Results.Ok(new { Status = "Stopped" });
});

app.MapGet("/api/datapath/sharedmemory", (SharedMemoryManager manager) =>
{
    return Results.Ok(new
    {
        manager.TotalAllocatedBytes
    });
});

app.MapPost("/api/datapath/sharedmemory/create", (SharedMemoryManager manager, string name, long size) =>
{
    try
    {
        var region = manager.CreateRegion(name, size);
        return Results.Ok(new { region.Name, region.Size });
    }
    catch
    {
        return Results.BadRequest(new { Error = "Failed to create region" });
    }
});

app.MapDelete("/api/datapath/sharedmemory/{name}", (SharedMemoryManager manager, string name) =>
{
    var result = manager.ReleaseRegion(name);
    return Results.Ok(new { Success = result });
});

app.MapGet("/api/performance/metrics", (PerformanceMonitor monitor) =>
{
    var metrics = monitor.GetMetrics();
    return Results.Ok(new
    {
        metrics.DataMovementReductionRate,
        metrics.ComputeEnergyEfficiencyRatio,
        metrics.TaskSchedulingLatencyMs,
        metrics.HardwareUtilizationRate,
        metrics.ThroughputOpsPerSecond,
        metrics.AverageLatencyMs,
        metrics.MemoryAllocatedBytes,
        metrics.MemorySavedBytes,
        metrics.P95LatencyMs,
        metrics.P99LatencyMs,
        metrics.Timestamp,
        metrics.GcGen0Collections,
        metrics.GcGen1Collections,
        metrics.GcGen2Collections
    });
});

app.Run();
