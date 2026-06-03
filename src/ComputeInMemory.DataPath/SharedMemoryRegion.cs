using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ComputeInMemory.DataPath;

public sealed unsafe class SharedMemoryRegion : IDisposable
{
    private readonly string _name;
    private readonly long _size;
    private byte* _pointer;
    private readonly bool _isOwner;
    private bool _disposed;
    private readonly object _lock = new();

    public string Name => _name;
    public long Size => _size;
    public IntPtr Pointer => (IntPtr)_pointer;
    public bool IsAllocated => _pointer != null;

       public SharedMemoryRegion(string name, long size, bool createNew = true)
    {
        _name = name;
        _size = size;
        _isOwner = createNew;

        if (createNew)
        {
            _pointer = (byte*)Marshal.AllocHGlobal((nint)size);
            Unsafe.InitBlockUnaligned(_pointer, 0, (uint)size);
        }
    }

    public Span<byte> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new Span<byte>(_pointer, (int)_size);
    }

    public Memory<byte> AsMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return AsSpan().ToArray();
    }

    public Span<T> AsSpan<T>() where T : struct
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var byteSpan = new Span<byte>(_pointer, (int)_size);
        return MemoryMarshal.Cast<byte, T>(byteSpan);
    }

    public void Write(ReadOnlySpan<byte> data, long offset = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (offset + data.Length > _size)
            throw new ArgumentOutOfRangeException(nameof(offset), "Write exceeds region bounds");

        var destination = new Span<byte>(_pointer + (int)offset, data.Length);
        data.CopyTo(destination);
    }

    public void Read(Span<byte> buffer, long offset = 0, int? length = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var readLength = length ?? buffer.Length;
        if (offset + readLength > _size)
            throw new ArgumentOutOfRangeException(nameof(offset), "Read exceeds region bounds");

        var source = new ReadOnlySpan<byte>(_pointer + (int)offset, readLength);
        source.CopyTo(buffer);
    }

    public void Write<T>(in T value, long offset = 0) where T : struct
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (offset + Unsafe.SizeOf<T>() > _size)
            throw new ArgumentOutOfRangeException(nameof(offset));

        Unsafe.Write(_pointer + (int)offset, value);
    }

    public T Read<T>(long offset = 0) where T : struct
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (offset + Unsafe.SizeOf<T>() > _size)
            throw new ArgumentOutOfRangeException(nameof(offset));

        return Unsafe.Read<T>(_pointer + (int)offset);
    }

    public IntPtr Attach(IntPtr externalPointer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pointer = (byte*)externalPointer;
        return (IntPtr)_pointer;
    }

    public void ZeroFill()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Unsafe.InitBlockUnaligned(_pointer, 0, (uint)_size);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_isOwner && _pointer != null)
        {
            Marshal.FreeHGlobal((IntPtr)_pointer);
            _pointer = null;
        }
    }
}

public sealed class SharedMemoryManager : IDisposable
{
    private readonly Dictionary<string, SharedMemoryRegion> _regions = new();
    private readonly object _lock = new();
    private bool _disposed;

    public SharedMemoryRegion CreateRegion(string name, long size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            if (_regions.ContainsKey(name))
                throw new InvalidOperationException($"Region '{name}' already exists");

            var region = new SharedMemoryRegion(name, size, createNew: true);
            _regions[name] = region;
            return region;
        }
    }

    public SharedMemoryRegion? GetRegion(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            return _regions.GetValueOrDefault(name);
        }
    }

    public bool ReleaseRegion(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_lock)
        {
            if (_regions.Remove(name, out var region))
            {
                region.Dispose();
                return true;
            }

            return false;
        }
    }

    public long TotalAllocatedBytes
    {
        get
        {
            lock (_lock)
            {
                return _regions.Values.Sum(r => r.Size);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            foreach (var region in _regions.Values)
                region.Dispose();
            _regions.Clear();
        }
    }
}
