namespace ComputeInMemory.Scheduler;

public sealed class TaskScheduler : IDisposable
{
    private readonly HardwareResourceMonitor _resourceMonitor;
    private readonly ISchedulingStrategy _strategy;
    private readonly Dictionary<string, TaskDag> _dags = new();
    private readonly Dictionary<string, CancellationTokenSource> _taskCancellations = new();
    private readonly PriorityQueue<ComputeTask, (TaskPriority priority, DateTime enqueueTime)> _pendingQueue = new();
    private readonly Dictionary<string, ComputeTask> _runningTasks = new();
    private readonly SemaphoreSlim _schedulerLock = new(1, 1);
    private readonly Timer _schedulerTimer;
    private readonly TimeSpan _timeSlice;
    private bool _disposed;
    private bool _isRunning;

    public event EventHandler<ComputeTask>? TaskStarted;
    public event EventHandler<ComputeTask>? TaskCompleted;
    public event EventHandler<ComputeTask>? TaskFailed;
    public event EventHandler<ComputeTask>? TaskCancelled;

    public int PendingCount => _pendingQueue.Count;
    public int RunningCount => _runningTasks.Count;
    public int CompletedCount { get; private set; }
    public int FailedCount { get; private set; }

    public TaskScheduler(HardwareResourceMonitor resourceMonitor,
        ISchedulingStrategy? strategy = null,
        TimeSpan? schedulingInterval = null,
        TimeSpan? timeSlice = null)
    {
        _resourceMonitor = resourceMonitor;
        _strategy = strategy ?? new EnergyEfficiencySchedulingStrategy();
        _timeSlice = timeSlice ?? TimeSpan.FromMilliseconds(100);

        var interval = schedulingInterval ?? TimeSpan.FromMilliseconds(50);
        _schedulerTimer = new Timer(ScheduleNext, null, interval, interval);
    }

    public void Start() => _isRunning = true;
    public void Stop() => _isRunning = false;

    public string SubmitDag(TaskDag dag)
    {
        if (dag.HasCycle())
            throw new InvalidOperationException("DAG contains a cycle");

        lock (_dags)
        {
            _dags[dag.DagId] = dag;
        }

        var readyTasks = dag.GetReadyTasks();
        foreach (var task in readyTasks)
        {
            EnqueueTask(task);
        }

        return dag.DagId;
    }

    public string SubmitTask(ComputeTask task)
    {
        EnqueueTask(task);
        return task.Id.ToString();
    }

    public bool CancelTask(string taskId)
    {
        lock (_runningTasks)
        {
            if (_taskCancellations.TryGetValue(taskId, out var cts))
            {
                cts.Cancel();
                if (_runningTasks.TryGetValue(taskId, out var task))
                {
                    task.State = TaskState.Cancelled;
                    TaskCancelled?.Invoke(this, task);
                }

                return true;
            }
        }

        return false;
    }

    public bool PauseTask(string taskId)
    {
        lock (_runningTasks)
        {
            if (_runningTasks.TryGetValue(taskId, out var task))
            {
                task.State = TaskState.Paused;
                if (_taskCancellations.TryGetValue(taskId, out var cts))
                    cts.Cancel();
                return true;
            }
        }

        return false;
    }

    public bool ResumeTask(string taskId)
    {
        lock (_runningTasks)
        {
            if (_runningTasks.TryGetValue(taskId, out var task) && task.State == TaskState.Paused)
            {
                task.State = TaskState.Pending;
                EnqueueTask(task);
                return true;
            }
        }

        return false;
    }

    private void EnqueueTask(ComputeTask task)
    {
        task.State = TaskState.Scheduled;
        task.ScheduledAt = DateTime.UtcNow;
        _pendingQueue.Enqueue(task, (task.Priority, DateTime.UtcNow));
    }

    private async void ScheduleNext(object? state)
    {
        if (!_isRunning || _disposed) return;

        if (!_schedulerLock.Wait(0)) return;

        try
        {
            while (_pendingQueue.Count > 0 && _runningTasks.Count < GetMaxConcurrency())
            {
                if (!_pendingQueue.TryPeek(out var task, out _)) break;

                var availableResources = _resourceMonitor.GetAvailableResources(task.PreferredHardware);
                if (availableResources.Count == 0)
                {
                    var allResources = _resourceMonitor.GetAvailableResources();
                    var fallback = _strategy.SelectResource(task, allResources);
                    if (fallback == null) break;

                    _pendingQueue.Dequeue();
                    _ = ExecuteTaskAsync(task, fallback);
                }
                else
                {
                    var selected = _strategy.SelectResource(task, availableResources);
                    if (selected == null) break;

                    _pendingQueue.Dequeue();
                    _ = ExecuteTaskAsync(task, selected);
                }
            }
        }
        finally
        {
            _schedulerLock.Release();
        }
    }

    private int GetMaxConcurrency() =>
        _resourceMonitor.GetAvailableResources().Sum(r => r.CoreCount);

    private async Task ExecuteTaskAsync(ComputeTask task, HardwareResource resource)
    {
        var cts = new CancellationTokenSource();
        _taskCancellations[task.Id.ToString()] = cts;

        task.State = TaskState.Running;
        task.StartedAt = DateTime.UtcNow;
        task.AssignedResourceId = resource.Id;
        resource.CurrentLoad = Math.Clamp(resource.CurrentLoad + 0.1, 0, 1);

        lock (_runningTasks)
        {
            _runningTasks[task.Id.ToString()] = task;
        }

        _resourceMonitor.UpdateResourceLoad(resource.Id, resource.CurrentLoad);
        TaskStarted?.Invoke(this, task);

        try
        {
            await task.ExecutionFunc(cts.Token);
            task.State = TaskState.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.ActualDurationMs = (DateTime.UtcNow - task.StartedAt!.Value).TotalMilliseconds;
            CompletedCount++;
            TaskCompleted?.Invoke(this, task);

            ProcessDependents(task);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            if (task.State != TaskState.Paused)
            {
                task.State = TaskState.Cancelled;
                TaskCancelled?.Invoke(this, task);
            }
        }
        catch (Exception ex)
        {
            task.LastError = ex;
            task.RetryCount++;

            if (task.RetryCount < task.MaxRetryCount)
            {
                task.State = TaskState.Pending;
                EnqueueTask(task);
            }
            else
            {
                task.State = TaskState.Failed;
                FailedCount++;
                TaskFailed?.Invoke(this, task);
            }
        }
        finally
        {
            lock (_runningTasks)
            {
                _runningTasks.Remove(task.Id.ToString());
            }

            _taskCancellations.Remove(task.Id.ToString());

            resource.CurrentLoad = Math.Clamp(resource.CurrentLoad - 0.1, 0, 1);
            _resourceMonitor.UpdateResourceLoad(resource.Id, resource.CurrentLoad);
            cts.Dispose();
        }
    }

    private void ProcessDependents(ComputeTask completedTask)
    {
        lock (_dags)
        {
            foreach (var dag in _dags.Values)
            {
                var dependents = dag.GetDependents(completedTask.Id.ToString());
                foreach (var dependent in dependents)
                {
                    if (dependent.State == TaskState.Pending)
                    {
                        var readyTasks = dag.GetReadyTasks();
                        if (readyTasks.Any(t => t.Id == dependent.Id))
                            EnqueueTask(dependent);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _isRunning = false;
        _schedulerTimer.Dispose();

        foreach (var cts in _taskCancellations.Values)
            cts.Cancel();

        _schedulerLock.Dispose();
    }
}
