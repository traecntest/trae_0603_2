using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ComputeInMemory.Storage;

namespace ComputeInMemory.DataPath;

public enum PimOpCode : ushort
{
    NoOp = 0,
    Read = 1,
    Write = 2,
    Add = 3,
    Multiply = 4,
    Reduce = 5,
    Scan = 6,
    Filter = 7,
    BitwiseAnd = 8,
    BitwiseOr = 9,
    VectorAdd = 10,
    VectorMultiply = 11,
    MatrixMultiply = 12
}

public sealed class PimInstruction
{
    public PimOpCode OpCode { get; init; }
    public IntPtr SourceAddress { get; init; }
    public IntPtr DestinationAddress { get; init; }
    public long Length { get; init; }
    public IntPtr OperandAddress { get; init; }
    public long OperandLength { get; init; }
    public uint Flags { get; init; }
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}

public sealed class PimCommandPacket
{
    private readonly byte[] _buffer;
    private const int HeaderSize = 64;

    public int PacketSize => _buffer.Length;

    public PimCommandPacket(int maxPayloadSize = 4096)
    {
        _buffer = new byte[HeaderSize + maxPayloadSize];
    }

    public void EncodeInstruction(PimInstruction instruction)
    {
        var span = _buffer.AsSpan();
        Unsafe.WriteUnaligned(ref span[0], (ushort)instruction.OpCode);
        Unsafe.WriteUnaligned(ref span[2], instruction.SourceAddress);
        Unsafe.WriteUnaligned(ref span[10], instruction.DestinationAddress);
        Unsafe.WriteUnaligned(ref span[18], instruction.Length);
        Unsafe.WriteUnaligned(ref span[26], instruction.OperandAddress);
        Unsafe.WriteUnaligned(ref span[34], instruction.OperandLength);
        Unsafe.WriteUnaligned(ref span[42], instruction.Flags);

        var correlationBytes = instruction.CorrelationId.ToByteArray();
        correlationBytes.AsSpan().CopyTo(span.Slice(46, 16));
    }

    public PimInstruction DecodeInstruction()
    {
        var span = _buffer.AsSpan();
        var opCode = Unsafe.ReadUnaligned<PimOpCode>(ref span[0]);
        var sourceAddr = Unsafe.ReadUnaligned<IntPtr>(ref span[2]);
        var destAddr = Unsafe.ReadUnaligned<IntPtr>(ref span[10]);
        var length = Unsafe.ReadUnaligned<long>(ref span[18]);
        var operandAddr = Unsafe.ReadUnaligned<IntPtr>(ref span[26]);
        var operandLen = Unsafe.ReadUnaligned<long>(ref span[34]);
        var flags = Unsafe.ReadUnaligned<uint>(ref span[42]);

        var correlationBytes = span.Slice(46, 16);
        var correlationId = new Guid(correlationBytes.ToArray());

        return new PimInstruction
        {
            OpCode = opCode,
            SourceAddress = sourceAddr,
            DestinationAddress = destAddr,
            Length = length,
            OperandAddress = operandAddr,
            OperandLength = operandLen,
            Flags = flags,
            CorrelationId = correlationId
        };
    }

    public ReadOnlyMemory<byte> GetPacketBytes() => _buffer.AsMemory(0, HeaderSize);
}

public sealed class NearComputeChannel : IDisposable
{
    private readonly SharedMemoryRegion _commandBuffer;
    private readonly SharedMemoryRegion _responseBuffer;
    private readonly Dictionary<Guid, TaskCompletionSource<PimInstruction>> _pendingRequests = new();
    private readonly object _lock = new();
    private bool _disposed;
    private long _instructionsDispatched;

    public long InstructionsDispatched => _instructionsDispatched;

    public NearComputeChannel(long commandBufferSize = 4096, long responseBufferSize = 4096)
    {
        _commandBuffer = new SharedMemoryRegion("pim_cmd", commandBufferSize);
        _responseBuffer = new SharedMemoryRegion("pim_resp", responseBufferSize);
    }

    public PimInstruction CreateVectorAddInstruction(IntPtr sourceA, IntPtr sourceB, IntPtr destination, long length)
    {
        return new PimInstruction
        {
            OpCode = PimOpCode.VectorAdd,
            SourceAddress = sourceA,
            DestinationAddress = destination,
            Length = length,
            OperandAddress = sourceB,
            OperandLength = length
        };
    }

    public PimInstruction CreateVectorMultiplyInstruction(IntPtr sourceA, IntPtr sourceB, IntPtr destination,
        long length)
    {
        return new PimInstruction
        {
            OpCode = PimOpCode.VectorMultiply,
            SourceAddress = sourceA,
            DestinationAddress = destination,
            Length = length,
            OperandAddress = sourceB,
            OperandLength = length
        };
    }

    public PimInstruction CreateReduceInstruction(IntPtr source, IntPtr destination, long length)
    {
        return new PimInstruction
        {
            OpCode = PimOpCode.Reduce,
            SourceAddress = source,
            DestinationAddress = destination,
            Length = length
        };
    }

    public void DispatchInstruction(PimInstruction instruction)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var packet = new PimCommandPacket();
        packet.EncodeInstruction(instruction);
        var bytes = packet.GetPacketBytes();
        _commandBuffer.Write(bytes.Span);
        Interlocked.Increment(ref _instructionsDispatched);
    }

    public Task<PimInstruction> DispatchInstructionAsync(PimInstruction instruction,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var tcs = new TaskCompletionSource<PimInstruction>();

        lock (_lock)
        {
            _pendingRequests[instruction.CorrelationId] = tcs;
        }

        DispatchInstruction(instruction);

        cancellationToken.Register(() =>
        {
            lock (_lock)
            {
                _pendingRequests.Remove(instruction.CorrelationId);
            }

            tcs.TrySetCanceled(cancellationToken);
        });

        return tcs.Task;
    }

    public unsafe void SimulateVectorAdd(IntPtr sourceA, IntPtr sourceB, IntPtr destination, long length)
    {
        var srcA = (float*)sourceA.ToPointer();
        var srcB = (float*)sourceB.ToPointer();
        var dst = (float*)destination.ToPointer();
        var count = length / sizeof(float);

        for (var i = 0; i < count; i++)
        {
            dst[i] = srcA[i] + srcB[i];
        }
    }

    public unsafe void SimulateVectorMultiply(IntPtr sourceA, IntPtr sourceB, IntPtr destination, long length)
    {
        var srcA = (float*)sourceA.ToPointer();
        var srcB = (float*)sourceB.ToPointer();
        var dst = (float*)destination.ToPointer();
        var count = length / sizeof(float);

        for (var i = 0; i < count; i++)
        {
            dst[i] = srcA[i] * srcB[i];
        }
    }

    public unsafe float SimulateReduce(IntPtr source, long length)
    {
        var src = (float*)source.ToPointer();
        var count = length / sizeof(float);
        var sum = 0.0f;

        for (var i = 0; i < count; i++)
        {
            sum += src[i];
        }

        return sum;
    }

    public void CompleteInstruction(Guid correlationId, PimInstruction result)
    {
        lock (_lock)
        {
            if (_pendingRequests.Remove(correlationId, out var tcs))
                tcs.TrySetResult(result);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            foreach (var tcs in _pendingRequests.Values)
                tcs.TrySetCanceled();
            _pendingRequests.Clear();
        }

        _commandBuffer.Dispose();
        _responseBuffer.Dispose();
    }
}
