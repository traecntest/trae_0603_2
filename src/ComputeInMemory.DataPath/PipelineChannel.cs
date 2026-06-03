using System.IO.Pipelines;

namespace ComputeInMemory.DataPath;

public sealed class PipelineChannel : IAsyncDisposable
{
    private readonly Pipe _pipe;
    private readonly string _channelId;
    private long _bytesTransferred;
    private long _messagesTransferred;

    public string ChannelId => _channelId;
    public long BytesTransferred => _bytesTransferred;
    public long MessagesTransferred => _messagesTransferred;

    public PipelineChannel(string channelId, PipeOptions? options = null)
    {
        _channelId = channelId;
        _pipe = new Pipe(options ?? new PipeOptions(
            pauseWriterThreshold: 64 * 1024,
            resumeWriterThreshold: 32 * 1024,
            minimumSegmentSize: 4096,
            useSynchronizationContext: false));
    }

    public PipeWriter Writer => _pipe.Writer;
    public PipeReader Reader => _pipe.Reader;

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        var writer = _pipe.Writer;
        var memory = writer.GetMemory(data.Length);
        data.Span.CopyTo(memory.Span);
        writer.Advance(data.Length);

        var result = await writer.FlushAsync(cancellationToken);
        if (result.IsCompleted) return;

        Interlocked.Add(ref _bytesTransferred, data.Length);
        Interlocked.Increment(ref _messagesTransferred);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _pipe.Reader.ReadAsync(cancellationToken);
        var buffer = result.Buffer;

        if (buffer.IsEmpty)
        {
            _pipe.Reader.AdvanceTo(buffer.Start);
            return ReadOnlyMemory<byte>.Empty;
        }

        var length = (int)buffer.Length;
        var data = new byte[length];
        var span = data.AsSpan();
        foreach (var segment in buffer)
        {
            segment.Span.CopyTo(span);
            span = span[segment.Length..];
        }

        _pipe.Reader.AdvanceTo(buffer.End);
        return data;
    }

    public async ValueTask WriteMessageAsync<T>(T message, System.Text.Json.JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var json = System.Text.Json.JsonSerializer.Serialize(message, options);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var header = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, bytes.Length);

        await WriteAsync(header, cancellationToken);
        await WriteAsync(bytes, cancellationToken);
    }

    public async ValueTask<T?> ReadMessageAsync<T>(System.Text.Json.JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var headerData = await ReadAsync(cancellationToken);
        if (headerData.Length < 4) return null;

        var messageLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(headerData.Span);
        var messageData = await ReadAsync(cancellationToken);
        if (messageData.Length < messageLength) return null;

        var json = System.Text.Encoding.UTF8.GetString(messageData.Span[..messageLength]);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, options);
    }

    public void CompleteWriter() => _pipe.Writer.Complete();
    public void CompleteReader() => _pipe.Reader.Complete();

    public async ValueTask DisposeAsync()
    {
        await _pipe.Writer.CompleteAsync();
        await _pipe.Reader.CompleteAsync();
    }
}
