namespace ComputeInMemory.Storage;

public sealed class DataLifecycleManager : IDisposable
{
    private readonly StoragePool _pool;
    private readonly Timer _evaluationTimer;
    private readonly Dictionary<StorageTier, TimeSpan> _tierThresholds;
    private readonly Dictionary<StorageTier, int> _tierAccessThresholds;
    private readonly object _lock = new();
    private bool _disposed;

    public DataLifecycleManager(StoragePool pool,
        Dictionary<StorageTier, TimeSpan>? tierThresholds = null,
        Dictionary<StorageTier, int>? tierAccessThresholds = null,
        TimeSpan? evaluationInterval = null)
    {
        _pool = pool;
        _pool.BlockEvicted += OnBlockEvicted;
        _pool.BlockPromoted += OnBlockPromoted;

        _tierThresholds = tierThresholds ?? new Dictionary<StorageTier, TimeSpan>
        {
            [StorageTier.Hot] = TimeSpan.FromMinutes(5),
            [StorageTier.Warm] = TimeSpan.FromMinutes(30),
            [StorageTier.Cold] = TimeSpan.FromHours(6),
            [StorageTier.Archive] = TimeSpan.MaxValue
        };

        _tierAccessThresholds = tierAccessThresholds ?? new Dictionary<StorageTier, int>
        {
            [StorageTier.Hot] = 0,
            [StorageTier.Warm] = 3,
            [StorageTier.Cold] = 1,
            [StorageTier.Archive] = 0
        };

        var interval = evaluationInterval ?? TimeSpan.FromSeconds(30);
        _evaluationTimer = new Timer(EvaluateBlocks, null, interval, interval);
    }

    public int PromotionCount { get; private set; }
    public int DemotionCount { get; private set; }
    public int EvictionCount { get; private set; }

    public StorageTier DetermineOptimalTier(StorageBlock block)
    {
        var idleTime = DateTime.UtcNow - block.LastAccessedAt;
        var currentTierIndex = (int)block.Tier;

        if (block.IsPinned) return block.Tier;

        if (ShouldPromote(block, idleTime) && currentTierIndex > 0)
            return (StorageTier)(currentTierIndex - 1);

        if (ShouldDemote(block, idleTime) && currentTierIndex < (int)StorageTier.Archive)
            return (StorageTier)(currentTierIndex + 1);

        return block.Tier;
    }

    public void ForcePromote(string key, StorageTier targetTier)
    {
        _pool.Promote(key, targetTier);
    }

    public void MarkPinned(string key)
    {
        if (_pool.TryGet(key, out var block) && block != null)
            block.IsPinned = true;
    }

    public void Unpin(string key)
    {
        if (_pool.TryGet(key, out var block) && block != null)
            block.IsPinned = false;
    }

    private bool ShouldPromote(StorageBlock block, TimeSpan idleTime)
    {
        if (block.Tier == StorageTier.Hot) return false;
        var hotThreshold = _tierThresholds[StorageTier.Hot];
        return idleTime < hotThreshold && block.AccessCount > _tierAccessThresholds.GetValueOrDefault(block.Tier, 1);
    }

    private bool ShouldDemote(StorageBlock block, TimeSpan idleTime)
    {
        if (block.Tier == StorageTier.Archive) return false;
        var threshold = _tierThresholds.GetValueOrDefault(block.Tier, TimeSpan.MaxValue);
        return idleTime > threshold;
    }

    private void EvaluateBlocks(object? state)
    {
        if (_disposed) return;
        lock (_lock)
        {
            foreach (StorageTier tier in Enum.GetValues(typeof(StorageTier)))
            {
                var blocks = _pool.GetBlocksByTier(tier).ToList();
                foreach (var block in blocks)
                {
                    if (block.IsPinned) continue;
                    if (block.IsExpired)
                    {
                        _pool.Release(block.Key);
                        EvictionCount++;
                        continue;
                    }

                    var optimalTier = DetermineOptimalTier(block);
                    if (optimalTier != block.Tier)
                    {
                        if (_pool.Promote(block.Key, optimalTier))
                        {
                            if ((int)optimalTier < (int)block.Tier)
                                PromotionCount++;
                            else
                                DemotionCount++;
                        }
                    }
                }
            }
        }
    }

    private void OnBlockEvicted(object? sender, StorageBlock block) => EvictionCount++;
    private void OnBlockPromoted(object? sender, StorageBlock block) { }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _evaluationTimer.Dispose();
        _pool.BlockEvicted -= OnBlockEvicted;
        _pool.BlockPromoted -= OnBlockPromoted;
    }
}
