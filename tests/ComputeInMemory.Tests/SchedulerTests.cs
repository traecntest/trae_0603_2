using ComputeInMemory.Scheduler;

namespace ComputeInMemory.Tests;

public class TaskDagTests
{
    [Fact]
    public void AddTask_AddsTaskToDag()
    {
        var dag = new TaskDag();
        var task = ComputeTask.Create(() => { }, name: "task1");

        dag.AddTask(task);
        Assert.Single(dag.Tasks);
        Assert.Equal(task.Id.ToString(), dag.Tasks.First().Key);
    }

    [Fact]
    public void HasCycle_NoCycle_ReturnsFalse()
    {
        var dag = new TaskDag();
        var task1 = ComputeTask.Create(() => { }, name: "task1");
        var task2 = ComputeTask.Create(() => { }, name: "task2");
        task2.Dependencies.Add(task1.Id.ToString());

        dag.AddTask(task1);
        dag.AddTask(task2);

        Assert.False(dag.HasCycle());
    }

    [Fact]
    public void HasCycle_WithCycle_ReturnsTrue()
    {
        var dag = new TaskDag();
        var task1 = ComputeTask.Create(() => { }, name: "task1");
        var task2 = ComputeTask.Create(() => { }, name: "task2");
        var task3 = ComputeTask.Create(() => { }, name: "task3");

        dag.AddTask(task1);
        dag.AddTask(task2);
        dag.AddTask(task3);

        dag.AddDependency(task1.Id.ToString(), task2.Id.ToString());
        dag.AddDependency(task2.Id.ToString(), task3.Id.ToString());
        dag.AddDependency(task3.Id.ToString(), task1.Id.ToString());

        Assert.True(dag.HasCycle());
    }

    [Fact]
    public void TopologicalSort_ReturnsCorrectOrder()
    {
        var dag = new TaskDag();
        var task1 = ComputeTask.Create(() => { }, name: "task1");
        var task2 = ComputeTask.Create(() => { }, name: "task2");
        var task3 = ComputeTask.Create(() => { }, name: "task3");
        task2.Dependencies.Add(task1.Id.ToString());
        task3.Dependencies.Add(task2.Id.ToString());

        dag.AddTask(task1);
        dag.AddTask(task2);
        dag.AddTask(task3);

        var sorted = dag.TopologicalSort();
        Assert.Equal(3, sorted.Count);

        var task1Idx = sorted.FindIndex(t => t.Id == task1.Id);
        var task2Idx = sorted.FindIndex(t => t.Id == task2.Id);
        var task3Idx = sorted.FindIndex(t => t.Id == task3.Id);
        Assert.True(task1Idx < task2Idx);
        Assert.True(task2Idx < task3Idx);
    }

    [Fact]
    public void GetReadyTasks_ReturnsTasksWithCompletedDependencies()
    {
        var dag = new TaskDag();
        var task1 = ComputeTask.Create(() => { }, name: "task1");
        var task2 = ComputeTask.Create(() => { }, name: "task2");
        var task3 = ComputeTask.Create(() => { }, name: "task3");
        task3.Dependencies.Add(task1.Id.ToString());
        task3.Dependencies.Add(task2.Id.ToString());

        dag.AddTask(task1);
        dag.AddTask(task2);
        dag.AddTask(task3);

        var ready = dag.GetReadyTasks();
        Assert.Equal(2, ready.Count);
        Assert.DoesNotContain(ready, t => t.Id == task3.Id);
    }

    [Fact]
    public void GetReadyTasks_AfterCompletingDependencies_ReturnsDependentTask()
    {
        var dag = new TaskDag();
        var task1 = ComputeTask.Create(() => { }, name: "task1");
        var task2 = ComputeTask.Create(() => { }, name: "task2");
        task2.Dependencies.Add(task1.Id.ToString());

        dag.AddTask(task1);
        dag.AddTask(task2);

        task1.State = TaskState.Completed;
        var ready = dag.GetReadyTasks();
        Assert.Single(ready);
        Assert.Equal(task2.Id, ready[0].Id);
    }

    [Fact]
    public void GetDependents_ReturnsCorrectDependents()
    {
        var dag = new TaskDag();
        var task1 = ComputeTask.Create(() => { }, name: "task1");
        var task2 = ComputeTask.Create(() => { }, name: "task2");
        var task3 = ComputeTask.Create(() => { }, name: "task3");

        dag.AddTask(task1);
        dag.AddTask(task2);
        dag.AddTask(task3);

        dag.AddDependency(task1.Id.ToString(), task2.Id.ToString());
        dag.AddDependency(task1.Id.ToString(), task3.Id.ToString());

        var dependents = dag.GetDependents(task1.Id.ToString());
        Assert.Equal(2, dependents.Count);
    }

    [Fact]
    public void CriticalPathLength_ReturnsCorrectLength()
    {
        var dag = new TaskDag();
        var task1 = ComputeTask.Create(() => { }, name: "task1", estimatedDurationMs: 100);
        var task2 = ComputeTask.Create(() => { }, name: "task2", estimatedDurationMs: 200);
        task2.Dependencies.Add(task1.Id.ToString());

        dag.AddTask(task1);
        dag.AddTask(task2);

        var length = dag.CriticalPathLength();
        Assert.Equal(100, length);
    }
}

public class SchedulingStrategiesTests
{
    private static List<HardwareResource> CreateTestResources()
    {
        return new List<HardwareResource>
        {
            new()
            {
                Id = "cpu1", Type = HardwareType.CPU, CoreCount = 8, MemoryBytes = 16L * 1024 * 1024 * 1024,
                ComputeCapacity = 100, EnergyEfficiencyRatio = 10, CurrentLoad = 0.3, IsAvailable = true
            },
            new()
            {
                Id = "gpu1", Type = HardwareType.GPU, CoreCount = 1024, MemoryBytes = 8L * 1024 * 1024 * 1024,
                ComputeCapacity = 1000, EnergyEfficiencyRatio = 5, CurrentLoad = 0.1, IsAvailable = true
            },
            new()
            {
                Id = "pim1", Type = HardwareType.PIM, CoreCount = 256, MemoryBytes = 4L * 1024 * 1024 * 1024,
                ComputeCapacity = 500, EnergyEfficiencyRatio = 20, CurrentLoad = 0.0, IsAvailable = true
            }
        };
    }

    [Fact]
    public void PriorityStrategy_SelectsPreferredHardwareType()
    {
        var strategy = new PrioritySchedulingStrategy();
        var task = ComputeTask.Create(() => { }, preferredHardware: HardwareType.GPU);
        var resources = CreateTestResources();

        var selected = strategy.SelectResource(task, resources);
        Assert.NotNull(selected);
        Assert.Equal(HardwareType.GPU, selected!.Type);
    }

    [Fact]
    public void PriorityStrategy_SelectsLeastLoadedResource()
    {
        var strategy = new PrioritySchedulingStrategy();
        var task = ComputeTask.Create(() => { }, preferredHardware: HardwareType.CPU);
        var resources = CreateTestResources();

        var selected = strategy.SelectResource(task, resources);
        Assert.NotNull(selected);
        Assert.Equal("cpu1", selected!.Id);
    }

    [Fact]
    public void PriorityStrategy_NoAvailableResource_ReturnsNull()
    {
        var strategy = new PrioritySchedulingStrategy();
        var task = ComputeTask.Create(() => { }, preferredHardware: HardwareType.FPGA);
        var resources = CreateTestResources();

        var selected = strategy.SelectResource(task, resources);
        Assert.Null(selected);
    }

    [Fact]
    public void EnergyEfficiencyStrategy_PrefersHighEfficiencyResource()
    {
        var strategy = new EnergyEfficiencySchedulingStrategy();
        var task = ComputeTask.Create(() => { });
        var resources = CreateTestResources();

        var selected = strategy.SelectResource(task, resources);
        Assert.NotNull(selected);
        Assert.Equal(HardwareType.PIM, selected!.Type);
    }

    [Fact]
    public void RoundRobinStrategy_CyclesThroughResources()
    {
        var strategy = new RoundRobinSchedulingStrategy();
        var task = ComputeTask.Create(() => { });
        var resources = CreateTestResources();

        var first = strategy.SelectResource(task, resources);
        var second = strategy.SelectResource(task, resources);
        Assert.NotEqual(first!.Id, second!.Id);
    }
}

public class HardwareResourceMonitorTests
{
    [Fact]
    public void RegisterResource_AddsResource()
    {
        using var monitor = new HardwareResourceMonitor();
        var resource = new HardwareResource
        {
            Id = "cpu1", Type = HardwareType.CPU, CoreCount = 8, IsAvailable = true
        };

        monitor.RegisterResource(resource);
        var resources = monitor.GetAvailableResources();
        Assert.Single(resources);
    }

    [Fact]
    public void RemoveResource_RemovesResource()
    {
        using var monitor = new HardwareResourceMonitor();
        var resource = new HardwareResource
        {
            Id = "cpu1", Type = HardwareType.CPU, CoreCount = 8, IsAvailable = true
        };

        monitor.RegisterResource(resource);
        monitor.RemoveResource("cpu1");
        Assert.Empty(monitor.GetAvailableResources());
    }

    [Fact]
    public void GetAvailableResources_FilterByType()
    {
        using var monitor = new HardwareResourceMonitor();
        monitor.RegisterResource(new HardwareResource
        {
            Id = "cpu1", Type = HardwareType.CPU, CoreCount = 8, IsAvailable = true
        });
        monitor.RegisterResource(new HardwareResource
        {
            Id = "gpu1", Type = HardwareType.GPU, CoreCount = 1024, IsAvailable = true
        });

        var cpuResources = monitor.GetAvailableResources(HardwareType.CPU);
        Assert.Single(cpuResources);
        Assert.Equal(HardwareType.CPU, cpuResources[0].Type);
    }

    [Fact]
    public void UpdateResourceLoad_ChangesLoadValue()
    {
        using var monitor = new HardwareResourceMonitor();
        var resource = new HardwareResource
        {
            Id = "cpu1", Type = HardwareType.CPU, CoreCount = 8, CurrentLoad = 0, IsAvailable = true
        };
        monitor.RegisterResource(resource);

        monitor.UpdateResourceLoad("cpu1", 0.5);
        Assert.Equal(0.5, resource.CurrentLoad);
    }

    [Fact]
    public void GetAverageLoad_ReturnsCorrectAverage()
    {
        using var monitor = new HardwareResourceMonitor();
        monitor.RegisterResource(new HardwareResource
        {
            Id = "cpu1", Type = HardwareType.CPU, CoreCount = 8, CurrentLoad = 0.2, IsAvailable = true
        });
        monitor.RegisterResource(new HardwareResource
        {
            Id = "cpu2", Type = HardwareType.CPU, CoreCount = 4, CurrentLoad = 0.6, IsAvailable = true
        });

        var avgLoad = monitor.GetAverageLoad(HardwareType.CPU);
        Assert.Equal(0.4, avgLoad, 2);
    }

    [Fact]
    public void HasCapacity_ReturnsTrueWhenEnoughMemory()
    {
        using var monitor = new HardwareResourceMonitor();
        monitor.RegisterResource(new HardwareResource
        {
            Id = "cpu1", Type = HardwareType.CPU, CoreCount = 8,
            MemoryBytes = 1024 * 1024 * 1024, CurrentLoad = 0, IsAvailable = true
        });

        Assert.True(monitor.HasCapacity(HardwareType.CPU, 1024));
    }

    [Fact]
    public void HasCapacity_ReturnsFalseWhenNotEnoughMemory()
    {
        using var monitor = new HardwareResourceMonitor();
        monitor.RegisterResource(new HardwareResource
        {
            Id = "fpga1", Type = HardwareType.FPGA, CoreCount = 4,
            MemoryBytes = 1024, CurrentLoad = 0, IsAvailable = true
        });

        Assert.False(monitor.HasCapacity(HardwareType.FPGA, 2048));
    }

    [Fact]
    public void GetResourceCounts_ReturnsCorrectCounts()
    {
        using var monitor = new HardwareResourceMonitor();
        monitor.RegisterResource(new HardwareResource
        {
            Id = "cpu1", Type = HardwareType.CPU, CoreCount = 8, IsAvailable = true
        });
        monitor.RegisterResource(new HardwareResource
        {
            Id = "cpu2", Type = HardwareType.CPU, CoreCount = 4, IsAvailable = true
        });
        monitor.RegisterResource(new HardwareResource
        {
            Id = "gpu1", Type = HardwareType.GPU, CoreCount = 1024, IsAvailable = true
        });

        var counts = monitor.GetResourceCounts();
        Assert.Equal(2, counts[HardwareType.CPU]);
        Assert.Equal(1, counts[HardwareType.GPU]);
    }
}

public class ComputeTaskTests
{
    [Fact]
    public void Create_WithAction_SetsProperties()
    {
        var task = ComputeTask.Create(() => { }, priority: TaskPriority.High,
            preferredHardware: HardwareType.GPU, estimatedMemoryBytes: 1024);

        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(HardwareType.GPU, task.PreferredHardware);
        Assert.Equal(1024, task.EstimatedMemoryBytes);
        Assert.Equal(TaskState.Pending, task.State);
    }

    [Fact]
    public void Create_WithAsyncFunc_SetsProperties()
    {
        var task = ComputeTask.Create("test", async ct => await Task.Delay(1, ct),
            priority: TaskPriority.Critical);

        Assert.Equal("test", task.Name);
        Assert.Equal(TaskPriority.Critical, task.Priority);
    }

    [Fact]
    public void MaxRetryCount_DefaultIsThree()
    {
        var task = ComputeTask.Create(() => { });
        Assert.Equal(3, task.MaxRetryCount);
    }
}
