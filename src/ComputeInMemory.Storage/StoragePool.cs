namespace ComputeInMemory.Storage;

public interface IStoragePool : IDisposable
{
    StorageBlock Allocate(string key, long sizeBytes, StorageType type = StorageType.Memory,
        StorageTier tier = StorageTier.Hot, TimeSpan? ttl = null);

    StorageBlock AllocateUnmanaged(string key, long sizeBytes, StorageTier tier = StorageTier.Hot);

    bool TryGet(string key, out StorageBlock? block);
    bool Release(string key);
    bool Promote(string key, StorageTier targetTier);
    long GetTotalCapacity(StorageType type);
    long GetUsedCapacity(StorageType type);
    IEnumerable<StorageBlock> GetBlocksByTier(StorageTier tier);
    event EventHandler<StorageBlock>? BlockEvicted;
    event EventHandler<StorageBlock>? BlockPromoted;
}

public sealed class StoragePool : IStoragePool
{
    private readonly Dictionary<string, StorageBlock> _blocks = new();
    private readonly Dictionary<StorageTier, long> _tierCapacity;
    private readonly Dictionary<StorageType, long> _typeCapacity;
    private readonly Dictionary<StorageTier, long> _tierUsed = new();
    private readonly Dictionary<StorageType, long> _typeUsed = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event EventHandler<StorageBlock>? BlockEvicted;
    public event EventHandler<StorageBlock>? BlockPromoted;

    public StoragePool(Dictionary<StorageTier, long>? tierCapacity = null,
        Dictionary<StorageType, long>? typeCapacity = null)
    {
        _tierCapacity = tierCapacity ?? new Dictionary<StorageTier, long>
        {
            [StorageTier.Hot] = 1024L * 1024 * 1024,
            [StorageTier.Warm] = 4L * 1024 * 1024 * 1024,
            [StorageTier.Cold] = 16L * 1024 * 1024 * 1024,
            [StorageTier.Archive] = long.MaxValue
        };

        _typeCapacity = typeCapacity ?? new Dictionary<StorageType, long>
        {
            [StorageType.Memory] = 2L * 1024 * 1024 * 1024,
            [StorageType.Cache] = 512L * 1024 * 1024,
            [StorageType.Persistent] = long.MaxValue,
            [StorageType.Distributed] = long.MaxValue,
            [StorageType.NearCompute] = 1L * 1024 * 1024 * 1024
        };

        foreach (StorageTier tier in Enum.GetValues(typeof(StorageTier)))
            _tierUsed[tier] = 0;

        foreach (StorageType type in Enum.GetValues(typeof(StorageType)))
            _typeUsed[type] = 0;
    }

    public StorageBlock Allocate(string key, long sizeBytes, StorageType type = StorageType.Memory,
        StorageTier tier = StorageTier.Hot, TimeSpan? ttl = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            EnsureCapacity(sizeBytes, tier, type);

            var data = new byte[sizeBytes];
            var block = new StorageBlock(key, data, type, tier, ttl);
            _blocks[key] = block;
            _tierUsed[tier] += sizeBytes;
            _typeUsed[type] += sizeBytes;
            return block;
        }
    }

    public StorageBlock AllocateUnmanaged(string key, long sizeBytes, StorageTier tier = StorageTier.Hot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            var type = StorageType.NearCompute;
            EnsureCapacity(sizeBytes, tier, type);

            var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal((nint)sizeBytes);
            var block = new StorageBlock(key, ptr, sizeBytes, type, tier);
            _blocks[key] = block;
            _tierUsed[tier] += sizeBytes;
            _typeUsed[type] += sizeBytes;
            return block;
        }
    }

    public bool TryGet(string key, out StorageBlock? block)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            if (_blocks.TryGetValue(key, out var b) && !b.IsExpired)
            {
                block = b;
                return true;
            }

            if (_blocks.TryGetValue(key, out var expired))
            {
                Release(key);
            }

            block = null;
            return false;
        }
    }

    public bool Release(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            if (!_blocks.TryGetValue(key, out var block)) return false;
            _tierUsed[block.Tier] -= block.SizeBytes;
            _typeUsed[block.Type] -= block.SizeBytes;
            _blocks.Remove(key);
            block.Dispose();
            return true;
        }
    }

    public bool Promote(string key, StorageTier targetTier)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            if (!_blocks.TryGetValue(key, out var block)) return false;
            if (block.Tier == targetTier) return true;

            var oldTier = block.Tier;
            _tierUsed[oldTier] -= block.SizeBytes;
            EnsureCapacity(block.SizeBytes, targetTier, block.Type);
            _tierUsed[targetTier] += block.SizeBytes;

            block.Tier = targetTier;
            BlockPromoted?.Invoke(this, block);
            return true;
        }
    }

    public long GetTotalCapacity(StorageType type) =>
        _typeCapacity.GetValueOrDefault(type, 0);

    public long GetUsedCapacity(StorageType type) =>
        _typeUsed.GetValueOrDefault(type, 0);

    public IEnumerable<StorageBlock> GetBlocksByTier(StorageTier tier)
    {
        lock (_lock)
        {
            return _blocks.Values.Where(b => b.Tier == tier).ToList();
        }
    }

    private void EnsureCapacity(long sizeBytes, StorageTier tier, StorageType type)
    {
        if (_tierCapacity.TryGetValue(tier, out var tierCap) &&
            _tierUsed[tier] + sizeBytes > tierCap)
        {
            EvictForSpace(tier, sizeBytes - (tierCap - _tierUsed[tier]));
        }

        if (_typeCapacity.TryGetValue(type, out var typeCap) &&
            _typeUsed[type] + sizeBytes > typeCap)
        {
            throw new InvalidOperationException(
                $"Insufficient {type} capacity: need {sizeBytes}, available {typeCap - _typeUsed[type]}");
        }
    }

    private void EvictForSpace(StorageTier tier, long neededBytes)
    {
        var candidates = _blocks.Values
            .Where(b => b.Tier == tier && !b.IsPinned)
            .OrderBy(b => b.LastAccessedAt)
            .ThenBy(b => b.AccessCount)
            .ToList();

        var freed = 0L;
        foreach (var candidate in candidates)
        {
            if (freed >= neededBytes) break;
            freed += candidate.SizeBytes;
            _blocks.Remove(candidate.Key);
            _tierUsed[tier] -= candidate.SizeBytes;
            _typeUsed[candidate.Type] -= candidate.SizeBytes;
            BlockEvicted?.Invoke(this, candidate);
            candidate.Dispose();
        }

        if (freed < neededBytes)
            throw new InvalidOperationException(
                $"Cannot free enough space in {tier} tier: needed {neededBytes}, freed {freed}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            foreach (var block in _blocks.Values)
                block.Dispose();
            _blocks.Clear();
        }
    }
}
