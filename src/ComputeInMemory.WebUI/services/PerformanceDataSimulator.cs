using ComputeInMemory.Benchmark;

namespace ComputeInMemory.WebUI.Services;

public class PerformanceDataSimulator : BackgroundService
{
    private readonly PerformanceMonitor _monitor;
    private readonly Random _random = new();

    public PerformanceDataSimulator(PerformanceMonitor monitor)
    {
        _monitor = monitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            SimulateActivity();
            await Task.Delay(500, stoppingToken);
        }
    }

    private void SimulateActivity()
    {
        var taskCount = _random.Next(1, 5);
        for (int i = 0; i < taskCount; i++)
        {
            _monitor.RecordTaskScheduled();
            var schedulingDelay = _random.NextDouble() * 5;
            var executionTime = _random.NextDouble() * 50 + 10;
            _monitor.RecordTaskCompletion(executionTime, schedulingDelay);
        }

        var bytesMoved = _random.Next(1024, 65536);
        _monitor.RecordDataMovement(bytesMoved);
    }
}
