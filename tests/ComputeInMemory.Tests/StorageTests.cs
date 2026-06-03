using ComputeInMemory.Storage;

namespace ComputeInMemory.Tests;

public class StorageBlockTests
{
    [Fact]
    public void Constructor_WithManagedMemory_SetsProperties()
    {
        var data = new byte[1024];
        var block = new StorageBlock("test", data, StorageType.Memory, StorageTier.Hot);

        Assert.Equal("test", block.Key);
        Assert.Equal(1024, block.SizeBytes);
        Assert.Equal(StorageType.Memory, block.Type);
        Assert.Equal(StorageTier.Hot, block.Tier);
        Assert.False(block.IsPinned);
        Assert.Null(block.TimeToLive);
    }

    [Fact]
    public void GetMemory_ReturnsCorrectData()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var block = new StorageBlock("test", data, StorageType.Memory);

        var memory = block.GetMemory();
        Assert.Equal(5, memory.Length);
        Assert.Equal(1, memory.Span[0]);
        Assert.Equal(5, memory.Span[4]);
    }

    [Fact]
    public void GetSpan_ReturnsCorrectSpan()
    {
        var data = new byte[] { 10, 20, 30 };
        var block = new StorageBlock("test", data, StorageType.Memory);

        var span = block.GetSpan();
        Assert.Equal(3, span.Length);
        Assert.Equal(10, span[0]);
        Assert.Equal(30, span[2]);
    }

    [Fact]
    public void AccessCount_IncrementsOnEachAccess()
    {
        var data = new byte[100];
        var block = new StorageBlock("test", data);

        Assert.Equal(0, block.AccessCount);
        block.GetMemory();
        Assert.Equal(1, block.AccessCount);
        block.GetSpan();
        Assert.Equal(2, block.AccessCount);
    }

    [Fact]
    public void IsExpired_WithTtl_ReturnsTrueWhenExpired()
    {
        var data = new byte[100];
        var block = new StorageBlock("test", data, StorageType.Memory, StorageTier.Hot,
            TimeSpan.FromMilliseconds(50));

        Assert.False(block.IsExpired);
        Thread.Sleep(100);
        Assert.True(block.IsExpired);
    }

    [Fact]
    public void IsExpired_WithoutTtl_ReturnsFalse()
    {
        var data = new byte[100];
        var block = new StorageBlock("test", data);

        Assert.False(block.IsExpired);
    }

    [Fact]
    public void IsPinned_CanBeSetAndCleared()
    {
        var data = new byte[100];
        var block = new StorageBlock("test", data);

        Assert.False(block.IsPinned);
        block.IsPinned = true;
        Assert.True(block.IsPinned);
        block.IsPinned = false;
        Assert.False(block.IsPinned);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var data = new byte[100];
        var block = new StorageBlock("test", data);

        block.Dispose();
        block.Dispose();
    }

    [Fact]
    public void GetMemory_AfterDispose_ThrowsObjectDisposedException()
    {
        var data = new byte[100];
        var block = new StorageBlock("test", data);
        block.Dispose();

        Assert.Throws<ObjectDisposedException>(() => block.GetMemory());
    }
}

public class ZeroCopyAccessorTests
{
    [Fact]
    public void PinAndGetMemory_ReturnsPinnedMemory()
    {
        using var accessor = new ZeroCopyAccessor();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        var memory = accessor.PinAndGetMemory("key1", data);
        Assert.Equal(5, memory.Length);
        Assert.Equal(1, memory.Span[0]);
    }

    [Fact]
    public void PinAndGetMemory_ReplacesExistingPinnedHandle()
    {
        using var accessor = new ZeroCopyAccessor();
        var data1 = new byte[] { 1, 2, 3 };
        var data2 = new byte[] { 4, 5, 6, 7 };

        accessor.PinAndGetMemory("key1", data1);
        var memory = accessor.PinAndGetMemory("key1", data2);
        Assert.Equal(4, memory.Length);
        Assert.Equal(4, memory.Span[0]);
    }

    [Fact]
    public void AsSpan_CastsBytesToIntegers()
    {
        using var accessor = new ZeroCopyAccessor();
        var data = new byte[8];
        BitConverter.GetBytes(42).CopyTo(data, 0);
        BitConverter.GetBytes(100).CopyTo(data, 4);

        var memory = new Memory<byte>(data);
        var span = accessor.AsSpan<int>(memory);
        Assert.Equal(2, span.Length);
        Assert.Equal(42, span[0]);
        Assert.Equal(100, span[1]);
    }

    [Fact]
    public void AsReadOnlySpan_CastsBytesToDoubles()
    {
        using var accessor = new ZeroCopyAccessor();
        var data = new byte[16];
        BitConverter.GetBytes(3.14).CopyTo(data, 0);
        BitConverter.GetBytes(2.71).CopyTo(data, 8);

        var memory = new ReadOnlyMemory<byte>(data);
        var span = accessor.AsReadOnlySpan<double>(memory);
        Assert.Equal(2, span.Length);
        Assert.Equal(3.14, span[0], 0.001);
        Assert.Equal(2.71, span[1], 0.001);
    }

    [Fact]
    public void GetReference_ReturnsReferenceToFirstElement()
    {
        using var accessor = new ZeroCopyAccessor();
        var data = new byte[4];
        BitConverter.GetBytes(999).CopyTo(data, 0);

        var memory = new Memory<byte>(data);
        ref int value = ref accessor.GetReference<int>(memory);
        Assert.Equal(999, value);
    }

    [Fact]
    public void CopyBetweenBlocks_CopiesDataCorrectly()
    {
        using var accessor = new ZeroCopyAccessor();
        var srcData = new byte[] { 10, 20, 30, 40, 50 };
        var dstData = new byte[5];
        using var source = new StorageBlock("src", srcData);
        using var dest = new StorageBlock("dst", dstData);

        accessor.CopyBetweenBlocks(source, dest);
        var result = dest.GetSpan();
        Assert.Equal(10, result[0]);
        Assert.Equal(50, result[4]);
    }

    [Fact]
    public void CopyBetweenBlocks_WithOffset_CopiesCorrectly()
    {
        using var accessor = new ZeroCopyAccessor();
        var srcData = new byte[] { 1, 2, 3, 4, 5 };
        var dstData = new byte[10];
        using var source = new StorageBlock("src", srcData);
        using var dest = new StorageBlock("dst", dstData);

        accessor.CopyBetweenBlocks(source, dest, sourceOffset: 1, destinationOffset: 3, length: 3);
        var result = dest.GetSpan();
        Assert.Equal(0, result[0]);
        Assert.Equal(2, result[3]);
        Assert.Equal(4, result[5]);
    }

    [Fact]
    public unsafe void CopyFromUnmanaged_CopiesDataToManagedMemory()
    {
        using var accessor = new ZeroCopyAccessor();
        var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
        try
        {
            var srcData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            System.Runtime.InteropServices.Marshal.Copy(srcData, 0, ptr, 4);

            var dstMemory = new Memory<byte>(new byte[4]);
            accessor.CopyFromUnmanaged(ptr, dstMemory, 4);

            Assert.Equal(0xAA, dstMemory.Span[0]);
            Assert.Equal(0xDD, dstMemory.Span[3]);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
        }
    }

    [Fact]
    public unsafe void CopyToUnmanaged_CopiesDataFromManagedMemory()
    {
        using var accessor = new ZeroCopyAccessor();
        var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
        try
        {
            var srcMemory = new ReadOnlyMemory<byte>(new byte[] { 0x11, 0x22, 0x33, 0x44 });
            accessor.CopyToUnmanaged(srcMemory, ptr, 4);

            var result = new byte[4];
            System.Runtime.InteropServices.Marshal.Copy(ptr, result, 0, 4);
            Assert.Equal(0x11, result[0]);
            Assert.Equal(0x44, result[3]);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
        }
    }
}

public class StoragePoolTests
{
    [Fact]
    public void Allocate_CreatesBlockWithCorrectProperties()
    {
        using var pool = new StoragePool();
        var block = pool.Allocate("key1", 1024);

        Assert.Equal("key1", block.Key);
        Assert.Equal(1024, block.SizeBytes);
        Assert.Equal(StorageType.Memory, block.Type);
        Assert.Equal(StorageTier.Hot, block.Tier);
    }

    [Fact]
    public void TryGet_ReturnsTrueForExistingBlock()
    {
        using var pool = new StoragePool();
        pool.Allocate("key1", 100);

        Assert.True(pool.TryGet("key1", out var block));
        Assert.NotNull(block);
        Assert.Equal("key1", block!.Key);
    }

    [Fact]
    public void TryGet_ReturnsFalseForMissingBlock()
    {
        using var pool = new StoragePool();
        Assert.False(pool.TryGet("nonexistent", out _));
    }

    [Fact]
    public void Release_RemovesBlockAndReturnsTrue()
    {
        using var pool = new StoragePool();
        pool.Allocate("key1", 100);

        Assert.True(pool.Release("key1"));
        Assert.False(pool.TryGet("key1", out _));
    }

    [Fact]
    public void Release_NonexistentKey_ReturnsFalse()
    {
        using var pool = new StoragePool();
        Assert.False(pool.Release("nonexistent"));
    }

    [Fact]
    public void Promote_ChangesBlockTier()
    {
        using var pool = new StoragePool();
        var block = pool.Allocate("key1", 100, StorageType.Memory, StorageTier.Hot);

        Assert.True(pool.Promote("key1", StorageTier.Warm));
        Assert.True(pool.TryGet("key1", out var promoted));
        Assert.Equal(StorageTier.Warm, promoted!.Tier);
    }

    [Fact]
    public void GetUsedCapacity_TracksAllocation()
    {
        using var pool = new StoragePool();
        pool.Allocate("key1", 1000, StorageType.Memory);

        Assert.Equal(1000, pool.GetUsedCapacity(StorageType.Memory));
    }

    [Fact]
    public void GetUsedCapacity_DecreasesAfterRelease()
    {
        using var pool = new StoragePool();
        pool.Allocate("key1", 1000, StorageType.Memory);
        pool.Release("key1");

        Assert.Equal(0, pool.GetUsedCapacity(StorageType.Memory));
    }

    [Fact]
    public void GetBlocksByTier_ReturnsCorrectBlocks()
    {
        using var pool = new StoragePool();
        pool.Allocate("hot1", 100, StorageType.Memory, StorageTier.Hot);
        pool.Allocate("hot2", 200, StorageType.Memory, StorageTier.Hot);
        pool.Allocate("cold1", 300, StorageType.Persistent, StorageTier.Cold);

        var hotBlocks = pool.GetBlocksByTier(StorageTier.Hot).ToList();
        Assert.Equal(2, hotBlocks.Count);

        var coldBlocks = pool.GetBlocksByTier(StorageTier.Cold).ToList();
        Assert.Single(coldBlocks);
    }

    [Fact]
    public void BlockEvicted_FiredWhenCapacityExceeded()
    {
        var tierCapacity = new Dictionary<StorageTier, long>
        {
            [StorageTier.Hot] = 100,
            [StorageTier.Warm] = 1024,
            [StorageTier.Cold] = long.MaxValue,
            [StorageTier.Archive] = long.MaxValue
        };
        using var pool = new StoragePool(tierCapacity);

        StorageBlock? evictedBlock = null;
        pool.BlockEvicted += (_, block) => evictedBlock = block;

        pool.Allocate("first", 50, StorageType.Memory, StorageTier.Hot);
        pool.Allocate("second", 60, StorageType.Memory, StorageTier.Hot);

        Assert.NotNull(evictedBlock);
        Assert.Equal("first", evictedBlock!.Key);
    }

    [Fact]
    public void AllocateUnmanaged_CreatesBlockWithUnmanagedPointer()
    {
        using var pool = new StoragePool();
        var block = pool.AllocateUnmanaged("unmanaged1", 1024);

        Assert.Equal("unmanaged1", block.Key);
        Assert.Equal(1024, block.SizeBytes);
        Assert.Equal(StorageType.NearCompute, block.Type);

        pool.Release("unmanaged1");
    }
}

public class DataLifecycleManagerTests
{
    [Fact]
    public void DetermineOptimalTier_PinnedBlock_ReturnsCurrentTier()
    {
        using var pool = new StoragePool();
        var manager = new DataLifecycleManager(pool, evaluationInterval: TimeSpan.FromHours(1));
        var block = pool.Allocate("pinned", 100, StorageType.Memory, StorageTier.Hot);
        block.IsPinned = true;

        var tier = manager.DetermineOptimalTier(block);
        Assert.Equal(StorageTier.Hot, tier);
        manager.Dispose();
    }

    [Fact]
    public void MarkPinned_SetsBlockAsPinned()
    {
        using var pool = new StoragePool();
        var manager = new DataLifecycleManager(pool, evaluationInterval: TimeSpan.FromHours(1));
        pool.Allocate("key1", 100);

        manager.MarkPinned("key1");
        Assert.True(pool.TryGet("key1", out var block) && block!.IsPinned);
        manager.Dispose();
    }

    [Fact]
    public void Unpin_ClearsPinnedFlag()
    {
        using var pool = new StoragePool();
        var manager = new DataLifecycleManager(pool, evaluationInterval: TimeSpan.FromHours(1));
        pool.Allocate("key1", 100);

        manager.MarkPinned("key1");
        manager.Unpin("key1");
        Assert.True(pool.TryGet("key1", out var block) && !block!.IsPinned);
        manager.Dispose();
    }

    [Fact]
    public void ForcePromote_ChangesBlockTier()
    {
        using var pool = new StoragePool();
        var manager = new DataLifecycleManager(pool, evaluationInterval: TimeSpan.FromHours(1));
        pool.Allocate("key1", 100, StorageType.Memory, StorageTier.Hot);

        manager.ForcePromote("key1", StorageTier.Cold);
        Assert.True(pool.TryGet("key1", out var block));
        Assert.Equal(StorageTier.Cold, block!.Tier);
        manager.Dispose();
    }
}
