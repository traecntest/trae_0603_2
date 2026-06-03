using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ComputeInMemory.Storage;

namespace ComputeInMemory.DataPath;

public static unsafe class NativeMemoryBridge
{
    public static IntPtr Allocate(long sizeBytes)
    {
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        var ptr = Marshal.AllocHGlobal((nint)sizeBytes);
        Unsafe.InitBlockUnaligned(ptr.ToPointer(), 0, (uint)sizeBytes);
        return ptr;
    }

    public static void Free(IntPtr ptr) => Marshal.FreeHGlobal(ptr);

    public static void Copy(IntPtr source, IntPtr destination, long length)
    {
        if (length <= 0) return;
        var src = (void*)source;
        var dst = (void*)destination;
        Buffer.MemoryCopy(src, dst, length, length);
    }

    public static void CopyFromManaged(ReadOnlySpan<byte> source, IntPtr destination, int length)
    {
        if (length <= 0) return;
        ref var srcRef = ref MemoryMarshal.GetReference(source);
        Unsafe.CopyBlockUnaligned((void*)destination, Unsafe.AsPointer(ref srcRef), (uint)length);
    }

    public static void CopyToManaged(IntPtr source, Span<byte> destination, int length)
    {
        if (length <= 0) return;
        ref var dstRef = ref MemoryMarshal.GetReference(destination);
        Unsafe.CopyBlockUnaligned(Unsafe.AsPointer(ref dstRef), (void*)source, (uint)length);
    }

    public static Span<T> AsSpan<T>(IntPtr pointer, int count) where T : struct
    {
        if (pointer == IntPtr.Zero) throw new ArgumentNullException(nameof(pointer));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return new Span<T>(pointer.ToPointer(), count);
    }

    public static ReadOnlySpan<T> AsReadOnlySpan<T>(IntPtr pointer, int count) where T : struct
    {
        if (pointer == IntPtr.Zero) throw new ArgumentNullException(nameof(pointer));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return new ReadOnlySpan<T>(pointer.ToPointer(), count);
    }

    public static ref T GetRef<T>(IntPtr pointer, long elementIndex = 0) where T : struct
    {
        if (pointer == IntPtr.Zero) throw new ArgumentNullException(nameof(pointer));
        return ref Unsafe.AsRef<T>((void*)(pointer + (int)(elementIndex * Unsafe.SizeOf<T>())));
    }

    public static void Write<T>(IntPtr pointer, in T value) where T : struct
    {
        if (pointer == IntPtr.Zero) throw new ArgumentNullException(nameof(pointer));
        Unsafe.Write((void*)pointer, value);
    }

    public static T Read<T>(IntPtr pointer) where T : struct
    {
        if (pointer == IntPtr.Zero) throw new ArgumentNullException(nameof(pointer));
        return Unsafe.Read<T>((void*)pointer);
    }

    public static Memory<byte> ManagedFromNative(IntPtr source, long length)
    {
        var managed = new byte[length];
        CopyToManaged(source, managed, (int)length);
        return managed;
    }

    public static IntPtr NativeFromManaged(ReadOnlyMemory<byte> source)
    {
        var ptr = Allocate(source.Length);
        CopyFromManaged(source.Span, ptr, source.Length);
        return ptr;
    }

    public static Span<float> AsFloatSpan(IntPtr pointer, int floatCount) =>
        AsSpan<float>(pointer, floatCount);

    public static Span<int> AsIntSpan(IntPtr pointer, int intCount) =>
        AsSpan<int>(pointer, intCount);

    public static Span<double> AsDoubleSpan(IntPtr pointer, int doubleCount) =>
        AsSpan<double>(pointer, doubleCount);

    public static void ZeroFill(IntPtr pointer, long sizeBytes)
    {
        if (pointer == IntPtr.Zero) throw new ArgumentNullException(nameof(pointer));
        Unsafe.InitBlockUnaligned((void*)pointer, 0, (uint)sizeBytes);
    }
}
