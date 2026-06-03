namespace ComputeInMemory.Storage;

public enum StorageTier
{
    Hot,
    Warm,
    Cold,
    Archive
}

public enum StorageType
{
    Memory,
    Cache,
    Persistent,
    Distributed,
    NearCompute
}

public sealed class StorageBlock : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Key { get; }
    public StorageTier Tier { get; set; }
    public StorageType Type { get; }
    public long SizeBytes { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public int AccessCount { get; set; }
    public TimeSpan? TimeToLive { get; set; }
    public bool IsPinned { get; set; }

    private Memory<byte>? _memoryOwner;
    private IntPtr _unmanagedPtr;
    private bool _disposed;

    public bool IsUnmanaged => !_memoryOwner.HasValue;

    public StorageBlock(string key, Memory<byte> data, StorageType type = StorageType.Memory,
        StorageTier tier = StorageTier.Hot, TimeSpan? ttl = null)
    {
        Key = key;
        SizeBytes = data.Length;
        Type = type;
        Tier = tier;
        TimeToLive = ttl;
        _memoryOwner = data;
    }

    public StorageBlock(string key, IntPtr unmanagedPtr, long size, StorageType type = StorageType.NearCompute,
        StorageTier tier = StorageTier.Hot)
    {
        Key = key;
        SizeBytes = size;
        Type = type;
        Tier = tier;
        _unmanagedPtr = unmanagedPtr;
    }

    public Memory<byte> GetMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LastAccessedAt = DateTime.UtcNow;
        AccessCount++;
        return _memoryOwner ?? throw new InvalidOperationException("No managed memory available");
    }

    public Span<byte> GetSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LastAccessedAt = DateTime.UtcNow;
        AccessCount++;
        return _memoryOwner.HasValue ? _memoryOwner.Value.Span : throw new InvalidOperationException("No managed memory available");
    }

    public IntPtr GetUnmanagedPointer()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        LastAccessedAt = DateTime.UtcNow;
        AccessCount++;
        return _unmanagedPtr;
    }

    public bool IsExpired => TimeToLive.HasValue &&
        DateTime.UtcNow - CreatedAt > TimeToLive.Value;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _memoryOwner = null;
    }
}
