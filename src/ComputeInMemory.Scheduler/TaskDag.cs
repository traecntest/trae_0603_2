namespace ComputeInMemory.Scheduler;

public sealed class TaskDag
{
    private readonly Dictionary<string, ComputeTask> _tasks = new();
    private readonly Dictionary<string, HashSet<string>> _adjacencyList = new();
    private readonly Dictionary<string, HashSet<string>> _reverseAdjacency = new();
    private readonly object _lock = new();

    public string DagId { get; } = Guid.NewGuid().ToString("N")[..8];
    public IReadOnlyDictionary<string, ComputeTask> Tasks => _tasks;

    public void AddTask(ComputeTask task)
    {
        lock (_lock)
        {
            _tasks[task.Id.ToString()] = task;
            if (!_adjacencyList.ContainsKey(task.Id.ToString()))
                _adjacencyList[task.Id.ToString()] = new HashSet<string>();
            if (!_reverseAdjacency.ContainsKey(task.Id.ToString()))
                _reverseAdjacency[task.Id.ToString()] = new HashSet<string>();

            foreach (var dep in task.Dependencies)
            {
                _adjacencyList[dep].Add(task.Id.ToString());
                _reverseAdjacency[task.Id.ToString()].Add(dep);
            }
        }
    }

    public void AddDependency(string fromTaskId, string toTaskId)
    {
        lock (_lock)
        {
            if (!_adjacencyList.ContainsKey(fromTaskId))
                _adjacencyList[fromTaskId] = new HashSet<string>();
            if (!_adjacencyList.ContainsKey(toTaskId))
                _adjacencyList[toTaskId] = new HashSet<string>();
            if (!_reverseAdjacency.ContainsKey(toTaskId))
                _reverseAdjacency[toTaskId] = new HashSet<string>();
            if (!_reverseAdjacency.ContainsKey(fromTaskId))
                _reverseAdjacency[fromTaskId] = new HashSet<string>();

            _adjacencyList[fromTaskId].Add(toTaskId);
            _reverseAdjacency[toTaskId].Add(fromTaskId);
        }
    }

    public bool HasCycle()
    {
        lock (_lock)
        {
            var visited = new HashSet<string>();
            var recursionStack = new HashSet<string>();

            foreach (var node in _adjacencyList.Keys)
            {
                if (DetectCycleDfs(node, visited, recursionStack))
                    return true;
            }

            return false;
        }
    }

    public List<ComputeTask> TopologicalSort()
    {
        lock (_lock)
        {
            var result = new List<ComputeTask>();
            var visited = new HashSet<string>();
            var tempMarked = new HashSet<string>();

            foreach (var node in _adjacencyList.Keys)
            {
                if (!visited.Contains(node))
                    TopologicalSortDfs(node, visited, tempMarked, result);
            }

            result.Reverse();
            return result;
        }
    }

    public List<ComputeTask> GetReadyTasks()
    {
        lock (_lock)
        {
            var ready = new List<ComputeTask>();
            foreach (var (id, task) in _tasks)
            {
                if (task.State != TaskState.Pending) continue;
                var deps = _reverseAdjacency.GetValueOrDefault(id, new HashSet<string>());
                if (deps.All(depId =>
                        _tasks.TryGetValue(depId, out var depTask) &&
                        depTask.State == TaskState.Completed))
                {
                    ready.Add(task);
                }
            }

            return ready.OrderByDescending(t => t.Priority).ToList();
        }
    }

    public List<ComputeTask> GetDependents(string taskId)
    {
        lock (_lock)
        {
            var dependents = _adjacencyList.GetValueOrDefault(taskId, new HashSet<string>());
            return dependents
                .Where(id => _tasks.ContainsKey(id))
                .Select(id => _tasks[id])
                .ToList();
        }
    }

    public int CriticalPathLength()
    {
        lock (_lock)
        {
            var sorted = TopologicalSort();
            var dist = new Dictionary<string, double>();
            foreach (var task in sorted)
            {
                dist[task.Id.ToString()] = 0;
            }

            foreach (var task in sorted)
            {
                var taskId = task.Id.ToString();
                foreach (var dependent in _adjacencyList.GetValueOrDefault(taskId, new HashSet<string>()))
                {
                    if (_tasks.TryGetValue(dependent, out var depTask))
                    {
                        var newDist = dist[taskId] + task.EstimatedDurationMs;
                        if (newDist > dist.GetValueOrDefault(dependent, 0))
                            dist[dependent] = newDist;
                    }
                }
            }

            return (int)(dist.Values.DefaultIfEmpty(0).Max());
        }
    }

    private bool DetectCycleDfs(string node, HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(node)) return true;
        if (visited.Contains(node)) return false;

        visited.Add(node);
        recursionStack.Add(node);

        foreach (var neighbor in _adjacencyList.GetValueOrDefault(node, new HashSet<string>()))
        {
            if (DetectCycleDfs(neighbor, visited, recursionStack))
                return true;
        }

        recursionStack.Remove(node);
        return false;
    }

    private void TopologicalSortDfs(string node, HashSet<string> visited, HashSet<string> tempMarked,
        List<ComputeTask> result)
    {
        if (tempMarked.Contains(node)) return;
        if (visited.Contains(node)) return;

        tempMarked.Add(node);

        foreach (var neighbor in _adjacencyList.GetValueOrDefault(node, new HashSet<string>()))
        {
            TopologicalSortDfs(neighbor, visited, tempMarked, result);
        }

        tempMarked.Remove(node);
        visited.Add(node);

        if (_tasks.TryGetValue(node, out var task))
            result.Add(task);
    }
}
