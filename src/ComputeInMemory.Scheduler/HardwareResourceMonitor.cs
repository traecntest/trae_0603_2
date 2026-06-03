namespace ComputeInMemory.Scheduler;

public sealed class HardwareResourceMonitor : IDisposable
{
    private readonly Dictionary<string, HardwareResource> _resources = new();
    private readonly Timer _monitorTimer;
    private readonly object _lock = new();
    private bool _disposed;

    public event EventHandler<HardwareResource>? ResourceAdded;
    public event EventHandler<HardwareResource>? ResourceRemoved;
    public event EventHandler<HardwareResource>? ResourceStatusChanged;

    public HardwareResourceMonitor(TimeSpan? monitorInterval = null)
    {
        var interval = monitorInterval ?? TimeSpan.FromSeconds(5);
        _monitorTimer = new Timer(MonitorResources, null, interval, interval);
    }

    public void RegisterResource(HardwareResource resource)
    {
        lock (_lock)
        {
            _resources[resource.Id] = resource;
            ResourceAdded?.Invoke(this, resource);
        }
    }

    public void RemoveResource(string resourceId)
    {
        lock (_lock)
        {
            if (_resources.Remove(resourceId, out var resource))
                ResourceRemoved?.Invoke(this, resource);
        }
    }

    public IReadOnlyList<HardwareResource> GetAvailableResources(HardwareType? type = null)
    {
        lock (_lock)
        {
            var query = _resources.Values.Where(r => r.IsAvailable);
            if (type.HasValue)
                query = query.Where(r => r.Type == type.Value);
            return query.ToList();
        }
    }

    public HardwareResource? GetResource(string resourceId)
    {
        lock (_lock)
        {
            return _resources.GetValueOrDefault(resourceId);
        }
    }

    public void UpdateResourceLoad(string resourceId, double load)
    {
        lock (_lock)
        {
            if (_resources.TryGetValue(resourceId, out var resource))
            {
                resource.CurrentLoad = Math.Clamp(load, 0, 1);
                ResourceStatusChanged?.Invoke(this, resource);
            }
        }
    }

    public double GetAverageLoad(HardwareType? type = null)
    {
        lock (_lock)
        {
            var resources = GetAvailableResources(type);
            return resources.Count == 0 ? 0 : resources.Average(r => r.CurrentLoad);
        }
    }

    public Dictionary<HardwareType, int> GetResourceCounts()
    {
        lock (_lock)
        {
            return _resources.Values
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }

    public bool HasCapacity(HardwareType type, long memoryBytes)
    {
        lock (_lock)
        {
            return _resources.Values.Any(r =>
                r.Type == type && r.IsAvailable && r.MemoryBytes >= memoryBytes && r.CurrentLoad < 1.0);
        }
    }

    private void MonitorResources(object? state)
    {
        if (_disposed) return;
        lock (_lock)
        {
            foreach (var resource in _resources.Values)
            {
                var wasAvailable = resource.IsAvailable;
                resource.IsAvailable = resource.CurrentLoad < 0.95;
                if (wasAvailable != resource.IsAvailable)
                    ResourceStatusChanged?.Invoke(this, resource);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitorTimer.Dispose();
    }
}
