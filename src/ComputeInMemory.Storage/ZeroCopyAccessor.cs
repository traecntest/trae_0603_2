using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeInMemory.Storage;

public sealed class ZeroCopyAccessor : IDisposable
{
    private readonly Dictionary<string, GCHandle> _pinnedHandles = new();
    private readonly object _lock = new();
    private bool _disposed;

    public Memory<byte> PinAndGetMemory(string key, byte[] data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            if (_pinnedHandles.TryGetValue(key, out var existingHandle))
            {
                existingHandle.Free();
                _pinnedHandles.Remove(key);
            }

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            _pinnedHandles[key] = handle;
            return new Memory<byte>(data);
        }
    }

    public Span<T> AsSpan<T>(Memory<byte> memory) where T : struct
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return MemoryMarshal.Cast<byte, T>(memory.Span);
    }

    public ReadOnlySpan<T> AsReadOnlySpan<T>(ReadOnlyMemory<byte> memory) where T : struct
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return MemoryMarshal.Cast<byte, T>(memory.Span);
    }

    public ref T GetReference<T>(Memory<byte> memory) where T : struct
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, T>(memory.Span));
    }

    public Memory<byte> CopyBetweenBlocks(StorageBlock source, StorageBlock destination, int sourceOffset = 0,
        int destinationOffset = 0, int? length = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var srcSpan = source.GetSpan();
        var dstSpan = destination.GetSpan();
        var copyLength = length ?? Math.Min(srcSpan.Length - sourceOffset, dstSpan.Length - destinationOffset);

        srcSpan.Slice(sourceOffset, copyLength).CopyTo(dstSpan.Slice(destinationOffset));
        return destination.GetMemory().Slice(destinationOffset, copyLength);
    }

    public unsafe void CopyFromUnmanaged(IntPtr source, Memory<byte> destination, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var dstSpan = destination.Span;
        var srcSpan = new ReadOnlySpan<byte>(source.ToPointer(), Math.Min(length, dstSpan.Length));
        srcSpan.CopyTo(dstSpan);
    }

    public unsafe void CopyToUnmanaged(ReadOnlyMemory<byte> source, IntPtr destination, int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var srcSpan = source.Span;
        var dstSpan = new Span<byte>(destination.ToPointer(), Math.Min(length, srcSpan.Length));
        srcSpan.CopyTo(dstSpan);
    }

    public Memory<T> ReinterpretCast<T>(Memory<byte> memory) where T : struct
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Unsafe.SizeOf<T>() == 0)
            throw new ArgumentException("Cannot cast to zero-size type");

        var remainder = memory.Length % Unsafe.SizeOf<T>();
        if (remainder != 0)
            memory = memory[..^remainder];

        return MemoryMarshal.Cast<byte, T>(memory.Span).ToArray();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            foreach (var handle in _pinnedHandles.Values)
                handle.Free();
            _pinnedHandles.Clear();
        }
    }
}
