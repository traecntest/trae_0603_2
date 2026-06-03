using System.Runtime.InteropServices;
using ComputeInMemory.DataPath;

namespace ComputeInMemory.Tests;

public class SharedMemoryRegionTests
{
    [Fact]
    public void Constructor_AllocatesMemory()
    {
        using var region = new SharedMemoryRegion("test", 1024);
        Assert.True(region.IsAllocated);
        Assert.Equal(1024, region.Size);
        Assert.Equal("test", region.Name);
    }

    [Fact]
    public void AsSpan_ReturnsCorrectSize()
    {
        using var region = new SharedMemoryRegion("test", 1024);
        var span = region.AsSpan();
        Assert.Equal(1024, span.Length);
    }

    [Fact]
    public void Write_Read_Roundtrip()
    {
        using var region = new SharedMemoryRegion("test", 1024);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        region.Write(data);

        var buffer = new byte[5];
        region.Read(buffer, length: 5);
        Assert.Equal(1, buffer[0]);
        Assert.Equal(5, buffer[4]);
    }

    [Fact]
    public void Write_WithOffset_WritesAtCorrectPosition()
    {
        using var region = new SharedMemoryRegion("test", 1024);
        var data = new byte[] { 0xAA, 0xBB };
        region.Write(data, offset: 100);

        var buffer = new byte[2];
        region.Read(buffer, offset: 100, length: 2);
        Assert.Equal(0xAA, buffer[0]);
        Assert.Equal(0xBB, buffer[1]);
    }

    [Fact]
    public void Write_Read_Generic_Roundtrip()
    {
        using var region = new SharedMemoryRegion("test", 1024);
        region.Write(42, offset: 0);
        region.Write(3.14, offset: 4);

        Assert.Equal(42, region.Read<int>(0));
        Assert.Equal(3.14, region.Read<double>(4), 0.001);
    }

    [Fact]
    public void Write_ExceedsBounds_ThrowsArgumentOutOfRangeException()
    {
        using var region = new SharedMemoryRegion("test", 10);
        var data = new byte[20];
        Assert.Throws<ArgumentOutOfRangeException>(() => region.Write(data, offset: 0));
    }

    [Fact]
    public void Read_ExceedsBounds_ThrowsArgumentOutOfRangeException()
    {
        using var region = new SharedMemoryRegion("test", 10);
        var buffer = new byte[20];
        Assert.Throws<ArgumentOutOfRangeException>(() => region.Read(buffer, offset: 0));
    }

    [Fact]
    public void ZeroFill_SetsAllBytesToZero()
    {
        using var region = new SharedMemoryRegion("test", 16);
        var data = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
        region.Write(data);
        region.ZeroFill();

        var span = region.AsSpan();
        Assert.True(span.ToArray().All(b => b == 0));
    }

    [Fact]
    public void AsSpan_T_CastsToTypedSpan()
    {
        using var region = new SharedMemoryRegion("test", 16);
        var intSpan = region.AsSpan<int>();
        Assert.Equal(4, intSpan.Length);

        intSpan[0] = 100;
        intSpan[1] = 200;
        Assert.Equal(100, region.Read<int>(0));
        Assert.Equal(200, region.Read<int>(4));
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var region = new SharedMemoryRegion("test", 1024);
        region.Dispose();
        region.Dispose();
    }
}

public class SharedMemoryManagerTests
{
    [Fact]
    public void CreateRegion_CreatesAndStoresRegion()
    {
        using var manager = new SharedMemoryManager();
        var region = manager.CreateRegion("region1", 1024);

        Assert.NotNull(region);
        Assert.Equal("region1", region.Name);
        Assert.Same(region, manager.GetRegion("region1"));
    }

    [Fact]
    public void CreateRegion_DuplicateName_ThrowsInvalidOperationException()
    {
        using var manager = new SharedMemoryManager();
        manager.CreateRegion("region1", 1024);
        Assert.Throws<InvalidOperationException>(() => manager.CreateRegion("region1", 2048));
    }

    [Fact]
    public void GetRegion_Nonexistent_ReturnsNull()
    {
        using var manager = new SharedMemoryManager();
        Assert.Null(manager.GetRegion("nonexistent"));
    }

    [Fact]
    public void ReleaseRegion_RemovesAndDisposesRegion()
    {
        using var manager = new SharedMemoryManager();
        manager.CreateRegion("region1", 1024);

        Assert.True(manager.ReleaseRegion("region1"));
        Assert.Null(manager.GetRegion("region1"));
    }

    [Fact]
    public void ReleaseRegion_Nonexistent_ReturnsFalse()
    {
        using var manager = new SharedMemoryManager();
        Assert.False(manager.ReleaseRegion("nonexistent"));
    }

    [Fact]
    public void TotalAllocatedBytes_TracksAllRegions()
    {
        using var manager = new SharedMemoryManager();
        manager.CreateRegion("r1", 1024);
        manager.CreateRegion("r2", 2048);

        Assert.Equal(3072, manager.TotalAllocatedBytes);
    }
}

public class NativeMemoryBridgeTests
{
    [Fact]
    public void Allocate_ReturnsNonNullPointer()
    {
        var ptr = NativeMemoryBridge.Allocate(1024);
        Assert.NotEqual(IntPtr.Zero, ptr);
        NativeMemoryBridge.Free(ptr);
    }

    [Fact]
    public void Allocate_ZeroSize_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeMemoryBridge.Allocate(0));
    }

    [Fact]
    public void Free_ValidPointer_DoesNotThrow()
    {
        var ptr = NativeMemoryBridge.Allocate(1024);
        NativeMemoryBridge.Free(ptr);
    }

    [Fact]
    public void Copy_CopiesDataBetweenPointers()
    {
        var src = NativeMemoryBridge.Allocate(1024);
        var dst = NativeMemoryBridge.Allocate(1024);
        try
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            NativeMemoryBridge.CopyFromManaged(data, src, 5);
            NativeMemoryBridge.Copy(src, dst, 5);

            var result = new byte[5];
            NativeMemoryBridge.CopyToManaged(dst, result, 5);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, result);
        }
        finally
        {
            NativeMemoryBridge.Free(src);
            NativeMemoryBridge.Free(dst);
        }
    }

    [Fact]
    public void CopyFromManaged_CopyToManaged_Roundtrip()
    {
        var ptr = NativeMemoryBridge.Allocate(1024);
        try
        {
            var original = new byte[256];
            Random.Shared.NextBytes(original);

            NativeMemoryBridge.CopyFromManaged(original, ptr, 256);
            var result = new byte[256];
            NativeMemoryBridge.CopyToManaged(ptr, result, 256);

            Assert.Equal(original, result);
        }
        finally
        {
            NativeMemoryBridge.Free(ptr);
        }
    }

    [Fact]
    public void AsSpan_ReturnsCorrectSpan()
    {
        var ptr = NativeMemoryBridge.Allocate(1024);
        try
        {
            var data = new byte[] { 10, 20, 30, 40 };
            NativeMemoryBridge.CopyFromManaged(data, ptr, 4);

            var span = NativeMemoryBridge.AsSpan<byte>(ptr, 4);
            Assert.Equal(10, span[0]);
            Assert.Equal(40, span[3]);
        }
        finally
        {
            NativeMemoryBridge.Free(ptr);
        }
    }

    [Fact]
    public void AsSpan_Typed_ReturnsCorrectCount()
    {
        var ptr = NativeMemoryBridge.Allocate(1024);
        try
        {
            var intSpan = NativeMemoryBridge.AsSpan<int>(ptr, 256);
            Assert.Equal(256, intSpan.Length);
        }
        finally
        {
            NativeMemoryBridge.Free(ptr);
        }
    }

    [Fact]
    public void Write_Read_Roundtrip()
    {
        var ptr = NativeMemoryBridge.Allocate(1024);
        try
        {
            NativeMemoryBridge.Write(ptr, 42);
            Assert.Equal(42, NativeMemoryBridge.Read<int>(ptr));

            NativeMemoryBridge.Write(ptr + 4, 3.14);
            Assert.Equal(3.14, NativeMemoryBridge.Read<double>(ptr + 4), 0.001);
        }
        finally
        {
            NativeMemoryBridge.Free(ptr);
        }
    }

    [Fact]
    public void GetRef_ReturnsReferenceToElement()
    {
        var ptr = NativeMemoryBridge.Allocate(1024);
        try
        {
            ref int value = ref NativeMemoryBridge.GetRef<int>(ptr, 0);
            value = 999;
            Assert.Equal(999, NativeMemoryBridge.Read<int>(ptr));
        }
        finally
        {
            NativeMemoryBridge.Free(ptr);
        }
    }

    [Fact]
    public void ManagedFromNative_NativeFromManaged_Roundtrip()
    {
        var original = new byte[128];
        Random.Shared.NextBytes(original);

        var ptr = NativeMemoryBridge.NativeFromManaged(original);
        try
        {
            var managed = NativeMemoryBridge.ManagedFromNative(ptr, 128);
            Assert.Equal(original, managed.ToArray());
        }
        finally
        {
            NativeMemoryBridge.Free(ptr);
        }
    }

    [Fact]
    public void ZeroFill_SetsAllBytesToZero()
    {
        var ptr = NativeMemoryBridge.Allocate(64);
        try
        {
            var data = new byte[64];
            Array.Fill(data, (byte)0xFF);
            NativeMemoryBridge.CopyFromManaged(data, ptr, 64);

            NativeMemoryBridge.ZeroFill(ptr, 64);

            var result = new byte[64];
            NativeMemoryBridge.CopyToManaged(ptr, result, 64);
            Assert.True(result.All(b => b == 0));
        }
        finally
        {
            NativeMemoryBridge.Free(ptr);
        }
    }

    [Fact]
    public void AsFloatSpan_ReturnsCorrectSpan()
    {
        var ptr = NativeMemoryBridge.Allocate(1024);
        try
        {
            var floatSpan = NativeMemoryBridge.AsFloatSpan(ptr, 256);
            Assert.Equal(256, floatSpan.Length);
        }
        finally
        {
            NativeMemoryBridge.Free(ptr);
        }
    }
}

public class NearComputeChannelTests
{
    [Fact]
    public void CreateVectorAddInstruction_CreatesCorrectInstruction()
    {
        using var channel = new NearComputeChannel();
        var srcA = IntPtr.Zero + 100;
        var srcB = IntPtr.Zero + 200;
        var dst = IntPtr.Zero + 300;

        var instruction = channel.CreateVectorAddInstruction(srcA, srcB, dst, 1024);

        Assert.Equal(PimOpCode.VectorAdd, instruction.OpCode);
        Assert.Equal(srcA, instruction.SourceAddress);
        Assert.Equal(srcB, instruction.OperandAddress);
        Assert.Equal(dst, instruction.DestinationAddress);
        Assert.Equal(1024, instruction.Length);
    }

    [Fact]
    public void CreateVectorMultiplyInstruction_CreatesCorrectInstruction()
    {
        using var channel = new NearComputeChannel();
        var instruction = channel.CreateVectorMultiplyInstruction(
            IntPtr.Zero + 100, IntPtr.Zero + 200, IntPtr.Zero + 300, 512);

        Assert.Equal(PimOpCode.VectorMultiply, instruction.OpCode);
        Assert.Equal(512, instruction.Length);
    }

    [Fact]
    public void CreateReduceInstruction_CreatesCorrectInstruction()
    {
        using var channel = new NearComputeChannel();
        var instruction = channel.CreateReduceInstruction(IntPtr.Zero + 100, IntPtr.Zero + 200, 2048);

        Assert.Equal(PimOpCode.Reduce, instruction.OpCode);
        Assert.Equal(2048, instruction.Length);
    }

    [Fact]
    public unsafe void SimulateVectorAdd_ComputesCorrectResult()
    {
        using var channel = new NearComputeChannel();
        var count = 4;
        var size = count * sizeof(float);

        var srcAPtr = NativeMemoryBridge.Allocate(size);
        var srcBPtr = NativeMemoryBridge.Allocate(size);
        var dstPtr = NativeMemoryBridge.Allocate(size);

        try
        {
            var srcA = (float*)srcAPtr.ToPointer();
            var srcB = (float*)srcBPtr.ToPointer();

            srcA[0] = 1.0f; srcA[1] = 2.0f; srcA[2] = 3.0f; srcA[3] = 4.0f;
            srcB[0] = 10.0f; srcB[1] = 20.0f; srcB[2] = 30.0f; srcB[3] = 40.0f;

            channel.SimulateVectorAdd(srcAPtr, srcBPtr, dstPtr, size);

            var dst = (float*)dstPtr.ToPointer();
            Assert.Equal(11.0f, dst[0], 0.001f);
            Assert.Equal(22.0f, dst[1], 0.001f);
            Assert.Equal(33.0f, dst[2], 0.001f);
            Assert.Equal(44.0f, dst[3], 0.001f);
        }
        finally
        {
            NativeMemoryBridge.Free(srcAPtr);
            NativeMemoryBridge.Free(srcBPtr);
            NativeMemoryBridge.Free(dstPtr);
        }
    }

    [Fact]
    public unsafe void SimulateVectorMultiply_ComputesCorrectResult()
    {
        using var channel = new NearComputeChannel();
        var count = 4;
        var size = count * sizeof(float);

        var srcAPtr = NativeMemoryBridge.Allocate(size);
        var srcBPtr = NativeMemoryBridge.Allocate(size);
        var dstPtr = NativeMemoryBridge.Allocate(size);

        try
        {
            var srcA = (float*)srcAPtr.ToPointer();
            var srcB = (float*)srcBPtr.ToPointer();

            srcA[0] = 2.0f; srcA[1] = 3.0f; srcA[2] = 4.0f; srcA[3] = 5.0f;
            srcB[0] = 10.0f; srcB[1] = 20.0f; srcB[2] = 30.0f; srcB[3] = 40.0f;

            channel.SimulateVectorMultiply(srcAPtr, srcBPtr, dstPtr, size);

            var dst = (float*)dstPtr.ToPointer();
            Assert.Equal(20.0f, dst[0], 0.001f);
            Assert.Equal(60.0f, dst[1], 0.001f);
            Assert.Equal(120.0f, dst[2], 0.001f);
            Assert.Equal(200.0f, dst[3], 0.001f);
        }
        finally
        {
            NativeMemoryBridge.Free(srcAPtr);
            NativeMemoryBridge.Free(srcBPtr);
            NativeMemoryBridge.Free(dstPtr);
        }
    }

    [Fact]
    public unsafe void SimulateReduce_ComputesCorrectSum()
    {
        using var channel = new NearComputeChannel();
        var count = 4;
        var size = count * sizeof(float);

        var srcPtr = NativeMemoryBridge.Allocate(size);

        try
        {
            var src = (float*)srcPtr.ToPointer();
            src[0] = 1.0f; src[1] = 2.0f; src[2] = 3.0f; src[3] = 4.0f;

            var result = channel.SimulateReduce(srcPtr, size);
            Assert.Equal(10.0f, result, 0.001f);
        }
        finally
        {
            NativeMemoryBridge.Free(srcPtr);
        }
    }

    [Fact]
    public void DispatchInstruction_IncrementsCounter()
    {
        using var channel = new NearComputeChannel();
        Assert.Equal(0, channel.InstructionsDispatched);

        var instruction = channel.CreateVectorAddInstruction(
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 100);
        channel.DispatchInstruction(instruction);

        Assert.Equal(1, channel.InstructionsDispatched);
    }

    [Fact]
    public void CompleteInstruction_CompletesPendingRequest()
    {
        using var channel = new NearComputeChannel();
        var instruction = channel.CreateVectorAddInstruction(
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 100);

        var task = channel.DispatchInstructionAsync(instruction);
        channel.CompleteInstruction(instruction.CorrelationId, instruction);

        Assert.True(task.IsCompleted);
    }
}

public class PimCommandPacketTests
{
    [Fact]
    public void EncodeDecode_Roundtrip()
    {
        var packet = new PimCommandPacket();
        var original = new PimInstruction
        {
            OpCode = PimOpCode.VectorAdd,
            SourceAddress = IntPtr.Zero + 0x1000,
            DestinationAddress = IntPtr.Zero + 0x2000,
            Length = 1024,
            OperandAddress = IntPtr.Zero + 0x3000,
            OperandLength = 1024,
            Flags = 0x01
        };

        packet.EncodeInstruction(original);
        var decoded = packet.DecodeInstruction();

        Assert.Equal(original.OpCode, decoded.OpCode);
        Assert.Equal(original.SourceAddress, decoded.SourceAddress);
        Assert.Equal(original.DestinationAddress, decoded.DestinationAddress);
        Assert.Equal(original.Length, decoded.Length);
        Assert.Equal(original.OperandAddress, decoded.OperandAddress);
        Assert.Equal(original.OperandLength, decoded.OperandLength);
        Assert.Equal(original.Flags, decoded.Flags);
        Assert.Equal(original.CorrelationId, decoded.CorrelationId);
    }
}
