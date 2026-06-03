namespace ComputeInMemory.Scheduler;

public interface ISchedulingStrategy
{
    HardwareResource? SelectResource(ComputeTask task, IEnumerable<HardwareResource> availableResources);
}

public sealed class PrioritySchedulingStrategy : ISchedulingStrategy
{
    public HardwareResource? SelectResource(ComputeTask task, IEnumerable<HardwareResource> availableResources)
    {
        return availableResources
            .Where(r => r.IsAvailable && r.Type == task.PreferredHardware && r.CurrentLoad < 1.0)
            .OrderBy(r => r.CurrentLoad)
            .FirstOrDefault();
    }
}

public sealed class EnergyEfficiencySchedulingStrategy : ISchedulingStrategy
{
    private readonly double _efficiencyWeight;
    private readonly double _loadWeight;

    public EnergyEfficiencySchedulingStrategy(double efficiencyWeight = 0.7, double loadWeight = 0.3)
    {
        _efficiencyWeight = efficiencyWeight;
        _loadWeight = loadWeight;
    }

    public HardwareResource? SelectResource(ComputeTask task, IEnumerable<HardwareResource> availableResources)
    {
        var resources = availableResources
            .Where(r => r.IsAvailable && r.CurrentLoad < 1.0)
            .ToList();

        if (resources.Count == 0) return null;

        var maxEfficiency = resources.Max(r => r.EnergyEfficiencyRatio);
        var minLoad = resources.Min(r => r.CurrentLoad);

        return resources
            .Select(r => new
            {
                Resource = r,
                Score = (r.EnergyEfficiencyRatio / (maxEfficiency > 0 ? maxEfficiency : 1)) * _efficiencyWeight +
                        ((1 - (minLoad > 0 ? r.CurrentLoad / minLoad : r.CurrentLoad)) * _loadWeight)
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault()
            ?.Resource;
    }
}

public sealed class RoundRobinSchedulingStrategy : ISchedulingStrategy
{
    private int _index;
    private readonly object _lock = new();

    public HardwareResource? SelectResource(ComputeTask task, IEnumerable<HardwareResource> availableResources)
    {
        var resources = availableResources
            .Where(r => r.IsAvailable && r.CurrentLoad < 1.0)
            .ToList();

        if (resources.Count == 0) return null;

        lock (_lock)
        {
            _index = (_index + 1) % resources.Count;
            return resources[_index];
        }
    }
}

public sealed class TimeSliceSchedulingStrategy : ISchedulingStrategy
{
    private readonly TimeSpan _timeSlice;
    private readonly Dictionary<string, DateTime> _resourceLastUsed = new();

    public TimeSliceSchedulingStrategy(TimeSpan? timeSlice = null)
    {
        _timeSlice = timeSlice ?? TimeSpan.FromMilliseconds(100);
    }

    public HardwareResource? SelectResource(ComputeTask task, IEnumerable<HardwareResource> availableResources)
    {
        var now = DateTime.UtcNow;
        return availableResources
            .Where(r => r.IsAvailable && r.CurrentLoad < 1.0)
            .Select(r => new
            {
                Resource = r,
                IdleTime = _resourceLastUsed.TryGetValue(r.Id, out var lastUsed)
                    ? now - lastUsed
                    : TimeSpan.MaxValue
            })
            .Where(x => x.IdleTime >= _timeSlice || x.Resource.CurrentLoad == 0)
            .OrderByDescending(x => x.IdleTime)
            .FirstOrDefault()
            ?.Resource;
    }

    public void MarkResourceUsed(string resourceId)
    {
        _resourceLastUsed[resourceId] = DateTime.UtcNow;
    }
}
